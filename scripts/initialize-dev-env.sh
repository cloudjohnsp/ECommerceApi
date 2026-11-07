#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "$script_dir/.." && pwd)"
template_path="$repository_root/.env.example"
environment_path="$repository_root/.env"

[[ -f "$template_path" ]] || { echo "Environment template was not found at '$template_path'." >&2; exit 1; }

generate_secret() {
  local byte_count="$1"
  if command -v openssl >/dev/null 2>&1; then
    openssl rand -base64 "$byte_count" | tr -d '=\n' | tr '+/' '-_'
  elif command -v python3 >/dev/null 2>&1; then
    python3 -c 'import secrets, sys; print(secrets.token_urlsafe(int(sys.argv[1])))' "$byte_count"
  else
    echo "OpenSSL or Python 3 is required to generate local secrets." >&2
    return 1
  fi
}

if [[ ! -f "$environment_path" ]]; then
  cp "$template_path" "$environment_path"
fi

keys=(POSTGRES_PASSWORD RABBITMQ_PASSWORD JWT_SECRET_KEY PAYMENT_GATEWAY_WEBHOOK_SECRET GRAFANA_ADMIN_PASSWORD)
sizes=(32 32 48 32 32)
initialized=()

for index in "${!keys[@]}"; do
  key="${keys[$index]}"
  value="$(awk -F= -v key="$key" '$1 == key { sub(/^[^=]*=/, ""); print; exit }' "$environment_path")"
  [[ -n "${value//[[:space:]]/}" ]] && continue

  secret="$(generate_secret "${sizes[$index]}")"
  temporary_file="$(mktemp "${environment_path}.XXXXXX")"
  trap 'rm -f "$temporary_file"' EXIT
  awk -v key="$key" -v secret="$secret" '
    BEGIN { found = 0 }
    $0 ~ "^" key "=" { print key "=" secret; found = 1; next }
    { print }
    END { if (!found) print key "=" secret }
  ' "$environment_path" > "$temporary_file"
  mv "$temporary_file" "$environment_path"
  trap - EXIT
  initialized+=("$key")
done

if ((${#initialized[@]} == 0)); then
  echo "The local .env already contains every required secret. No values were changed."
else
  echo "Initialized local .env values for: ${initialized[*]// /, }."
fi
echo "Secrets were not printed and existing non-empty values were preserved."
