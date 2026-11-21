#!/usr/bin/env bash
# Benchmarks dos caminhos quentes do Core (rodam em Linux, sem DWM/WPF).
#
#   bash scripts/bench.sh            # mede e compara com benchmarks/baseline.json
#   bash scripts/bench.sh --update   # mede e grava novo baseline
#
# Sai 1 quando algum benchmark piora mais que BENCH_THRESHOLD (padrão 20%).
set -uo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"
PROJ="$ROOT/benchmarks/EveO.Benchmarks/EveO.Benchmarks.csproj"
ARTEFATOS="$ROOT/BenchmarkDotNet.Artifacts"
BASELINE="$ROOT/benchmarks/baseline.json"
LIMITE="${BENCH_THRESHOLD:-20}"

MODO="--check"
[ "${1:-}" = "--update" ] && MODO="--update"

[ -f "$PROJ" ] || { echo "projeto de benchmark ausente: $PROJ"; exit 2; }

# O BenchmarkDotNet grava no diretório de trabalho atual: fixar na raiz do repo
# torna o caminho dos artefatos previsível para o comparador.
cd "$ROOT" || exit 2
rm -rf "$ARTEFATOS"

export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
echo "== medindo (job short) =="
dotnet run -c Release --project "$PROJ" -- \
  --job short --exporters json --filter '*' 2>&1 \
  | grep -E '\| Method|^\s*\| |Total time|executed benchmarks|error' | head -30

echo
echo "== comparando com o baseline =="
EXTRA=()
[ "$MODO" = "--update" ] && EXTRA=(--update)
python3 "$ROOT/benchmarks/compare.py" \
  --results "$ARTEFATOS" \
  --baseline "$BASELINE" \
  --threshold "$LIMITE" "${EXTRA[@]+"${EXTRA[@]}"}"
