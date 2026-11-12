# Licença e proveniência do EVE-O Custom

O EVE-O Custom, como conjunto, é distribuído sob `GPL-3.0-only`. O texto integral está em [`LICENSE`](../LICENSE). O aviso de copyright e o texto MIT anteriores foram preservados integralmente em [`LICENSES/MIT.txt`](../LICENSES/MIT.txt). Os avisos de cada arquivo e de terceiros devem acompanhar o código ao ser movido ou importado.

| Origem | Referência | Tratamento |
| --- | --- | --- |
| EVE-O Preview, upstream compartilhado | [`3febbe5`](https://github.com/Proopai/eve-o-preview/commit/3febbe503de8cf85abcbba31bc2ab89f76f7b492) | Preservar os avisos herdados. |
| EVE-O Custom antes desta decisão | [`9b8cca8`](https://github.com/Psykolic/eve-o-custom/tree/9b8cca8fb08319a360be8f5b22a0450383cdd674) | Base técnica para a integração. O `LICENSE` MIT desse commit foi copiado byte a byte. |
| EVE-O Preview T0C | [`8b8777a`](https://github.com/Psykolic/eve-o-preview-t0c/tree/8b8777a32fc766f5336a695f52e08f329db4f850) | O texto GPLv3 veio do `LICENSE` desse commit. Qualquer importação de código deve registrar o repositório, o SHA, os arquivos e a licença aplicável. |

---

## Rastreamento por arquivo (importações do T0C)

> Registre o commit de origem, o caminho e o SHA do blob de cada arquivo. Em um clone do T0C, use `git rev-parse "<commit>:<caminho-no-T0C>"`. Pelo GitHub CLI, use `gh api "repos/Psykolic/eve-o-preview-t0c/contents/<caminho-no-T0C>?ref=<commit>" --jq .sha`. Use `🔄 Planejado` enquanto a importação estiver pendente, `✅ Importado` para uma cópia e `⚠️ Adaptado` quando houver alterações.

| Origem | Arquivo no T0C | SHA do blob de origem | Destino planejado no Custom | Status | Observações |
| --- | --- | --- | --- | --- | --- |
| T0C @ 8b8777a | `src/Eve-O-Preview/Services/Implementation/WindowManager.cs` | `e95c563d570b37222425d33c3f5e24c122216796` | `src/Eve-O-Preview/Capture/Win32Capture.cs` | 🔄 Planejado | Referência para a captura GDI/BitBlt (`GetStaticThumbnail`). |
| T0C @ 8b8777a | `src/Eve-O-Preview/Services/Interop/Gdi32NativeMethods.cs` | `ef5ca24270aefae1d564ccf63726c1fb948eb59f` | `src/Eve-O-Preview/Capture/Interop/Gdi32NativeMethods.cs` | 🔄 Planejado | P/Invoke GDI32 (CreateCompatibleDC, BitBlt, DeleteObject, etc.) |
| T0C @ 8b8777a | `src/Eve-O-Preview/Services/Interop/User32NativeMethods.cs` | `cec122f6bea2e4812ab9a25c0d9df1a977b7edb1` | `src/Eve-O-Preview/Capture/Interop/User32NativeMethods.cs` | 🔄 Planejado | P/Invoke USER32 (`GetDC`, `ReleaseDC`, `GetClientRect`). |
| T0C @ 8b8777a | `src/Eve-O-Preview/Services/Implementation/DwmThumbnail.cs` | `d0f60384a7fd74079b20b60ec057812627749629` | `src/Eve-O-Preview/Capture/DwmThumbnailCapture.cs` | 🔄 Planejado | Wrapper DWM Thumbnail API |
| T0C @ 8b8777a | `src/Eve-O-Preview/Services/Implementation/ThumbnailManager.cs` | `6c38827cb7c6faed738af6082353c4be331787a4` | `src/Eve-O-Preview/Capture/ThumbnailManager.cs` | 🔄 Planejado | Orquestração de thumbnails (multi-cliente) |
| T0C @ 8b8777a | `src/Eve-O-Preview/Services/Interop/DwmNativeMethods.cs` | `3ae07a760331d2b9f17dfcf557ca4dd04819774e` | `src/Eve-O-Preview/Capture/Interop/DwmNativeMethods.cs` | 🔄 Planejado | P/Invoke DWM (DwmRegisterThumbnail, DwmUpdateThumbnailProperties, DwmUnregisterThumbnail) |
| T0C @ 8b8777a | `src/Eve-O-Preview/Services/Interop/DWM_THUMBNAIL_PROPERTIES.cs` | `df7ac617be383f67d7a937233e13a15d1605f180` | `src/Eve-O-Preview/Capture/Interop/DwmThumbnailProperties.cs` | 🔄 Planejado | Struct DWM_THUMBNAIL_PROPERTIES |
| T0C @ 8b8777a | `src/Eve-O-Preview/Services/Interop/DWM_TNP_CONSTANTS.cs` | `e3bd3687654f76b4b684cf708591c9d6f8be6bf3` | `src/Eve-O-Preview/Capture/Interop/DwmTnpConstants.cs` | 🔄 Planejado | Flags DWM_TNP_* |
| T0C @ 8b8777a | `src/Eve-O-Preview/Services/Interop/RECT.cs` | `9f8b89acf180595a651959bcbc1f70d2998891cf` | `src/Eve-O-Preview/Capture/Interop/Rect.cs` | 🔄 Planejado | Struct RECT (compartilhada GDI/DWM) |
| T0C @ 8b8777a | `src/Eve-O-Preview/Services/Interop/MARGINS.cs` | `98adc8db9c08b8f3d899cd935b67edb3f65d982b` | `src/Eve-O-Preview/Capture/Interop/Margins.cs` | 🔄 Planejado | Struct MARGINS (DWM) |

---

## Checklist de conformidade por importação

Antes de marcar `✅ Importado`, confirmar:

- [ ] Licença aplicável conferida por arquivo, com os cabeçalhos e avisos existentes preservados.
- [ ] Adicionada linha de proveniência: `// Ported from T0C @ 8b8777a32fc766f5336a695f52e08f329db4f850 (src/Eve-O-Preview/...)`
- [ ] `SafeHandle`/`IDisposable` implementados corretamente para todos handles não-gerenciados
- [ ] Testes unitários criados (falhas injetadas + stress 1.000 iterações)
- [ ] CI passa (Build + Testes + GDI/DWM stress)
- [ ] Revisão Claude Code concluída (arquitetura, interop, ciclo de vida)

---

## Registro de binários distribuídos

| Versão/Tag | Commit EVE-O | Commit T0C (submódulo ou import) | Artefatos | Fonte correspondente disponível em |
| --- | --- | --- | --- | --- |
| — | — | — | — | — |

> Preencher a cada release. A GPLv3 exige que o código-fonte correspondente (incluindo scripts de build) esteja disponível aos destinatários do binário.

---

O histórico acima é o ponto de partida para a conferência por arquivo. Antes de importar código do T0C, conferir os cabeçalhos, avisos e licenças das dependências envolvidas. Antes de distribuir um binário, identificar o commit que o produziu e disponibilizar aos destinatários o código-fonte correspondente, incluindo os scripts necessários à compilação e à instalação conforme a GPLv3.
