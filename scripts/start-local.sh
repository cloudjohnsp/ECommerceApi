#!/usr/bin/env bash
set -euo pipefail

no_build=false
skip_smoke_test=false
while (($#)); do
  case "$1" in
    --no-build) no_build=true; shift ;;
    --skip-smoke-test) skip_smoke_test=true; shift ;;
    -h|--help) echo "Usage: $0 [--no-build] [--skip-smoke-test]"; exit 0 ;;
    *) echo "Unknown option: $1" >&2; exit 2 ;;
  esac
done

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "$script_dir/.." && pwd)"
environment_path="$repository_root/.env"
compose_path="$repository_root/docker-compose.yml"

if command -v docker >/dev/null 2>&1; then docker_bin=docker; elif command -v docker.exe >/dev/null 2>&1; then docker_bin=docker.exe; else echo "Docker was not found. Install or start Docker Desktop and try again." >&2; exit 1; fi
bash "$script_dir/initialize-dev-env.sh"

compose=("$docker_bin" compose --env-file "$environment_path" --file "$compose_path" up --detach --wait --wait-timeout 300 --force-recreate)
[[ "$no_build" == false ]] && compose+=(--build)
"${compose[@]}"

[[ "$skip_smoke_test" == true ]] || bash "$script_dir/smoke-test-local.sh"
echo "ECommerce local services started successfully."
