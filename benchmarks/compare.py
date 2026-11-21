#!/usr/bin/env python3
"""Compara o resultado do BenchmarkDotNet com o baseline commitado.

Uso:
  compare.py --results <pasta de resultados .json> --baseline benchmarks/baseline.json \
             [--threshold 10] [--update]

Sai 1 quando algum benchmark piora mais que o limite (regressão).

Formatos aceitos (o BenchmarkDotNet grava os dois):
  - lista de relatórios;
  - objeto {"Title": ..., "Benchmarks": [...]} (o arquivo *full-compressed*),
    onde cada item traz Type/Method no nível raiz.
"""
import argparse
import json
import pathlib
import sys


def extrair(itens):
    saida = []
    if isinstance(itens, dict):
        for chave in ("Benchmarks", "Reports"):
            if isinstance(itens.get(chave), list):
                saida.extend(extrair(itens[chave]))
        return saida
    if not isinstance(itens, list):
        return saida
    for item in itens:
        if not isinstance(item, dict):
            continue
        if isinstance(item.get("Benchmarks"), list):
            saida.extend(extrair(item["Benchmarks"]))
        else:
            saida.append(item)
    return saida


def nome_do_benchmark(item):
    descritor = item.get("Descriptor") or {}
    tipo = descritor.get("Type") or item.get("Type")
    metodo = descritor.get("Method") or item.get("Method")
    if tipo and metodo:
        return f"{tipo}.{metodo}"
    return None


def carregar_resultados(pasta):
    medias = {}
    for arq in sorted(pathlib.Path(pasta).rglob("*.json")):
        try:
            dados = json.loads(arq.read_text(encoding="utf-8"))
        except Exception:
            continue
        for item in extrair(dados):
            nome = nome_do_benchmark(item)
            media = (item.get("Statistics") or {}).get("Mean")
            if nome and isinstance(media, (int, float)):
                medias[nome] = round(float(media), 3)
    return medias


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--results", required=True)
    ap.add_argument("--baseline", required=True)
    ap.add_argument("--threshold", type=float, default=20.0)
    ap.add_argument("--update", action="store_true")
    args = ap.parse_args()

    atuais = carregar_resultados(args.results)
    if not atuais:
        print("  ERRO: nenhum resultado de benchmark encontrado")
        return 2

    base_path = pathlib.Path(args.baseline)
    anteriores = {}
    if base_path.exists() and not args.update:
        anteriores = json.loads(base_path.read_text(encoding="utf-8")).get("benchmarks", {})

    print(f"  benchmarks medidos: {len(atuais)}")
    piora = []
    for nome in sorted(atuais):
        atual = atuais[nome]
        antes = anteriores.get(nome)
        if antes is None:
            print(f"    {nome:<52} {atual/1000:>9.2f} µs   (sem baseline)")
            continue
        variacao = (atual - antes) / antes * 100.0 if antes else 0.0
        marca = "  " if variacao <= args.threshold else "!!"
        print(f"    {nome:<52} {atual/1000:>9.2f} µs   {variacao:+6.1f}%  {marca}")
        if variacao > args.threshold:
            piora.append((nome, antes, atual, variacao))

    if args.update:
        base_path.write_text(json.dumps({
            "gerado_por": "scripts/bench.sh --update",
            "maquina": "e52680v3 (Xeon E5-2680 v3)",
            "threshold_percent": args.threshold,
            "benchmarks": atuais,
        }, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
        print(f"  baseline atualizado: {args.baseline}")
        return 0

    if piora:
        print(f"\n  REGRESSAO acima de {args.threshold:.0f}% em {len(piora)} benchmark(s):")
        for nome, antes, atual, variacao in piora:
            print(f"    {nome}: {antes/1000:.2f} µs -> {atual/1000:.2f} µs ({variacao:+.1f}%)")
        return 1

    print(f"\n  ok: nenhum benchmark piorou mais de {args.threshold:.0f}%")
    return 0


if __name__ == "__main__":
    sys.exit(main())
