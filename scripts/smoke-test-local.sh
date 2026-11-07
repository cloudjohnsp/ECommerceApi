#!/usr/bin/env bash
set -euo pipefail

environment_path=""
project_name=""
api_port=8080
payment_port=5002
mailpit_port=8025
prometheus_port=9090
grafana_port=3001

usage() {
  echo "Usage: $0 [--environment-path PATH] [--project-name NAME] [--api-port PORT] [--payment-port PORT] [--mailpit-port PORT] [--prometheus-port PORT] [--grafana-port PORT]" >&2
}

while (($#)); do
  case "$1" in
    --environment-path) environment_path="$2"; shift 2 ;;
    --project-name) project_name="$2"; shift 2 ;;
    --api-port) api_port="$2"; shift 2 ;;
    --payment-port) payment_port="$2"; shift 2 ;;
    --mailpit-port) mailpit_port="$2"; shift 2 ;;
    --prometheus-port) prometheus_port="$2"; shift 2 ;;
    --grafana-port) grafana_port="$2"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) usage; exit 2 ;;
  esac
done

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "$script_dir/.." && pwd)"
environment_path="${environment_path:-$repository_root/.env}"
compose_path="$repository_root/docker-compose.yml"
expected_services=(postgres-db rabbitmq redis azurite mailpit ecommerce-payment ecommerce-api ecommerce-worker prometheus grafana)

command -v docker >/dev/null 2>&1 || { echo "Docker was not found. Install or start Docker Desktop and try again." >&2; exit 1; }
command -v curl >/dev/null 2>&1 || { echo "curl is required for the smoke test." >&2; exit 1; }
if command -v python3 >/dev/null 2>&1; then python_bin=python3; elif command -v python >/dev/null 2>&1; then python_bin=python; else echo "Python is required to validate payment responses." >&2; exit 1; fi
[[ -f "$environment_path" ]] || { echo "The local .env was not found. Run scripts/start-local.sh first." >&2; exit 1; }

compose=(docker compose --env-file "$environment_path" --file "$compose_path")
[[ -n "$project_name" ]] && compose=(docker compose --project-name "$project_name" --env-file "$environment_path" --file "$compose_path")

invoke_compose() { "${compose[@]}" "$@"; }
assert_http_success() { curl --fail --silent --show-error --max-time 10 "$1" >/dev/null; }

configured_services="$(invoke_compose config --services)"
for service in "${expected_services[@]}"; do
  grep -Fxq "$service" <<<"$configured_services" || { echo "Required Compose service '$service' is missing." >&2; exit 1; }
  container_id="$(invoke_compose ps --quiet "$service" | head -n 1)"
  [[ -n "$container_id" ]] || { echo "Required Compose service '$service' is not running." >&2; exit 1; }
  state="$(docker inspect --format '{{.State.Running}} {{if .State.Health}}{{.State.Health.Status}}{{end}}' "$container_id")"
  [[ "$state" == "true healthy" ]] || { echo "Compose service '$service' is not healthy (state: $state)." >&2; exit 1; }
done

assert_http_success "http://127.0.0.1:$api_port/api/health/ready"
assert_http_success "http://127.0.0.1:$payment_port/health"
assert_http_success "http://127.0.0.1:$mailpit_port/livez"
assert_http_success "http://127.0.0.1:$prometheus_port/-/ready"
assert_http_success "http://127.0.0.1:$grafana_port/api/health"
invoke_compose exec -T ecommerce-api curl --fail --silent --show-error http://ecommerce-payment:5000/health >/dev/null

smoke_reference="smoke-$($python_bin -c 'import uuid; print(uuid.uuid4().hex)')"
payment_body="$(printf '{"amount":1.00,"currency":"BRL","reference":"%s","correlationId":"%s"}' "$smoke_reference" "$smoke_reference")"
payment="$(curl --fail --silent --show-error --max-time 10 -X POST "http://127.0.0.1:$payment_port/payments" -H 'Content-Type: application/json' -H "Idempotency-Key: $smoke_reference" --data "$payment_body")"
payment_id="$(printf '%s' "$payment" | "$python_bin" -c 'import json,sys; value=json.load(sys.stdin); assert value.get("status") == "pending" and value.get("id"); print(value["id"])')" || { echo "Payment smoke flow did not create a pending payment." >&2; exit 1; }
approved_payment="$(curl --fail --silent --show-error --max-time 10 -X POST "http://127.0.0.1:$payment_port/payments/$payment_id/approve" -H 'Content-Type: application/json' --data '{}')"
printf '%s' "$approved_payment" | "$python_bin" -c 'import json,sys; value=json.load(sys.stdin); expected=sys.argv[1]; assert value.get("id") == expected and value.get("status") == "approved"' "$payment_id" || { echo "Payment smoke flow did not approve the created payment." >&2; exit 1; }

queue_output="$(invoke_compose exec -T rabbitmq rabbitmqctl list_queues --quiet name)"
for queue_name in ecommerce.worker.orders ecommerce.worker.orders.retry ecommerce.worker.orders.dead; do
  grep -Fxq "$queue_name" <<<"$queue_output" || { echo "Worker queue '$queue_name' was not declared." >&2; exit 1; }
done

echo "Unified local environment smoke test passed."
