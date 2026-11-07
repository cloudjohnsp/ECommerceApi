#!/usr/bin/env bash
set -euo pipefail

worker_path=""
payment_path=""
payment_python_path=""
skip_payment_install=false
allow_dirty=false
skip_project_gates=false
skip_image_build=false
skip_acceptance=false
skip_full_compose_smoke=false

usage() {
  cat <<'EOF'
Usage: verify-multi-repository.sh [options]
  --worker-path PATH              Path to ECommerceWorker.
  --payment-path PATH             Path to ECommercePayment.
  --payment-python-path PATH      Python executable for the Payment gate.
  --skip-payment-install          Reuse an already prepared Payment environment.
  --allow-dirty                   Allow uncommitted sources, tagging images -dirty.
  --skip-project-gates            Skip local project gates (diagnostic only).
  --skip-image-build              Reuse existing images (diagnostic only).
  --skip-acceptance               Skip cross-service acceptance (diagnostic only).
  --skip-full-compose-smoke       Skip full Compose smoke (diagnostic only).
EOF
}

while (($#)); do
  case "$1" in
    --worker-path) worker_path="$2"; shift 2 ;;
    --payment-path) payment_path="$2"; shift 2 ;;
    --payment-python-path) payment_python_path="$2"; shift 2 ;;
    --skip-payment-install) skip_payment_install=true; shift ;;
    --allow-dirty) allow_dirty=true; shift ;;
    --skip-project-gates) skip_project_gates=true; shift ;;
    --skip-image-build) skip_image_build=true; shift ;;
    --skip-acceptance) skip_acceptance=true; shift ;;
    --skip-full-compose-smoke) skip_full_compose_smoke=true; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown option: $1" >&2; usage; exit 2 ;;
  esac
done

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
api_root="$(cd -- "$script_dir/.." && pwd)"
worker_path="${worker_path:-$api_root/../../../personal-projects/ECommerceWorker}"
payment_path="${payment_path:-$api_root/../../../personal-projects/ECommercePayment}"
worker_path="$(cd -- "$worker_path" && pwd)"
payment_path="$(cd -- "$payment_path" && pwd)"
revision_lock_path="$api_root/integration/revisions.json"
artifact_root="$api_root/TestResults/coordinated/$(date -u +%Y%m%d-%H%M%S)"
temporary_paths=()

if command -v docker >/dev/null 2>&1; then docker_bin=docker; elif command -v docker.exe >/dev/null 2>&1; then docker_bin=docker.exe; else echo "Docker is required for the coordinated gate." >&2; exit 1; fi
if command -v dotnet >/dev/null 2>&1; then dotnet_bin=dotnet; elif command -v dotnet.exe >/dev/null 2>&1; then dotnet_bin=dotnet.exe; else echo "dotnet is required for the coordinated gate." >&2; exit 1; fi
if command -v python3 >/dev/null 2>&1; then python_bin=python3; elif command -v python >/dev/null 2>&1; then python_bin=python; else echo "Python is required for the coordinated gate." >&2; exit 1; fi
to_docker_path() {
  if [[ "$docker_bin" == *.exe ]] && command -v wslpath >/dev/null 2>&1; then wslpath -w "$1"; else printf '%s\n' "$1"; fi
}
[[ -d "$worker_path/.git" ]] || { echo "Worker repository was not found at '$worker_path'." >&2; exit 1; }
[[ -d "$payment_path/.git" ]] || { echo "Payment repository was not found at '$payment_path'." >&2; exit 1; }
mkdir -p "$artifact_root"

