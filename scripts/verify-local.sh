#!/usr/bin/env bash
set -euo pipefail

configuration=Release
skip_image=false
while (($#)); do
  case "$1" in
    --configuration) configuration="$2"; shift 2 ;;
    --skip-image) skip_image=true; shift ;;
    -h|--help) echo "Usage: $0 [--configuration Debug|Release] [--skip-image]"; exit 0 ;;
    *) echo "Unknown option: $1" >&2; exit 2 ;;
  esac
done
[[ "$configuration" == Debug || "$configuration" == Release ]] || { echo "Configuration must be Debug or Release." >&2; exit 2; }

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd -- "$script_dir/.." && pwd)"
temporary_environment_path="$(mktemp "${TMPDIR:-/tmp}/ecommerce-api-gate.XXXXXX.env")"
trap 'rm -f -- "$temporary_environment_path"' EXIT
if command -v dotnet >/dev/null 2>&1; then dotnet_bin=dotnet; elif command -v dotnet.exe >/dev/null 2>&1; then dotnet_bin=dotnet.exe; else echo "dotnet was not found." >&2; exit 1; fi
if command -v docker >/dev/null 2>&1; then docker_bin=docker; elif command -v docker.exe >/dev/null 2>&1; then docker_bin=docker.exe; else echo "Docker was not found. Install or start Docker Desktop and try again." >&2; exit 1; fi

invoke_gate_step() { echo "==> $1"; shift; "$@"; }
cd "$root"
invoke_gate_step "Restore API" "$dotnet_bin" restore ECommerceApi.slnx
invoke_gate_step "Verify API formatting" "$dotnet_bin" format ECommerceApi.slnx --verify-no-changes --no-restore
invoke_gate_step "Build API" "$dotnet_bin" build ECommerceApi.slnx --configuration "$configuration" --no-restore
invoke_gate_step "Test API with PostgreSQL" env RUN_POSTGRES_INTEGRATION_TESTS=true "$dotnet_bin" test ECommerceApi.slnx --configuration "$configuration" --no-build --no-restore
invoke_gate_step "Enforce API coverage" bash "$script_dir/verify-unit-coverage.sh" --configuration "$configuration"

secret_suffix="$(date +%s%N)-$$"
cat > "$temporary_environment_path" <<EOF
POSTGRES_PASSWORD=postgres-$secret_suffix
JWT_SECRET_KEY=jwt-$secret_suffix
PAYMENT_GATEWAY_WEBHOOK_SECRET=webhook-$secret_suffix
RABBITMQ_PASSWORD=rabbit-$secret_suffix
GRAFANA_ADMIN_PASSWORD=grafana-$secret_suffix
EOF
invoke_gate_step "Validate Docker Compose configuration" "$docker_bin" compose --env-file "$temporary_environment_path" --file docker-compose.yml config --quiet

if [[ "$skip_image" == false ]]; then
  revision="$(git rev-parse HEAD)"
  tag="ecommerce-api:sha-$revision"
  [[ -z "$(git status --porcelain)" ]] || tag+="-dirty"
  invoke_gate_step "Build API image" "$docker_bin" build --label "org.opencontainers.image.revision=$revision" --tag "$tag" .
fi
echo "API local gate passed."
