#!/usr/bin/env bash
# Verifies the generator against the checked-in fixture package: no registry access, no Unity.
#
#   tools/CommandRefGen/tests/run.sh            # compare against the golden file
#   tools/CommandRefGen/tests/run.sh --update   # rewrite the golden file from the current output
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project="$here/.."
fixture="$here/fixture-package"
golden="$here/expected/editor-commands.md"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

run() {
  dotnet run --project "$project" -- \
    --source-dir "$fixture" \
    --annotations "$project/annotations.json" \
    "$@"
}

echo "== generating from the fixture package =="
run --stdout >"$work/actual.md" 2>"$work/stderr.txt" || {
  cat "$work/stderr.txt" >&2
  echo "FAIL: the generator exited non-zero" >&2
  exit 1
}

if [[ "${1:-}" == "--update" ]]; then
  mkdir -p "$(dirname "$golden")"
  cp "$work/actual.md" "$golden"
  echo "updated $golden"
  exit 0
fi

failures=0
fail() { echo "FAIL: $*" >&2; failures=$((failures + 1)); }

echo "== comparing against the golden file =="
if ! diff -u "$golden" "$work/actual.md"; then
  fail "output does not match $golden (rerun with --update if the change is intended)"
fi

# Warnings the fixture is built to provoke; silence in any of these means a check stopped working.
echo "== checking the expected diagnostics =="
expect_warning() {
  grep -qF "$1" "$work/stderr.txt" || fail "expected a warning containing: $1"
}
expect_warning "'screenshot' has parameter(s) without [CliArg]: cancellation"
expect_warning "category directory 'Widgets' has no mapped section title"

# Test-assembly fixtures must never reach the document.
for fixture_command in log_editor test_types test_structured; do
  grep -qF "### $fixture_command" "$work/actual.md" \
    && fail "$fixture_command came from the excluded Tests/ assembly"
done

# The same finding must not be reported twice just because a file is parsed once per
# preprocessor pass.
duplicates="$(grep '^warning:' "$work/stderr.txt" | sort | uniq -d || true)"
[[ -z "$duplicates" ]] || fail "duplicate warnings:"$'\n'"$duplicates"

echo "== checking --check on an up-to-date file =="
cp "$work/actual.md" "$work/output.md"
run --output "$work/output.md" --check >/dev/null 2>&1 \
  || fail "--check reported an up-to-date file as stale"

echo "== checking --check on a stale file =="
printf '### gone_command\nRemoved upstream.\n- *(no arguments)*\n' >>"$work/output.md"
set +e
run --output "$work/output.md" --check >/dev/null 2>"$work/check.txt"
status=$?
set -e
[[ $status -eq 3 ]] || fail "--check on a stale file exited $status, expected 3"
grep -qF "gone_command" "$work/check.txt" || fail "--check did not report the stale command"

if [[ $failures -eq 0 ]]; then
  echo "PASS"
else
  echo "$failures check(s) failed" >&2
  exit 1
fi
