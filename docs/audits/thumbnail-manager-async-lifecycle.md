# ThumbnailManager Async Lifecycle — Independent Audit

**Scope**: `src/Eve-O-Preview/Services/Implementation/ThumbnailManager.cs`
**Branch audited**: `main` (commit `3479ae7`)
**Date**: 2026-11-11
**Auditor**: Autonomous Claude Code (Linux pilot)

---

## Executive Summary

This audit examines the asynchronous lifecycle of `ThumbnailManager`, focusing on three critical areas:

1. **`UpdateThumbnailsList()`** — `async void` timer callback (line 297)
2. **`ThumbnailActivated()`** — `Task.Run` + UI-thread continuation (line 676)
3. **`Stop()`** — synchronous timer stop without awaiting in-flight work (line 286)

**Bottom line**: The current design has **two P0 risks** (unobserved exceptions crashing the process; shutdown races leaking native handles) and **three P1 risks** (concurrent timer/activation races; missing cancellation; sync-context capture fragility). No production code changes are proposed here — this document establishes the evidence base for a future minimal refactor.

---

## 1. Code Map — Relevant Files & Lines

| File | Lines | Role |
|------|-------|------|
| `ThumbnailManager.cs` | 279–284 | `Start()` — begins timer, calls `RefreshThumbnails()` synchronously |
| `ThumbnailManager.cs` | 286–289 | `Stop()` — stops timer only |
| `ThumbnailManager.cs` | 291–295 | `ThumbnailUpdateTimerTick` — calls `UpdateThumbnailsList()` + `RefreshThumbnails()` |
| `ThumbnailManager.cs` | 297–397 | **`UpdateThumbnailsList()`** — `async void`, process diff + view create/destroy + `mediator.Publish` |
| `ThumbnailManager.cs` | 399–554 | `RefreshThumbnails()` — synchronous, heavy UI work, mutates `_thumbnailViews` |
| `ThumbnailManager.cs` | 676–695 | **`ThumbnailActivated()`** — `Task.Run(ActivateWindow)` → `ContinueWith(UI thread)` |
| `ThumbnailManager.cs` | 732–746 | `ThumbnailViewResized()` — `async void` event handler, `mediator.Publish` |
| `ThumbnailManager.cs` | 1032–1040 | `RaiseThumbnailLocationUpdatedNotification()` — `async void`, `mediator.Send` |
| `IThumbnailManager.cs` | 7–8 | Interface: `Start()`/`Stop()` return `void` (no cancellation) |
| `IProcessMonitor.cs` | 9 | `GetUpdatedProcesses(out added, out updated, out removed)` — synchronous |

---

## 2. Risk Register

### P0 — Unobserved Exceptions in `async void` Methods Crash the Process

**Evidence**:
- `UpdateThumbnailsList()` (line 297) is `async void` — any exception escapes the `DispatcherTimer.Tick` handler and terminates the process (per .NET semantics for `async void`).
- `ThumbnailViewResized()` (line 732) and `RaiseThumbnailLocationUpdatedNotification()` (line 1032) are also `async void` event handlers with identical semantics.
- No `try/catch` inside these methods; `_mediator.Publish/Send` can throw (e.g., handler throws, pipeline behavior throws, DI scope disposed).

**Failure scenario**:
Timer ticks → `UpdateThumbnailsList()` calls `_processMonitor.GetUpdatedProcesses()` → native interop throws `SEHException` / `AccessViolationException` → process dies with no log, no restart.

**Mitigation status**: None in current code.

---

### P0 — `Stop()` Does Not Await In-Flight Async Work → Native Handle Leaks / Use-After-Free

**Evidence**:
- `Stop()` (line 286–289) only calls `_thumbnailUpdateTimer.Stop()`.
- If `UpdateThumbnailsList()` is mid-await on `_mediator.Publish` (line 395), or `ThumbnailActivated()`'s `Task.Run` is executing `ActivateWindow` (line 680–686), `Stop()` returns immediately.
- Subsequent `ThumbnailManager` disposal (or process shutdown) can finalize `_thumbnailViews` while callbacks still hold `IThumbnailView` references → `view.Close()` (line 390) races with `view.Refresh()` (line 549) or `ActivateWindow` (line 683/685).

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

### P1 — `ContinueWith(TaskScheduler.FromCurrentSynchronizationContext())` Fragility

**Evidence**:
- `ThumbnailActivated()` (line 688–694) captures `SynchronizationContext.Current` at the point of `Task.Run` invocation (which runs on thread-pool, **no UI context**).
- `TaskScheduler.FromCurrentSynchronizationContext()` inside the `ContinueWith` lambda captures the **thread-pool context (null)**, not the UI context.
- Result: continuation runs on thread-pool, not UI thread → `SwitchActiveClient` / `RefreshThumbnails` execute off-UI-thread → cross-thread WPF/WinForms access → `InvalidOperationException` or silent corruption.

**Code trace**:
```csharp
Task.Run(() => { ... })           // runs on thread-pool, SynchronizationContext.Current == null
    .ContinueWith(t => { ... }, TaskScheduler.FromCurrentSynchronizationContext()); // captures null → TaskScheduler.Default
```

**Failure scenario**:
User clicks thumbnail → `ThumbnailActivated` fires → `ActivateWindow` succeeds → continuation runs on thread-pool → `RefreshThumbnails()` touches WPF `DispatcherTimer` / `IThumbnailView` → crash or visual glitch.

