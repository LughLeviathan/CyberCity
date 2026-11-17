#!/usr/bin/env python3
"""Restore only this revision's files, retaining replaced bytes in a quarantine folder."""
import argparse
import hashlib
import json
import shutil
from datetime import datetime
from pathlib import Path


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("target", type=Path, help="Explicit project directory or isolated rollback fixture")
    args = parser.parse_args()
    target = args.target.resolve(strict=True)
    evidence = Path(__file__).resolve().parent
    changes = json.loads((evidence / "rollback-manifest.json").read_text(encoding="utf-8"))
    planned = []
    for rel, item in changes.items():
        path = (target / rel).resolve()
        if not path.is_relative_to(target) or path == target:
            raise SystemExit(f"Rejected path outside target: {rel}")
        original = evidence / "original" / rel
        if item["original"] is not None and (not original.is_file() or digest(original) != item["original"]):
            raise SystemExit(f"Original backup failed integrity check: {rel}")
        if not path.is_file() or digest(path) != item["modified"]:
            raise SystemExit(f"Target differs from the packaged revision; no files changed: {rel}")
        planned.append((rel, path, original, item))
    quarantine = target / "Temp" / ("RevisionRollback-" + datetime.now().strftime("%Y%m%d-%H%M%S-%f"))
    for rel, path, original, item in planned:
        backup = quarantine / rel
        backup.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, backup)
        if item["original"] is None:
            path.unlink()
        else:
            shutil.copy2(original, path)
    for rel, path, original, item in planned:
        if item["original"] is None:
            assert not path.exists(), rel
        else:
            assert digest(path) == item["original"], rel
    print(f"PASS: restored {len(planned)} revision paths; original hashes match; replaced files retained.")


if __name__ == "__main__":
    main()
