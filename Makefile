# EVE-O Preview - alvos de build usados pelo agente autonomo e pela CI.
# No Linux o build usa EnableWindowsTargeting (ver Directory.Build.props);
# testes de UI/Win32 so rodam no Windows.

DOTNET ?= dotnet
SOLUTION := src/EVE-O-Preview.sln
CORE_TESTS := src/EveO.Core.Tests/EveO.Core.Tests.csproj
WIN_TESTS := src/EveO.Tests/EveO.Tests.csproj

.PHONY: help restore build test-core test-windows check pack clean

help:
	@echo "make build        - compila a solucao em Release"
	@echo "make test-core    - testes de nucleo (net8.0, roda no Linux)"
	@echo "make test-windows - testes de UI/Win32 (somente Windows)"
	@echo "make check        - gate: build + testes disponiveis nesta maquina"
	@echo "make pack         - publica o executavel win-x64 em dist/"

restore:
	$(DOTNET) restore $(SOLUTION)

build: restore
	$(DOTNET) build $(SOLUTION) -c Release --no-restore --nologo

test-core: build
	$(DOTNET) test $(CORE_TESTS) -c Release --no-build --nologo

test-windows: build
	$(DOTNET) test $(WIN_TESTS) -c Release --no-build --nologo

ifeq ($(OS),Windows_NT)
check: build test-windows
else
check: build test-core
endif

pack:
	$(DOTNET) publish src/Eve-O-Preview/Eve-O-Preview.csproj -c Release -r win-x64 --self-contained false -o dist

clean:
	$(DOTNET) clean $(SOLUTION) -c Release --nologo || true
	rm -rf dist