cleanup() {
  local path
  for path in "${temporary_paths[@]:-}"; do
    [[ -n "$path" && -e "$path" ]] || continue
    case "$(cd -- "$(dirname -- "$path")" && pwd)/$(basename -- "$path")" in
      "${TMPDIR:-/tmp}"/*|"$api_root"/.ecommerce-gate-*) rm -rf -- "$path" ;;
      *) echo "Refusing to remove non-temporary path '$path'." >&2 ;;
    esac
  done
}
trap cleanup EXIT

lock_values="$("$python_bin" - "$revision_lock_path" <<'PY'
import json, sys
with open(sys.argv[1], encoding='utf-8') as source:
    lock = json.load(source)
if lock.get('schemaVersion') != 1:
    raise SystemExit('Unsupported integration revision schema.')
for key in ('api', 'worker', 'payment'):
    print(lock[key]['revision'])
PY
)"
mapfile -t lock_revisions <<< "$lock_values"
api_expected="${lock_revisions[0]}"
worker_expected="${lock_revisions[1]}"
payment_expected="${lock_revisions[2]}"

get_repository_state() {
  local name="$1" path="$2" expected="$3" revision dirty
  revision="$(git -C "$path" rev-parse HEAD)"
  [[ "$expected" == self || "$revision" == "$expected" ]] || { echo "$name is at '$revision', but integration/revisions.json requires '$expected'." >&2; return 1; }
  dirty=false; [[ -z "$(git -C "$path" status --porcelain)" ]] || dirty=true
  [[ "$dirty" == false || "$allow_dirty" == true ]] || { echo "$name has uncommitted changes. Commit them or use --allow-dirty for a pre-commit validation." >&2; return 1; }
  printf '%s\t%s\n' "$revision" "$dirty"
}
IFS=$'\t' read -r api_revision api_dirty < <(get_repository_state API "$api_root" "$api_expected")
IFS=$'\t' read -r worker_revision worker_dirty < <(get_repository_state Worker "$worker_path" "$worker_expected")
IFS=$'\t' read -r payment_revision payment_dirty < <(get_repository_state Payment "$payment_path" "$payment_expected")

run_in_directory() { local name="$1" directory="$2"; shift 2; echo "==> $name"; (cd "$directory" && "$@"); }
run_external_gate() {
  local repository="$1"; shift
  if [[ -f "$repository/scripts/verify-local.sh" ]]; then bash "$repository/scripts/verify-local.sh" "$@"; return; fi
  command -v pwsh >/dev/null 2>&1 || { echo "'$repository' has no scripts/verify-local.sh and pwsh is unavailable for its legacy gate." >&2; return 1; }
  local arguments=()
  for argument in "$@"; do
    case "$argument" in
      --configuration) arguments+=(-Configuration) ;;
      Release|Debug) arguments+=("$argument") ;;
      --skip-image) arguments+=(-SkipImage) ;;
      --skip-install) arguments+=(-SkipInstall) ;;
      *) arguments+=("$argument") ;;
    esac
  done
  pwsh -NoProfile -File "$repository/scripts/verify-local.ps1" "${arguments[@]}"
}

if [[ "$skip_project_gates" == false ]]; then
  run_in_directory "Restore acceptance tests" "$api_root" "$dotnet_bin" restore ECommerceAcceptance.slnx
  run_in_directory "Verify acceptance formatting" "$api_root" "$dotnet_bin" format ECommerceAcceptance.slnx --verify-no-changes --no-restore
  bash "$script_dir/verify-local.sh" --configuration Release --skip-image
  run_external_gate "$worker_path" --configuration Release --skip-image
  if [[ -z "$payment_python_path" ]]; then
    venv="$(mktemp -d "${TMPDIR:-/tmp}/ecommerce-payment-gate.XXXXXX")"
    temporary_paths+=("$venv")
    run_in_directory "Create isolated Payment environment" "$payment_path" "$python_bin" -m venv "$venv"
    payment_python_path="$venv/bin/python"
    [[ -x "$payment_python_path" ]] || payment_python_path="$venv/Scripts/python.exe"
  fi
  payment_arguments=(-Python "$payment_python_path")
  [[ "$skip_payment_install" == true ]] && payment_arguments+=(-SkipInstall)
  payment_arguments+=(-SkipImage)
  if [[ -f "$payment_path/scripts/verify-local.sh" ]]; then
    payment_bash_arguments=(--python "$payment_python_path" --skip-image)
    [[ "$skip_payment_install" == true ]] && payment_bash_arguments+=(--skip-install)
    bash "$payment_path/scripts/verify-local.sh" "${payment_bash_arguments[@]}"
  else
    command -v pwsh >/dev/null 2>&1 || { echo "Payment has no Bash gate and pwsh is unavailable." >&2; exit 1; }
    pwsh -NoProfile -File "$payment_path/scripts/verify-local.ps1" "${payment_arguments[@]}"
  fi
fi

api_tag="ecommerce-api:sha-$api_revision"; [[ "$api_dirty" == false ]] || api_tag+="-dirty"
worker_tag="ecommerce-worker:sha-$worker_revision"; [[ "$worker_dirty" == false ]] || worker_tag+="-dirty"
payment_tag="ecommerce-payment:sha-$payment_revision"; [[ "$payment_dirty" == false ]] || payment_tag+="-dirty"
if [[ "$skip_image_build" == false ]]; then
  run_in_directory "Build API image" "$api_root" "$docker_bin" build --label "org.opencontainers.image.revision=$api_revision" --tag "$api_tag" .
  run_in_directory "Build Worker image" "$worker_path" "$docker_bin" build --label "org.opencontainers.image.revision=$worker_revision" --tag "$worker_tag" .
  run_in_directory "Build Payment image" "$payment_path" "$docker_bin" build --label "org.opencontainers.image.revision=$payment_revision" --tag "$payment_tag" .
fi
assert_image_revision() { [[ "$("$docker_bin" image inspect "$1" --format '{{ index .Config.Labels "org.opencontainers.image.revision" }}')" == "$2" ]] || { echo "Image '$1' does not have expected revision '$2'." >&2; exit 1; }; }
assert_image_revision "$api_tag" "$api_revision"; assert_image_revision "$worker_tag" "$worker_revision"; assert_image_revision "$payment_tag" "$payment_revision"

API_REVISION="$api_revision" WORKER_REVISION="$worker_revision" PAYMENT_REVISION="$payment_revision" API_TAG="$api_tag" WORKER_TAG="$worker_tag" PAYMENT_TAG="$payment_tag" DOCKER_BIN="$docker_bin" \
  "$python_bin" - "$artifact_root/manifest.json" <<'PY'
import json, os, subprocess, sys
images = [('api', 'API'), ('worker', 'WORKER'), ('payment', 'PAYMENT')]
value = {'generatedAtUtc': __import__('datetime').datetime.now(__import__('datetime').timezone.utc).isoformat(), 'repositories': [], 'images': []}
for service, prefix in images:
    revision, tag = os.environ[f'{prefix}_REVISION'], os.environ[f'{prefix}_TAG']
    value['repositories'].append({'Name': prefix.title(), 'Revision': revision})
    value['images'].append({'service': service, 'tag': tag, 'id': subprocess.check_output([os.environ['DOCKER_BIN'], 'image', 'inspect', tag, '--format', '{{.Id}}'], text=True).strip()})
json.dump(value, open(sys.argv[1], 'w', encoding='utf-8'), indent=2)
PY

export ECOMMERCE_API_IMAGE="$api_tag" ECOMMERCE_WORKER_IMAGE="$worker_tag" ECOMMERCE_PAYMENT_IMAGE="$payment_tag"
export ECOMMERCE_WORKER_CONTEXT="$(to_docker_path "$worker_path")" ECOMMERCE_PAYMENT_CONTEXT="$(to_docker_path "$payment_path")"
if [[ "$skip_acceptance" == false ]]; then
  run_in_directory "Run cross-service acceptance gate" "$api_root" env RUN_CROSS_SERVICE_ACCEPTANCE_TESTS=true ECOMMERCE_ACCEPTANCE_REUSE_IMAGES=true "$dotnet_bin" test ECommerceAcceptance.slnx --configuration Release --no-restore
fi

if [[ "$skip_full_compose_smoke" == false ]]; then
  project_name="ecommerce-gate-$("$python_bin" -c 'import uuid; print(uuid.uuid4().hex[:12])')"
  gate_environment_path="$(mktemp "$api_root/.$project_name.XXXXXX.env")"
  temporary_paths+=("$gate_environment_path")
  get_free_port() { "$python_bin" -c 'import socket; s=socket.socket(); s.bind(("127.0.0.1",0)); print(s.getsockname()[1]); s.close()'; }
  api_port="$(get_free_port)"; payment_port="$(get_free_port)"; mailpit_port="$(get_free_port)"; prometheus_port="$(get_free_port)"; grafana_port="$(get_free_port)"; rabbit_port="$(get_free_port)"; azurite_port="$(get_free_port)"
  secret_suffix="$("$python_bin" -c 'import uuid; print(uuid.uuid4().hex)')"
  cat > "$gate_environment_path" <<EOF
ECOMMERCE_API_IMAGE=$api_tag
ECOMMERCE_WORKER_IMAGE=$worker_tag
ECOMMERCE_PAYMENT_IMAGE=$payment_tag
ECOMMERCE_WORKER_CONTEXT=$(to_docker_path "$worker_path")
ECOMMERCE_PAYMENT_CONTEXT=$(to_docker_path "$payment_path")
POSTGRES_USER=gate
POSTGRES_PASSWORD=postgres-$secret_suffix
JWT_SECRET_KEY=jwt-$secret_suffix
PAYMENT_GATEWAY_WEBHOOK_SECRET=webhook-$secret_suffix
RABBITMQ_USER=gate
RABBITMQ_PASSWORD=rabbit-$secret_suffix
GRAFANA_ADMIN_USER=gate
GRAFANA_ADMIN_PASSWORD=grafana-$secret_suffix
ECOMMERCE_API_PORT=$api_port
PAYMENT_GATEWAY_PORT=$payment_port
MAILPIT_PORT=$mailpit_port
PROMETHEUS_PORT=$prometheus_port
GRAFANA_PORT=$grafana_port
RABBITMQ_MANAGEMENT_PORT=$rabbit_port
AZURITE_PORT=$azurite_port
EOF
  compose=("$docker_bin" compose --project-name "$project_name" --env-file "$(to_docker_path "$gate_environment_path")" --file "$(to_docker_path "$api_root/docker-compose.yml")")
  trap '"${compose[@]}" ps --all > "$artifact_root/compose-ps.txt" 2>&1 || true; "${compose[@]}" logs --no-color --timestamps > "$artifact_root/compose.log" 2>&1 || true; "${compose[@]}" down --volumes --remove-orphans --timeout 10 >/dev/null 2>&1 || true; cleanup' EXIT
  echo "==> Start full coordinated Compose"
  "${compose[@]}" up --detach --no-build --wait --wait-timeout 300
  bash "$script_dir/smoke-test-local.sh" --environment-path "$gate_environment_path" --project-name "$project_name" --api-port "$api_port" --payment-port "$payment_port" --mailpit-port "$mailpit_port" --prometheus-port "$prometheus_port" --grafana-port "$grafana_port"
  "${compose[@]}" ps --all > "$artifact_root/compose-ps.txt"
  "${compose[@]}" logs --no-color --timestamps > "$artifact_root/compose.log"
  "${compose[@]}" down --volumes --remove-orphans --timeout 10 >/dev/null
  trap cleanup EXIT
fi
echo "Coordinated multi-repository gate passed. Artifacts: $artifact_root"
