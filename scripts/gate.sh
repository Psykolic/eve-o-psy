#!/usr/bin/env bash
# Gate unico do EVE-O Preview: o que o agente autonomo e a CI Linux executam.
# Contrato: sai 0 somente se a solucao compila e os testes de nucleo passam.
set -euo pipefail

cd "$(dirname "$0")/.."

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

echo "== restore =="
dotnet restore src/EVE-O-Preview.sln

echo "== build (Release) =="
dotnet build src/EVE-O-Preview.sln -c Release --no-restore --nologo

echo "== testes de nucleo (net8.0, sem WinForms) =="
dotnet test src/EveO.Core.Tests/EveO.Core.Tests.csproj -c Release --no-build --nologo

echo "== gate ok =="
