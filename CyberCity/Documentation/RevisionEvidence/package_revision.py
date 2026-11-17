"""Package this revision without changing the user's Git index."""
import hashlib
import json
import os
import shutil
import subprocess
import zipfile
from pathlib import Path

root = Path(__file__).resolve().parents[2]
evidence = Path(__file__).resolve().parent
repo = Path(subprocess.check_output(["git", "rev-parse", "--show-toplevel"], cwd=root, text=True).strip())
prefix = root.relative_to(repo).as_posix() + "/"
tracked = subprocess.check_output(["git", "diff", "--name-only", "HEAD"], cwd=repo, text=True).splitlines()
new = subprocess.check_output(["git", "ls-files", "--others", "--exclude-standard"], cwd=repo, text=True).splitlines()
files = []
for item in sorted(set(tracked + new)):
    if not item.startswith(prefix):
        continue
    rel = item[len(prefix):]
    if "RevisionVerificationRun" in rel:
        continue
    if rel == ".gitattributes" or rel in ("ProjectSettings/EditorBuildSettings.asset", "ProjectSettings/ProjectSettings.asset") or rel.startswith(("Assets/CyberCity/", "Tools/", "ArtSource/interiors/")):
        files.append((item, rel))
assert files
manifest = {}
for gitpath, rel in files:
    path = root / rel
    old = evidence / "original" / rel
    if not old.exists() and subprocess.run(["git", "cat-file", "-e", "HEAD:" + gitpath], cwd=repo, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode == 0:
        old.parent.mkdir(parents=True, exist_ok=True)
        old.write_bytes(subprocess.check_output(["git", "show", "HEAD:" + gitpath], cwd=repo))
    original = hashlib.sha256(old.read_bytes()).hexdigest() if old.exists() else None
    manifest[rel] = {"original": original, "modified": hashlib.sha256(path.read_bytes()).hexdigest()}
(evidence / "rollback-manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
with zipfile.ZipFile(evidence / "MODIFIED_FILE.zip", "w", zipfile.ZIP_DEFLATED) as archive:
    for _, rel in files:
        archive.write(root / rel, rel)
with zipfile.ZipFile(evidence / "MODIFIED_FILE.zip") as archive:
    assert archive.testzip() is None
    assert len(archive.namelist()) == len(files)
env = dict(os.environ, GIT_INDEX_FILE=str(root / "Temp/revision-diff.index"))
subprocess.run(["git", "read-tree", "HEAD"], cwd=repo, env=env, check=True)
subprocess.run(["git", "--literal-pathspecs", "add", "--"] + [p for p, _ in files], cwd=repo, env=env, check=True)
patch = subprocess.check_output(["git", "diff", "--cached", "--binary", "--relative=" + prefix.rstrip("/"), "--"] + [p for p, _ in files], cwd=repo, env=env)
(evidence / "DIFF_FILE.patch").write_bytes(patch)
assert patch.startswith(b"diff --git")
fixture = root / "Temp/RevisionRollbackFixture"
assert not fixture.exists(), "Use a new fixture directory rather than overwriting prior rollback evidence."
fixture.mkdir()
for _, rel in files:
    destination = (fixture / rel).resolve()
    assert destination.is_relative_to(fixture.resolve())
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(root / rel, destination)
# Campaign data is unchanged, so it is intentionally not in the changed-file archive.
for rel in ("Assets/CyberCity/Resources/campaign.json", "Assets/CyberCity/Resources/campaign_hash.txt"):
    destination = fixture / rel
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(root / rel, destination)
print(f"PASS: packaged and reopened {len(files)} modified paths; ZIP CRC verified; diff written; rollback fixture ready.")
