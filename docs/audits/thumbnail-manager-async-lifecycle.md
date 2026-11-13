# ThumbnailManager Async Lifecycle — Independent Audit

**Scope**: `src/Eve-O-Preview/Services/Implementation/ThumbnailManager.cs`
**Branch audited**: `main` (commit `bc39c5a`, 2026-11-13). Original audit: commit `3479ae7`, 2026-11-11.
**Auditor**: Autonomous Claude Code (Linux pilot); revised 2026-11-13 by OpenCode.
**Revision**: v1.1 — corrected against today's code (see "What changed since 11/11" below).

---

## Executive Summary

This audit examines the asynchronous lifecycle of `ThumbnailManager`, focusing on three critical areas:

1. **`UpdateThumbnailsList()`** — `async void` timer callback (line 297)
2. **`ThumbnailActivated()`** — `Task.Run` + UI-thread continuation (line 676)
3. **`Stop()`** — synchronous timer stop without awaiting in-flight work (line 286)

**Bottom line (v1.1)**: the current design has **two P0 risks** (unobserved exceptions crashing the process; shutdown races leaking native handles) and **two P1 risks** (concurrent timer/activation races; missing cancellation). The v1.0 P1 about the continuation scheduler was **invalid and is already resolved in code**, and the Linux interval P2 was **invalid** — details in section 0. No production code changes are proposed here — this document establishes the evidence base for a future minimal refactor.

---

## 0. What changed since 11/11 (v1.1 corrections)

| Item in v1.0 | Status now | Evidence |
|---|---|---|
| P1: `ThumbnailActivated` continuation captures a null scheduler | **Resolved, and the original analysis was wrong** | `uiScheduler` is captured **before** `Task.Run` (`ThumbnailManager.cs:1085`) and both continuations have `Trace.TraceError` handlers (`:1092`, `:1115`). Also, `TaskScheduler.FromCurrentSynchronizationContext()` in the `ContinueWith` **argument** is evaluated on the calling thread (UI), not inside the lambda, so the "captures null" claim did not hold even for the old shape. |
| P2: Linux `ThumbnailRefreshPeriod` is unclamped | **Invalid** | Clamps exist per platform: 10–1000 ms on Linux (`ThumbnailConfiguration.cs:545`), 300–1000 ms on Windows (`:547`). |
| `Stop()` only stops the timer | **Outdated** | `Stop()` also closes spotlight views (`ThumbnailManager.cs:609-617`), but still does not await in-flight async work. |
| 3 `async void` methods | **Now 6** | `SpotlightViewRegionSelected` (`:506`), `SaveSpotlightConfiguration` (`:598`), `UpdateThumbnailsList` (`:626`), `PublishSpotlightRegionUpdated` (`:945`), `ThumbnailViewResized` (`:1155`), `RaiseThumbnailLocationUpdatedNotification` (`:1463`). **None** has a `try/catch`. |
| Line map | **Stale** | File grew from ~1056 to 1488 lines; every line reference below was re-checked. |

---

## 1. Code Map — Relevant Files & Lines (re-checked on `bc39c5a`)

| File | Lines | Role |
|------|-------|------|
| `ThumbnailManager.cs` | 603–607 | `Start()` — begins timer, calls `RefreshThumbnails()` synchronously |
| `ThumbnailManager.cs` | 609–617 | `Stop()` — stops timer **and closes spotlight views** (no await) |
| `ThumbnailManager.cs` | 619–623 | `ThumbnailUpdateTimerTick` — calls `UpdateThumbnailsList()` + `RefreshThumbnails()` |
| `ThumbnailManager.cs` | 626–746 | **`UpdateThumbnailsList()`** — `async void`, process diff + view create/destroy + `mediator.Publish` |
| `ThumbnailManager.cs` | 748–939 | `RefreshThumbnails()` — synchronous, heavy UI work, mutates `_thumbnailViews` |
| `ThumbnailManager.cs` | 1070–1150 | **`ThumbnailActivated()`** — `Task.Run(ActivateWindow)` → `ContinueWith(UI scheduler)` |
| `ThumbnailManager.cs` | 1155 | `ThumbnailViewResized()` — `async void` event handler, `mediator.Publish` |
| `ThumbnailManager.cs` | 1463 | `RaiseThumbnailLocationUpdatedNotification()` — `async void`, `mediator.Send` |
| `ThumbnailManager.cs` | 506, 598, 945 | Additional `async void` handlers (spotlight selection/save/publish) |
| `IThumbnailManager.cs` | 7–8 | Interface: `Start()`/`Stop()` return `void` (no cancellation) |
| `IProcessMonitor.cs` | 9 | `GetUpdatedProcesses(out added, out updated, out removed)` — synchronous |

