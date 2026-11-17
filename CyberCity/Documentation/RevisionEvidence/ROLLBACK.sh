#!/usr/bin/env bash
set -euo pipefail
if [ "$#" -ne 1 ]; then
    printf 'Usage: %s ABSOLUTE_PROJECT_OR_FIXTURE_PATH\n' "$0" >&2
    exit 2
fi
HERE="$(cd -- "$(dirname -- "$0")" && pwd)"
exec python "$HERE/rollback.py" "$1"
