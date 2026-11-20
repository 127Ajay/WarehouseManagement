#!/usr/bin/env bash
# One command local dev: start containers, wait for PostgreSQL, migrate, run the API with hot reload.
set -euo pipefail
cd "$(dirname "$0")/.."

docker compose up -d

echo "Waiting for PostgreSQL..."
for _ in $(seq 1 30); do
  if docker compose exec -T postgres pg_isready -U postgres >/dev/null 2>&1; then break; fi
  sleep 2
done

dotnet run --project src/Hosts/Wms.Host.Migrator
dotnet watch --project src/Hosts/Wms.Host.AppApi
