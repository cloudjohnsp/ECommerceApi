#!/usr/bin/env bash
set -euo pipefail

configuration=Debug
while (($#)); do
  case "$1" in
    --configuration) configuration="$2"; shift 2 ;;
    -h|--help) echo "Usage: $0 [--configuration Debug|Release]"; exit 0 ;;
    *) echo "Unknown option: $1" >&2; exit 2 ;;
  esac
done
[[ "$configuration" == Debug || "$configuration" == Release ]] || { echo "Configuration must be Debug or Release." >&2; exit 2; }

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
solution_root="$(cd -- "$script_dir/.." && pwd)"
minimum_line_coverage=80.0
if command -v python3 >/dev/null 2>&1; then python_bin=python3; elif command -v python >/dev/null 2>&1; then python_bin=python; else echo "Python is required to read Cobertura reports." >&2; exit 1; fi

targets=("ECommerce.Domain.Tests/ECommerce.Domain.Tests.csproj:ECommerce.Domain" "ECommerce.Application.Tests/ECommerce.Application.Tests.csproj:ECommerce.Application")
for target in "${targets[@]}"; do
  project="${target%%:*}"
  assembly="${target##*:}"
  results_directory="$(mktemp -d "${TMPDIR:-/tmp}/ecommerce-coverage.XXXXXX")"
  cleanup() { rm -rf -- "$results_directory"; }
  trap cleanup EXIT
  dotnet test "$solution_root/$project" --configuration "$configuration" --no-build --no-restore --collect:'XPlat Code Coverage' --results-directory "$results_directory"
  report="$(find "$results_directory" -name coverage.cobertura.xml -print -quit)"
  [[ -n "$report" ]] || { echo "Coverage report was not generated for $project." >&2; exit 1; }
  line_rate="$($python_bin - "$report" "$assembly" <<'PY'
import sys
import xml.etree.ElementTree as ET
root = ET.parse(sys.argv[1]).getroot()
for package in root.findall('.//package'):
    if package.get('name') == sys.argv[2]:
        print(float(package.get('line-rate', '0')) * 100)
        break
else:
    raise SystemExit(f"Assembly {sys.argv[2]} was not found in the coverage report.")
PY
)"
  "$python_bin" - "$assembly" "$line_rate" "$minimum_line_coverage" <<'PY'
import sys
assembly, actual, minimum = sys.argv[1], float(sys.argv[2]), float(sys.argv[3])
print(f"{assembly}: {actual:.2f}% line coverage")
if actual <= minimum:
    raise SystemExit(f"{assembly} line coverage must be greater than {minimum:.2f}%, but was {actual:.2f}%.")
PY
  rm -rf -- "$results_directory"
  trap - EXIT
done