---

## 2. Risk Register

### P0 — Unobserved Exceptions in `async void` Methods Crash the Process

**Evidence**:
- Six `async void` methods, **none with a `try/catch`**: `UpdateThumbnailsList()` (`:626`), `ThumbnailViewResized()` (`:1155`), `RaiseThumbnailLocationUpdatedNotification()` (`:1463`), `SpotlightViewRegionSelected()` (`:506`), `SaveSpotlightConfiguration()` (`:598`), `PublishSpotlightRegionUpdated()` (`:945`).
- Any exception escaping one of them terminates the process (per .NET semantics for `async void`).
- `_mediator.Publish/Send` can throw (handler throws, pipeline behavior throws, DI scope disposed), and so can the native interop inside `UpdateThumbnailsList`.

**Failure scenario**:
Timer ticks → `UpdateThumbnailsList()` calls `_processMonitor.GetUpdatedProcesses()` → native interop throws `SEHException` / `AccessViolationException` → process dies with no log, no restart.

**Mitigation status**: None in current code.

---

### P0 — `Stop()` Does Not Await In-Flight Async Work → Native Handle Leaks / Use-After-Free

**Evidence**:
- `Stop()` (`:609-617`) stops the timer and closes spotlight views, but returns immediately.
- If `UpdateThumbnailsList()` is mid-await on `_mediator.Publish`, or `ThumbnailActivated()`'s `Task.Run` is executing `ActivateWindow`, `Stop()` does not wait.
- Subsequent disposal can finalize `_thumbnailViews` while callbacks still hold `IThumbnailView` references → `view.Close()` races with `view.Refresh()` or `ActivateWindow`.

**Failure scenario**:
User closes app → `Stop()` called → timer stops → `UpdateThumbnailsList()` still awaiting `mediator.Publish` → `_thumbnailViews` cleared by GC/finalizer → continuation accesses disposed `IThumbnailView` → `ObjectDisposedException` or native handle corruption.

**Mitigation status**: None. `IThumbnailManager.Stop()` returns `void`; no `CancellationToken` plumbing exists.

---

### P1 — Concurrent Timer Tick + `ThumbnailActivated()` Race on `_thumbnailViews`

**Evidence**:
- `ThumbnailUpdateTimerTick` (line 292) runs on UI thread (DispatcherTimer) → calls `UpdateThumbnailsList()` (async, yields at line 395) → `RefreshThumbnails()` (line 294, synchronous, iterates `_thumbnailViews`).
- `ThumbnailActivated()` (line 676) fires on thumbnail click → `Task.Run` captures `view` (line 678) → continuation (line 688–694) runs on UI thread → calls `SwitchActiveClient`, `UpdateClientLayouts`, `RefreshThumbnails()`.
- Both paths mutate/iterate `_thumbnailViews` **without synchronization** (only `_ignoreViewEvents` boolean guard, which does not protect the dictionary).

**Failure scenario**:
Timer tick enumerates `_thumbnailViews` in `RefreshThumbnails()` (line 489) while `ThumbnailActivated` continuation calls `RefreshThumbnails()` re-entrantly → `InvalidOperationException` ("Collection was modified") or stale `view` reference.

**Mitigation status**: Partial — `_ignoreViewEvents` suppresses event *handlers* but not the timer callback itself.

---

### P1 — No CancellationToken Propagation → Cannot Cancel In-Flight Work on Shutdown

**Evidence**:
- `UpdateThumbnailsList()` calls `_mediator.Publish` (line 395) with no token.
- `ThumbnailActivated()` uses `Task.Run` with no token (line 680).
- `RaiseThumbnailLocationUpdatedNotification()` calls `_mediator.Send` (line 1039) with no token.
- `IThumbnailManager.Stop()` signature cannot express cancellation.

**Failure scenario**:
App shutdown → `Stop()` → process exits while `mediator.Publish` waits for downstream handlers (e.g., persistence writing to disk) → data loss or handle leak.

**Mitigation status**: None. Requires interface change (`Stop(CancellationToken)` or `Task StopAsync()`).

---

