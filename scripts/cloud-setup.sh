#!/usr/bin/env bash
# Prepares a fresh cloud/CI-like container for working on Clario Shop:
# a local PostgreSQL (postgres/postgres) for integration tests and the dotnet-ef tool for migrations.
# Safe to run more than once.
set -euo pipefail

if command -v service >/dev/null 2>&1 && [ -d /etc/postgresql ]; then
  service postgresql start >/dev/null
  for _ in $(seq 1 30); do
    su postgres -c "pg_isready -q" && break
    sleep 1
  done
  su postgres -c "psql -q -c \"ALTER USER postgres PASSWORD 'postgres';\""
  # Every test tenant is its own database with its own connection pool; the default 100 connections run out.
  if [ "$(su postgres -c "psql -tAq -c 'SHOW max_connections;'")" -lt 500 ]; then
    su postgres -c "psql -q -c 'ALTER SYSTEM SET max_connections = 500;'"
    service postgresql restart >/dev/null
    for _ in $(seq 1 30); do
      su postgres -c "pg_isready -q" && break
      sleep 1
    done
  fi
  echo "PostgreSQL ready: export CLARIOSHOP_TEST_POSTGRES='Host=localhost;Username=postgres;Password=postgres'"
else
  echo "No local PostgreSQL found; integration tests will use Testcontainers (needs Docker)."
fi

if ! dotnet ef --version >/dev/null 2>&1 && ! "$HOME/.dotnet/tools/dotnet-ef" --version >/dev/null 2>&1; then
  dotnet tool install -g dotnet-ef --version "10.0.*"
fi
echo "dotnet-ef ready (add \$HOME/.dotnet/tools to PATH if 'dotnet ef' is not found)."
