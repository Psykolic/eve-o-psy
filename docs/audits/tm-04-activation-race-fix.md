# TM-04 — ThumbnailActivated race-hardening fix

Date: 2026-11-11
Scope: `src/Eve-O-Preview/Services/Implementation/ThumbnailManager.cs` (`ThumbnailActivated(IntPtr id)`), regression tests in `src/EveO.Tests/Services/ThumbnailManagerActivationTests.cs`.
Builds on: #12 (async audit), #14 (6+1 performance audit). Independent of Draft PR #9 (TM-01).

## Problem (verified against source)

`ThumbnailActivated(IntPtr id)` is the click-to-activate entry point, wired as `view.ThumbnailActivated = this.ThumbnailActivated` in `UpdateThumbnailsList()`. Before this patch it:

1. Indexed `_thumbnailViews[id]` without a guard — a view removed between the event firing and the handler running (or after rapid 6+1 churn re-keyed the dictionary) raised `KeyNotFoundException` on the UI thread.
2. Started `Task.Run(...ActivateWindow...)` and continued with `TaskScheduler.FromCurrentSynchronizationContext()`, but the continuation ran **even when activation faulted or was cancelled**, and it captured the view instance without re-checking that it was still registered — so a stale/disposed view could be observed after the client closed mid-switch.

## Scheduler note (correction to audit #12)

`TaskScheduler.FromCurrentSynchronizationContext()` is evaluated **when `ContinueWith` is registered** — normally on the UI thread inside this event handler — not inside the `Task.Run` delegate. It was kept unchanged. All UI-only work (`SwitchActiveClient`, `UpdateClientLayouts`, `RefreshThumbnails`) remains marshaled to the UI thread via that scheduler. Audit #12's claim that the scheduler "captures the thread-pool context" is not repeated here.

## Behavior reviewed against source and regression tests

The following describes source behavior; five tests cover the cases listed below except cancellation. Tests use a queued `SynchronizationContext` to drive the UI continuation deterministically — no WPF dispatcher is required for these paths.

- **Unknown id**: `ThumbnailActivated` returns silently at entry; `ActivateWindow` is never called and no UI continuation is queued. Ordinary click-to-activate for known ids is unchanged.
- **View removed before continuation**: continuation re-checks `_thumbnailViews.TryGetValue(id, ...)`; on failure it returns without calling `SwitchActiveClient` / `UpdateClientLayouts` / `RefreshThumbnails` and without touching the stale view.
- **View replaced before continuation**: re-check also compares instance identity (`ReferenceEquals`) against the view captured at invocation time; a replacement registered under the same handle is not treated as the same client and is not mutated.
- **Faulted activation** (`ActivateWindow` throws): the continuation observes the exception (`task.Exception` is read and its base message is written via `Debug.WriteLine`) and returns early — no apparently successful UI state transition occurs (`_activeClient` unchanged, `RefreshThumbnails` not run).
- **Cancelled activation**: continuation returns early without mutating state (source review only; no dedicated test).
- **Successful activation**: client becomes the active client (`GetActiveClient()` returns the view) and `RefreshThumbnails` runs (observed via `GetForegroundWindowHandle` being called). UI work stays on the UI continuation.

### Error-observation rationale

The project has **no shared logging abstraction**. `WindowManager` keeps a private, file-append `WriteToLog` that is effectively LINUX-oriented and not reusable (private, hard-coded file name, LINUX call sites). Introducing a logger dependency is out of scope for this fix. The faulted branch therefore observes the exception (preventing an unobserved-task-exception) and surfaces it via `System.Diagnostics.Debug.WriteLine`. Exceptions are **not** swallowed silently; the skip of the state transition is the deliberate behavior, documented here and in code.

## Suspected / not yet verified (do NOT treat as confirmed)

- Whether `ActivateWindow` itself can intermittently fail on real Windows 11 with 6+1 clients (DWM/`SetForegroundWindow` focus-stealing rules). The faulted path is now handled; the frequency is unknown and requires manual 6+1 validation.
- Whether a view object can actually be observed in a disposed state by the continuation in production (the fix treats both "removed" and "replaced" as terminal; true disposal races depend on `ThumbnailView.Dispose` semantics, not audited here).
- Interaction with Draft PR #9 (TM-01): see [the follow-up compatibility review](tm-04-pr16-validation-review.md). Its production hunks and TM-04 compose in either order against the common base after line-ending normalization; a combined build and real lifecycle validation remain pending.
- GDI/WindowManager behavior (PRs #3–#5) is untouched.

## What this does NOT fix

- No TM-01 items (refresh try/finally, TryRemoveThumbnail, callback detach, null hotkey handling).
- No changes to cycling semantics, focus stealing, minimize behavior, or thumbnail layout.
- No new dependencies; no project/config/CI changes.

## Test coverage and CI status

- `src/EveO.Tests/Services/ThumbnailManagerActivationTests.cs` — 5 tests:
  unknown id, view removed before continuation, view replaced before continuation, faulted activation, successful activation.
- Tests target `net8.0-windows8.0` and must run on Windows (WinForms/WPF). Original Windows CI run [43092665611](https://github.com/Psykolic/eve-o-custom/actions/runs/43092665611) passed build and all 12 tests (zero failures/skips). Follow-up nullable-only changes and current CI evidence are tracked in [the validation review](tm-04-pr16-validation-review.md). No local execution or manual 6+1 validation is claimed.
- Known limitation: tests reflect on private `ThumbnailManager._thumbnailViews` and `ThumbnailActivated`. Renaming either breaks the tests (they fail loudly with a descriptive message).
- `RefreshThumbnails` is short-circuited in tests via `GetForegroundWindowHandle() == IntPtr.Zero`; real refresh behavior over multiple views is not exercised by these unit tests.

## Manual Windows 11 6+1 validation steps (NOT performed)

1. Launch 6 regular EVE clients on the primary monitor and the intel-only client on the secondary monitor; start EVE-O-Preview.
2. Repeated rapid clicks across thumbnails of all 7 clients (mouse and click-through if applicable): each click should activate the intended client; no crash, no exception dialog; active-client border follows the clicked client.
3. Close one client window (via in-game quit or Task Manager) **while rapidly clicking through clients**; verify no crash, remaining clients still activate, and the closed client's thumbnail disappears within one refresh cycle.
4. During step 3, watch for a stale-activation symptom: a closed client briefly becoming "active" or its thumbnail refusing to vanish — report either as a TM-04 failure.
5. Verify the intel-only client on the secondary monitor activates independently and is not affected by primary-monitor switching.
6. Verify ordinary single-click activation after an idle period (regression check of normal click-to-activate).
7. Check Debug output (Debug build under a debugger) for `ThumbnailActivated: ActivateWindow failed ...` messages; note frequency and correlate with step 2–4 to confirm the faulted path behaves (no ghost active client).