### P1 — ~~`ContinueWith(TaskScheduler.FromCurrentSynchronizationContext())` Fragility~~ (Resolved)

**Status**: no longer applies, and the original analysis was wrong.

The current code captures the scheduler on the UI thread **before** starting the task (`ThumbnailManager.cs:1085`) and passes it to both continuations, which also carry `Trace.TraceError` handlers (`:1092`, `:1115`).

```csharp
var uiScheduler = TaskScheduler.FromCurrentSynchronizationContext(); // UI thread, before Task.Run
Task.Run(() => this._windowManager.ActivateWindow(...))
    .ContinueWith(activation => { ... }, uiScheduler);
```

**Correction of the v1.0 reasoning**: `TaskScheduler.FromCurrentSynchronizationContext()` in the *argument* of `ContinueWith` is evaluated when `ContinueWith` is called — on the UI thread — not inside the continuation lambda. So the old shape would also have captured the UI context, unless the call had been inside the `Task.Run` body. The "captures null → runs on thread-pool" claim did not hold.

---

### ~~P2 — `DispatcherTimer` Interval Configuration Mismatch on Linux~~ (Invalid)

**Status**: the claim does not hold. `ThumbnailRefreshPeriod` is clamped per platform:

- Linux: `ApplyRestrictions(..., 10, 1000)` at `ThumbnailConfiguration.cs:545`.
- Windows: `ApplyRestrictions(..., 300, 1000)` at `ThumbnailConfiguration.cs:547`.

A Linux user setting 50 ms gets 50 ms (allowed by the Linux range), not an unclamped value — the low floor is intentional for the Wine/Linux path. What remains true: the floor differs by platform (10 ms vs 300 ms), so a shared config can behave differently.

---

### P2 — `_hideThumbnailsDelay` Counter Not Thread-Safe

**Evidence**:
- `_hideThumbnailsDelay` (field, line 52) read/written in `RefreshThumbnails()` (lines 442–455) which runs on UI thread *only* currently.
- If `RefreshThumbnails()` ever called off-UI (see P1 above), counter races.

**Failure scenario**:
Low probability today; becomes real if P1 continuation bug manifests.

---

## 3. Proven vs. Hypothesised Risks

| Risk | Status | Proof |
|------|--------|-------|
| P0: `async void` exceptions crash process | **Proven (still open)** | 6 `async void` methods, none with `try/catch` (lines in section 2). Unhandled exception in `async void` terminates the process. |
| P0: `Stop()` races with in-flight async | **Proven (still open)** | `Stop()` is synchronous void; `UpdateThumbnailsList()` awaits `mediator.Publish`; no join/await mechanism exists. |
| P1: Timer vs. activation race on `_thumbnailViews` | **Proven** | Two code paths (timer tick, activation continuation) mutate/iterate same `Dictionary` without lock. `_ignoreViewEvents` does not guard dictionary access. |
| P1: Missing `CancellationToken` | **Proven** | Zero `CancellationToken` parameters in call chain from `Start()`/`Stop()` to `mediator` calls. |
| P1: `ContinueWith` captures wrong scheduler | **Invalid / resolved** | Scheduler is captured before `Task.Run` (`:1085`) and continuations log errors; the original reasoning about the argument evaluation was wrong. |
| P2: Linux timer interval unclamped | **Invalid** | Clamps exist per platform (`ThumbnailConfiguration.cs:545` Linux, `:547` Windows). |
| P2: `_hideThumbnailsDelay` thread-safety | **Hypothesised** | Only triggered if P1 continuation bug occurs. |

---

## 4. Test Scenarios (for Future Regression Suite)

| ID | Scenario | Expected | Current Coverage |
|----|----------|----------|------------------|
| T-01 | `UpdateThumbnailsList()` throws in `GetUpdatedProcesses` | App stays alive, error logged, timer continues | **None** |
| T-02 | `Stop()` called while `UpdateThumbnailsList()` awaiting `mediator.Publish` | `Stop()` returns after await completes; no use-after-free | **None** |
| T-03 | Rapid thumbnail clicks during timer tick | No `InvalidOperationException`, no visual corruption | **None** |
| T-04 | `ThumbnailActivated` continuation runs on UI thread | `SynchronizationContext.Current` is UI context in continuation | **Code fixed** (`:1085`), **no test** |
| T-05 | Linux: `ThumbnailRefreshPeriod = 50` → timer stays ≥ 10 ms | Interval clamped to the 10–1000 ms Linux range | **Clamp exists** (`:545`), **no test** |
| T-06 | `mediator.Publish` throws in `RaiseThumbnailLocationUpdatedNotification` | App stays alive, error logged | **None** |
| T-07 | Shutdown: `Stop()` → `Dispose()` → finalizer → no handle leaks | Zero native handles leaked | Manual only |

