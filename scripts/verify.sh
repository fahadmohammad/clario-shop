#!/usr/bin/env bash
# Runs the same checks as CI (.github/workflows/ci.yml): Release build with warnings as errors,
# format check, then every test project. Uses the local PostgreSQL from cloud-setup.sh when it is running.
set -euo pipefail
cd "$(dirname "$0")/.."

if [ -z "${CLARIOSHOP_TEST_POSTGRES:-}" ] && command -v pg_isready >/dev/null 2>&1 && pg_isready -q -h localhost; then
  export CLARIOSHOP_TEST_POSTGRES="Host=localhost;Username=postgres;Password=postgres"
fi

dotnet restore ClarioShop.slnx
dotnet build ClarioShop.slnx --configuration Release --no-restore
dotnet format ClarioShop.slnx --verify-no-changes --no-restore --exclude "**/Migrations/**"
dotnet test --solution ClarioShop.slnx --configuration Release --no-build
