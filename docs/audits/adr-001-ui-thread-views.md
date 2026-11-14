# ADR-001 — Confining Views Dictionary to the UI Thread

**Date**: 2026-11-14
**Decision**: UI-thread confinement without locks
**Status**: Draft PR

---

## 1. Context

**Risk P1 — Concurrent access to `_thumbnailViews`** (`src/Eve-O-Preview/Services/Implementation/ThumbnailManager.cs:45`).

The `_thumbnailViews` field is a `Dictionary<IntPtr, IThumbnailView>` that stores all client thumbnails. It is accessed from multiple code paths that can execute on the UI thread or, potentially, on thread-pool threads:

| Source | Path | Lines |
|--------|------|-------|
| `DispatcherTimer.Tick` → `ThumbnailUpdateTimerTick` → `UpdateThumbnailsListCore` | UI thread (Dispatcher) | 761–865 |
| `ThumbnailActivated` → `Task.Run` → continuation with `TaskScheduler.FromCurrentSynchronizationContext()` | Thread-pool → UI continuation | 1225–1278 |
| Hotkey handlers (e.g. `CycleNextClient`, `MinimizeAllClients`) | UI thread (hotkey callback) | 180–288, 180–187 |
| `RefreshSpotlightViews`, `SyncSpotlightViews` | UI thread (invoked from hotkey/event handlers) | 380–487 |
| `TryRemoveThumbnail` | UI thread (called from `RefreshThumbnails`) | 869–896 |
| `SetActive`, `GetClientByPointer`, etc. | UI thread (public API) | 147–163, 153–157 |

Before the confinement decision, there was **no synchronization primitive** (lock, `Monitor`, `lock`) protecting `_thumbnailViews`. The only guard was `_ignoreViewEvents` (a boolean flag) which suppresses event *handlers* but does **not** protect dictionary mutability. This constituted a P1 race condition: if two threads simultaneously mutating/iterating the same `Dictionary<,>` would throw `InvalidOperationException` ("Collection was modified") or, worse, corrupt internal state without detection.

The risk was identified in **issue #59** and documented in prior audits (#12, #14, TM-01). The `TimerTickFollowedByActivation_KeepsThumbnailViewsConsistent` test (`ThumbnailManagerUiThreadConfinementTests.cs:216`) explicitly exercises the serialisation of timer tick and activation continuation on the UI thread, proving that both paths must remain on the same thread to keep the dictionary consistent.

## 2. Decision

**Confinement to the UI thread, without locks.**

The decision was to enforce that `_thumbnailViews` is **only accessed on the thread that created it** — the UI thread. All public entry points and internal methods that touch the dictionary must check that they are running on the UI thread and throw `InvalidOperationException` if called from another thread.

The enforcement mechanism is the static invariante `EnsureUiThread` (`ThumbnailManager.cs:132–140`) and the helper `EnsureThumbnailViewsThread` (`:142–145`):

```csharp
// Invariante de concorrência do dicionário de views (issue #59 / auditoria TM-01 P1).
// Não existe lock: o dicionário é confinado à thread de UI. Um acesso a partir de
// outra thread é defeito de programação e falha alto, em vez de corromper o estado.
internal static void EnsureUiThread(int uiThreadId, string operation)
{
    int currentThreadId = Environment.CurrentManagedThreadId;
    if (currentThreadId != uiThreadId)
    {
        throw new InvalidOperationException(
            $"{operation} deve ser executado na thread de UI (esperada {uiThreadId}, atual {currentThreadId}).");
    }
}
```

Every public method that accesses `_thumbnailViews` calls `EnsureThumbnailViewsThread` with a descriptive operation name. Examples:

- `GetClientByTitle` (`:149`)
- `GetClientByPointer` (`:155`)
- `GetActiveClient` (`:161`)
- `SetActive` (`:167`)
- `MinimizeAllClients` (`:182`)
- `CycleNextClient` (`:190`)
- `RefreshThumbnails` (`:900`)
- `UpdateSpotlights` (`:1065`)
- `TryRemoveThumbnail` (`:871`)

The `_uiThreadId` is captured at construction time (`:105`):

```csharp
this._uiThreadId = Environment.CurrentManagedThreadId;
```