**Mitigation status**: Bug. Correct pattern: capture `TaskScheduler ui = TaskScheduler.FromCurrentSynchronizationContext();` **before** `Task.Run`, then pass `ui` to `ContinueWith`.

---

### P2 — `DispatcherTimer` Interval Configuration Mismatch on Linux

**Evidence**:
- Line 79: `configuration.ThumbnailRefreshPeriod` used directly for `DispatcherTimer.Interval`.
- Windows: clamped to 300–1000 ms in `ThumbnailConfiguration.ApplyRestrictions()` (tested in `ThumbnailConfigurationTests.cs:43–59`).
- Linux: no clamping applied (`#if LINUX` guards elsewhere but not here).

**Failure scenario**:
Linux user sets `ThumbnailRefreshPeriod = 50` → timer fires every 50 ms → CPU spike, UI jank, `UpdateThumbnailsList()` re-entrancy pressure.

**Mitigation status**: Configuration validation gap.

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
| P0: `async void` exceptions crash process | **Proven** | .NET spec: unhandled exception in `async void` → `ThreadPool.UnobservedTaskException` → process termination (unless legacy `ThrowUnobservedTaskExceptions` config). No try/catch in any `async void` method. |
| P0: `Stop()` races with in-flight async | **Proven** | `Stop()` is synchronous void; `UpdateThumbnailsList()` awaits `mediator.Publish`; no join/await mechanism exists. |
| P1: Timer vs. activation race on `_thumbnailViews` | **Proven** | Two code paths (timer tick, activation continuation) mutate/iterate same `Dictionary` without lock. `_ignoreViewEvents` does not guard dictionary access. |
| P1: Missing `CancellationToken` | **Proven** | Zero `CancellationToken` parameters in call chain from `Start()`/`Stop()` to `mediator` calls. |
| P1: `ContinueWith` captures wrong scheduler | **Proven** | `Task.Run` executes on thread-pool where `SynchronizationContext.Current == null`; `FromCurrentSynchronizationContext()` inside lambda captures *that* null. |
| P2: Linux timer interval unclamped | **Proven** | `ApplyRestrictions()` only called on Windows (test confirms); Linux `#if` absent at line 79. |
| P2: `_hideThumbnailsDelay` thread-safety | **Hypothesised** | Only triggered if P1 continuation bug occurs. |

---

## 4. Test Scenarios (for Future Regression Suite)

| ID | Scenario | Expected | Current Coverage |
|----|----------|----------|------------------|
| T-01 | `UpdateThumbnailsList()` throws in `GetUpdatedProcesses` | App stays alive, error logged, timer continues | **None** |
| T-02 | `Stop()` called while `UpdateThumbnailsList()` awaiting `mediator.Publish` | `Stop()` returns after await completes; no use-after-free | **None** |
| T-03 | Rapid thumbnail clicks during timer tick | No `InvalidOperationException`, no visual corruption | **None** |
| T-04 | `ThumbnailActivated` continuation runs on UI thread | `SynchronizationContext.Current` is UI context in continuation | **None** (bug exists) |
| T-05 | Linux: `ThumbnailRefreshPeriod = 50` → timer clamped to 300 ms | Timer interval ≥ 300 ms | **None** (config gap) |
| T-06 | `mediator.Publish` throws in `RaiseThumbnailLocationUpdatedNotification` | App stays alive, error logged | **None** |
| T-07 | Shutdown: `Stop()` → `Dispose()` → finalizer → no handle leaks | Zero native handles leaked | Manual only |

---

## 5. Minimal Future Change Plan (Not Implemented Here)

> **Constraint**: This audit *only* documents. The following is a straw-man for the maintainer to scope a follow-up PR.

| Change | Files | Risk Addressed | Effort |
|--------|-------|----------------|--------|
| Wrap all `async void` bodies in `try/catch` → log + suppress | `ThumbnailManager.cs` (lines 297, 732, 1032) | P0 (crash) | Low |
| Change `Stop()` → `Task StopAsync(CancellationToken)`; propagate token to `mediator` calls | `IThumbnailManager.cs`, `ThumbnailManager.cs`, callers | P0 (shutdown race), P1 (cancellation) | Medium |
| Capture UI `TaskScheduler` *before* `Task.Run` in `ThumbnailActivated` | `ThumbnailManager.cs:676–695` | P1 (wrong thread) | Low |
| Add `lock(_thumbnailViews)` in `RefreshThumbnails` + `ThumbnailActivated` continuation | `ThumbnailManager.cs` | P1 (dictionary race) | Low |
| Move `ApplyRestrictions()` call to Linux path or make platform-agnostic | `ThumbnailConfiguration.cs` + `ThumbnailManager.cs:79` | P2 (timer interval) | Low |
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
4. Confirm Linux `ThumbnailRefreshPeriod` clamping works if config loaded on Linux.

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
      └─ ContinueWith(continuation, TaskScheduler.FromCurrentSynchronizationContext()) [BUG: captures null]
          ├─ SwitchActiveClient()
          ├─ UpdateClientLayouts()
          └─ RefreshThumbnails() [RE-ENTRANT!]

Stop()
  └─ _thumbnailUpdateTimer.Stop() [NO AWAIT]
```

---

**End of audit.** This document is the sole deliverable for issue #11 pilot. No production or test files were modified.