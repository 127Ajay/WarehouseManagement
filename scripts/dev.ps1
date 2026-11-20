# One command local dev on Windows: start containers, wait for PostgreSQL, migrate, run the API with hot reload.
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")

docker compose up -d

Write-Host "Waiting for PostgreSQL..."
for ($i = 0; $i -lt 30; $i++) {
    docker compose exec -T postgres pg_isready -U postgres *> $null
    if ($LASTEXITCODE -eq 0) { break }
    Start-Sleep -Seconds 2
}

dotnet run --project src/Hosts/Wms.Host.Migrator
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet watch --project src/Hosts/Wms.Host.AppApi