Because the `DispatcherTimer`, view events, and activation continuations all run on the UI thread (the same `SynchronizationContext`), **no lock is needed**. The trade-off is that any off‑UI access is caught early as a clear `InvalidOperationException` rather than causing subtle corruption or `InvalidOperationException` at runtime from the dictionary itself.

## 3. Invariant `EnsureUiThread` and Why It Fails High

The `EnsureUiThread` invariant is **high‑failure** by design:

- **Fail fast**: Throwing `InvalidOperationException` is the .NET recommended pattern for violating a thread-affinity invariant. It is immediately visible in debug logs and test output, unlike Heap Corruption or `InvalidOperationException` from the dictionary which can be intermittent and hard to reproduce.
- **No data loss**: The exception does not corrupt `_thumbnailViews`; the dictionary remains in whatever state it was in before the offending call. This is critical because the alternative (silently corrupting) would be far worse.
- **Explicit contract**: The method name and the `InvalidOperationException` make the contract explicit: "callers must be on the UI thread." This is self-documenting and assists future maintainers.
- **Test coverage**: The confinement tests (`ThumbnailManagerUiThreadConfinementTests.cs`) verify that calls from a background thread indeed throw, and that calls on the owning thread pass. The test `EnsureUiThread_FromAnotherThread_Throws` (`:136–146`) captures the exception from a background thread and asserts the message contains both the operation name and the expected owning thread ID.

The invariant fails **high** because it is checked at **every public API entry point** (`EnsureThumbnailViewsThread` is called in all methods that touch `_thumbnailViews`). If a new code path accesses the dictionary without the check, the test suite will fail immediately, providing a strong regression guard.

## 4. Rejected Alternatives

| Alternative | Description | Costs / Why Rejected |
|---|---|---|
| **Global lock (`lock(_thumbnailViews)`)** | Add a `private static readonly object _thumbnailViewsLock = new();` and wrap every dictionary access in `lock(_thumbnailViewsLock)`. | - **Performance**: The UI thread is the bottleneck already (timer tick, refresh, activation). Adding lock contention would serialize timer tick and activation continuation, increasing jitter. <br>- **Deadlock risk**: `RefreshThumbnails` already disables view events (`DisableViewEvents`/`EnableViewEvents`) and does work that could be re‑entered from a lock. <br>- **Does not address the root**: The real problem is that `ThumbnailActivated` posts work to the UI thread via `Task.Run` + continuation. A lock would mask the threading bug rather than fixing the design. |
| **Reader‑Writer lock (`ReaderWriterLockSlim`)** | Allow concurrent reads, exclusive writes. | - **Complexity**: Reads (e.g., `TryGetValue`) are frequent; writes (add/remove) are less frequent but still require coordination. The added state machine adds cognitive load and a surface for bugs. <br>- **Still a lock**: Same deadlock/performance concerns as above, just with better read‑throughput. Not needed because all mutating paths already run on the UI thread in the current architecture. |
| **ConcurrentDictionary** | Replace `Dictionary<IntPtr, IThumbnailView>` with `ConcurrentDictionary<IntPtr, IThumbnailView>`. | - **API mismatch**: `ConcurrentDictionary` does not expose the same LINQ-friendly iteration patterns used throughout (`_thumbnailViews.OrderBy`, `_thumbnailViews.Reverse`, `foreach` with `KeyValuePair`). <br>- **No thread‑affinity guarantee**: Would silently allow cross‑thread access, reverting to the original race‑condition risk but with a different failure mode. <br>- **Refactoring cost**: Significant — would require changing every iteration pattern and removing the `EnsureUiThread` checks. |
| **UI‑thread marshaling via `SynchronizationContext.Post`** | Ensure all dictionary access goes through `Post` to the UI thread. | - **Inversion of control**: Would require every caller to marshal work to the UI thread, which is exactly what the confinement decision avoids. <br>- **Performance overhead**: `Post` + delegate allocation per call, vs. a simple `EnsureUiThread` check. <br>- **Existing pattern**: The codebase already uses `EnsureUiThread` + direct access; adding `Post` would be redundant. |

The chosen approach (UI‑thread confinement + `EnsureUiThread` check) was selected because:

1. **All existing paths already run on the UI thread** — the `DispatcherTimer`, event handlers, and activation continuation are all bound to the UI `SynchronizationContext`.
2. **Zero lock overhead** — no `Monitor` enter/leave, no contention.
3. **Fail‑fast semantics** — any future off‑UI access is caught immediately with a clear error message, rather than causing subtle corruption.
4. **Tests already exist** — the confinement test suite validates the invariant, providing a regression guard.

## 5. Consequences and Residual Risks

| Area | Consequence |
|---|---|
| **Thread-affinity enforcement** | All methods that touch `_thumbnailViews` must call `EnsureThumbnailViewsThread`. If a new code path is added that needs to access the dictionary from another thread, the `EnsureUiThread` check must be updated or the design reassessed. |
| **`Stop()` shutdown race** (`ThumbnailManager.cs:686–723`) | `StopAsync` captures the `SynchronizationContext` and uses `RunOnThreadAsync` to close spotlight views in the UI thread. If the app is headless (no UI `SynchronizationContext`), the code falls back to running in the current thread (see comment at `:690–691`). This is a **residual risk** for non‑UI contexts (tests, headless automation). |
| **`_hideThumbnailsDelay` counter** (`:52, :442–455`) | Read/written only on the UI thread today. If a future path accesses it off‑UI, a race condition would appear. The confinement invariant does not cover this field currently. |
| **`_locationChangeNotificationSyncRoot` lock** (`:1586–1604`) | A `lock` exists for the location‑change notification queue, but it protects a different data structure (`_enqueuedLocationChangeNotification`). It does **not** conflict with the UI‑thread confinement of `_thumbnailViews`. |
| **Testability** | The confinement tests (`ThumbnailManagerUiThreadConfinementTests.cs`) verify the invariant. However, any test that runs the manager on a non‑UI thread will need to either marshal to the UI thread or expect `InvalidOperationException`. This is already the case in the existing test suite. |
| **Future refactoring** | If the architecture moves to a reactive or async/await pattern that genuinely needs cross‑thread access, the confinement model would need to be revisited (e.g., introducing a proper lock or moving to `ConcurrentDictionary` with appropriate synchronization). The ADR documents this as a known constraint. |

## 6. Reference Evidence

- **Source**: `src/Eve-O-Preview/Services/Implementation/ThumbnailManager.cs`
  - Field `_thumbnailViews` declaration (`:45`) with comment on confinement (`:42–44`).
  - Invariante `EnsureUiThread` (`:132–140`) and `EnsureThumbnailViewsThread` (`:142–145`).
  - All public methods that call `EnsureThumbnailViewsThread` before accessing `_thumbnailViews`.
  - Capture of `_uiThreadId` at construction (`:105`).
  - `StopAsync` fallback when no UI `SynchronizationContext` is present (`:690–691`).

- **Tests**: `src/EveO.Tests/Services/ThumbnailManagerUiThreadConfinementTests.cs`
  - `EnsureUiThread_OnOwningThread_DoesNotThrow` (`:128–133`) — verifies no exception on the owning thread.
  - `EnsureUiThread_FromAnotherThread_Throws` (`:136–146`) — verifies `InvalidOperationException` when called from a background thread.
  - `GetClientByTitle_FromAnotherThread_Throws` (`:149–157`), `GetClientByPointer_FromAnotherThread_Throws` (`:160–168`), etc. — all public API methods throw when called from the wrong thread.
  - `TimerTickFollowedByActivation_KeepsThumbnailViewsConsistent` (`:216–280`) — exercises the serialisation of timer tick and activation continuation on the UI thread, confirming the dictionary stays consistent.

## 7. Checklist for Future Changes

- [ ] Any new public method on `ThumbnailManager` that accesses `_thumbnailViews` must call `EnsureThumbnailViewsThread(nameof(...))`.
- [ ] Any new private method that accesses `_thumbnailViews` must be called only from paths already guaranteed to be on the UI thread, or must also have the check.
- [ ] If a method needs to access `_thumbnailViews` from a background thread, the design must be re‑evaluated (add a lock, move to `ConcurrentDictionary`, or marshal to UI thread).
- [ ] The `_uiThreadId` field should not be changed after construction; if the manager is created on a different thread, the invariant will fail.
- [ ] The `StopAsync` method’s fallback to the current thread when no `SynchronizationContext` is present (line `:690–691`) is a known exception to the UI‑thread rule and is documented in the source comment.

---

**End of ADR-001**.