---

## 5. Minimal Future Change Plan (Not Implemented Here)

> **Constraint**: This audit *only* documents. The following is a straw-man for the maintainer to scope a follow-up PR.

| Change | Files | Risk Addressed | Effort |
|--------|-------|----------------|--------|
| Wrap all `async void` bodies in `try/catch` → log + suppress | `ThumbnailManager.cs` (lines 506, 598, 626, 945, 1155, 1463) | P0 (crash) | Low |
| Change `Stop()` → `Task StopAsync(CancellationToken)`; propagate token to `mediator` calls | `IThumbnailManager.cs`, `ThumbnailManager.cs`, callers | P0 (shutdown race), P1 (cancellation) | Medium |
| ~~Capture UI `TaskScheduler` *before* `Task.Run`~~ | — | P1 (wrong thread) | **Done** (`:1085`) |
| Add `lock(_thumbnailViews)` in `RefreshThumbnails` + `ThumbnailActivated` continuation | `ThumbnailManager.cs` | P1 (dictionary race) | Low |
| ~~Fix Linux `ApplyRestrictions()`~~ | — | P2 (timer interval) | **Invalid** — clamp already there (`:545`) |
| Add integration test harness mocking `IProcessMonitor`/`IWindowManager`/`IMediator` | `EveO.Tests/` (new) | All (regression) | Medium |

---

## 6. What Was *Not* Verified (Linux CI Gap)

- **No Windows 6+1 multibox stress test executed** — this audit ran on Linux; WPF/WinForms interop, `DispatcherTimer`, `TaskScheduler.FromCurrentSynchronizationContext()`, and native `IWindowManager` implementations behave differently on Windows.
- **No manual validation of `ThumbnailActivated` continuation thread** — requires attaching debugger to Windows build.
- **No measurement of timer re-entrancy under load** — requires 6+ EVE clients running.

**Manual checks still required on Windows**:
1. Verify `ThumbnailActivated` continuation executes on UI thread (breakpoint in continuation, inspect `Thread.CurrentThread.ManagedThreadId` vs. UI thread ID).
2. Stress-test 6+1 layout: start 6 clients + intel window, cycle rapidly, verify no `InvalidOperationException` or visual tearing.
3. Verify `Stop()` during active thumbnail refresh does not leak `DwmThumbnail` handles (Process Explorer → handle count).
4. ~~Confirm Linux `ThumbnailRefreshPeriod` clamping works~~ — clamp confirmed in code (`ThumbnailConfiguration.cs:545`); still worth a unit test.

---

## 7. Appendix — Async Call Graph

```
Start()
  └─ _thumbnailUpdateTimer.Start()
  └─ RefreshThumbnails() [sync]

ThumbnailUpdateTimerTick (UI thread)
  ├─ UpdateThumbnailsList() [async void]
  │   ├─ _processMonitor.GetUpdatedProcesses() [sync, native]
  │   ├─ foreach added: _thumbnailViewFactory.Create() → view events +=
  │   ├─ foreach removed: view.Close() [native]
  │   └─ await _mediator.Publish(ThumbnailListUpdated) [FIRE-AND-FORGET]
  └─ RefreshThumbnails() [sync, heavy UI]
      ├─ _windowManager.GetForegroundWindowHandle() [native]
      ├─ foreach view: view.Refresh(), view.Show/Hide, view.SetOpacity...
      └─ EnableViewEvents()

ThumbnailActivated (UI thread, from view click)
  └─ Task.Run(() => _windowManager.ActivateWindow(...)) [thread-pool]
      └─ ContinueWith(continuation, uiScheduler) [scheduler captured before Task.Run]
          ├─ SwitchActiveClient()
          ├─ UpdateClientLayouts()
          └─ RefreshThumbnails() [RE-ENTRANT!]

Stop()
  └─ _thumbnailUpdateTimer.Stop() [NO AWAIT]
  └─ CloseSpotlightView() for each spotlight
```

---

**End of audit.** This document is the sole deliverable for issue #11 pilot. No production or test files were modified.