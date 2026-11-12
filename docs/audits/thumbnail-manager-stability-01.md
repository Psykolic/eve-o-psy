# ThumbnailManager stability audit — TM-01

**Scope:** Windows/.NET 8, `src/Eve-O-Preview/Services/Implementation/ThumbnailManager.cs`.  
**Baseline:** `main` after the PR #7 revert.  
**Change branch:** `feature/thumbnail-manager-stability-01` (independent of draft PRs #3, #4, #5 and #8).  
**Validation status:** Source review and unit tests submitted; Windows CI result must be checked separately. No EVE client, Win32/GDI stress harness, or 6+1 run executed as part of this change.

## Confirmed risks and changes

| Severity | Evidence | Mitigation in TM-01 | Verification |
| --- | --- | --- | --- |
| P1 | `RefreshThumbnails()` disables view events before iterating and previously re-enabled them only at the end; exceptions in `IThumbnailView.Refresh` could leave events disabled | Wrap the refresh body in `try/finally` so `EnableViewEvents()` runs even when a view fails | `RefreshFailureReenablesViewCallbacks` injects a view failure |
| P1 | `UpdateThumbnailsList()` indexed `_thumbnailViews[process.Handle]` for removed processes, assuming view dictionary and monitor snapshots never diverge | `TryRemoveThumbnail` checks `TryGetValue` and safely ignores an already-removed handle | `RemovingAnUnknownHandleDoesNotCrashOrAnnounceARemoval` |
| P2 | Removed views cleared most callbacks but not `ThumbnailDeactivated` | Clear all seven callbacks before closing a removed view | `RemovingAnExistingViewDetachesCallbacksAndClosesIt` |
| P2 | Optional hotkey groups can be null after malformed/incomplete configuration, and `foreach` would throw during manager initialization | Guard null enumerables in the two registration helpers | Test fixture constructs the manager with optional hotkey groups unset |

## Deferred findings (not implemented)

1. `UpdateThumbnailsList()` is `async void`, so publication failures have event-style exception behavior. Consider an awaited, single-flight update loop in a **separate** PR with lifecycle tests.
2. `ThumbnailActivated()` schedules window activation with `Task.Run` and then updates client state in a continuation. Test activation failure, rapid window close, shutdown and focus changes before changing this path.
3. Several other `DisableViewEvents()` / `EnableViewEvents()` pairs exist. Audit their exception paths independently rather than broadening this patch without tests.
4. The 6+1 scenario requires manual confirmation that six cycling clients stay on the primary monitor and the intel-only client remains separate on the secondary monitor; no window focus or input should be broadcast.
5. Native Win32/GDI/DWM ownership and 1,000-capture resource stress belong to the independent draft GDI change series (#3, #4, #5). Their checks do not establish TM-01 behavior.

## Acceptance criteria

- Windows `dotnet build src/EVE-O-Preview.sln --configuration Release` passes.
- Windows `dotnet test src/EveO.Tests/EveO.Tests.csproj --configuration Release` passes, including all three TM-01 tests.
- Existing build/test workflows remain unchanged.
- No production code outside `ThumbnailManager.cs` is modified.
- A manual Windows 6+1 smoke test and GDI baseline are required **before** claiming full multibox stability.

## Safety and integration

No gameplay automation, broadcast input, process injection, or client-memory manipulation. Only app-side thumbnail event and view lifecycle handling. The change is meant for an isolated **Draft** PR; do not merge it with the existing stacked GDI PRs or the shared-skills PR.
