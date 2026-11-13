# ThumbnailManager Performance Audit for 6+1 Multiboxing

**Document Version:** 1.1
**Date:** 2026-11-11 (v1.0), revised 2026-11-13 (v1.1)
**Scope:** Static code analysis of `ThumbnailManager` and related components for a 6+1 EVE Online multiboxing scenario (six regular clients on primary monitor, one intel/client on secondary monitor).
**Methodology:** Verified code observations from reading source at `main` = `cdf4721`; hypotheses marked explicitly. GDI/USER handle observations from the 13/11/2026 field runs are quoted with provenance (section 5.3). No CPU, allocation or soak profiling was executed.

> **Changes in v1.1:** corrected the thumbnail mode model (static vs live is a **global** mode, not "login screens"), corrected the `GetStaticThumbnail` cleanup claim (it **does** have `try/finally`; the exception-path leak was fixed in the GDI series #3/#4/#5), documented the capture path as it stands after #46/#47/#48, added the field handle measurements, and refreshed file sizes.

---

## Executive Summary

The `ThumbnailManager` coordinates up to 7 concurrent EVE clients at a default 500 ms refresh period (2 Hz). Each tick performs process enumeration, window-handle validation, per-thumbnail layout computation and per-thumbnail view updates; a forced refresh runs every 2nd tick.

The render path is a **global** choice, not per-thumbnail (`ThumbnailViewFactory.Create()`, `ThumbnailViewFactory.cs:18-23`):

- **Live mode** (`WineCompatibilityMode = false`, default on Windows): `LiveThumbnailView` / `DwmThumbnail`. 7 `HTHUMBNAIL` registrations, no GDI capture per tick.
- **Static mode** (`WineCompatibilityMode = true`): `StaticThumbnailView`. 0 DWM registrations; each thumbnail is captured from the client window on forced refreshes, always allocating a full-size `HBITMAP` and, when the frame comes back blank, performing a second capture through `PrintWindow` (section 2.3).

Persistent handles in both modes: 15 HWNDs (7 thumbnail forms + 7 overlays + 1 main window). The capture path cleans up `HDC`/`HBITMAP` through nested `try/finally` blocks and has explicit failure tests, so the exception-path leak reported in v1.0 no longer applies.

Remaining structural concerns are allocation churn on forced refreshes and O(n²) snap/hover work — harmless at n=7, a ceiling only if the refresh period drops or the client count grows.

---

## 1. Thumbnail Refresh Frequency

### 1.1 Verified Configuration

| Setting | Default (Windows) | Clamped Range (Windows) | Clamped Range (Linux/Wine) |
|---------|-------------------|-------------------------|----------------------------|
| `ThumbnailRefreshPeriod` | 500 ms | 300–1000 ms | 10–1000 ms |
| `FORCED_REFRESH_CYCLE_THRESHOLD` | 2 cycles | constant | constant |
| `HideThumbnailsDelay` | 2 cycles | configurable | configurable |

*Source: `ThumbnailConfiguration.cs:106` (default), `ThumbnailConfiguration.cs:544-548` (clamps), `ThumbnailManager.cs:27` (threshold)*

### 1.2 Observed Timer Behavior

- `DispatcherTimer` fires every `ThumbnailRefreshPeriod` ms (`ThumbnailManager.cs:77-79`).
- Each tick invokes `UpdateThumbnailsList()` (async) followed by `RefreshThumbnails()` (synchronous).
- A forced full refresh occurs every 2nd tick (`ThumbnailManager.cs:806-809`).

### 1.3 Hypothesis: Effective Refresh Rate Under Load

If `UpdateThumbnailsList()` or `RefreshThumbnails()` exceeds the timer interval, `DispatcherTimer` will queue ticks (WPF dispatcher behavior). At 300 ms minimum period with 7 clients, worst-case per-tick work may approach or exceed the budget, causing timer drift. **No benchmark data exists to confirm or refute this.**

---

## 2. Memory Allocations

### 2.1 Verified Allocation Sites Per Refresh Tick

| Location | Allocation Type | Frequency | Notes |
|----------|----------------|-----------|-------|
| `WindowManager.GetStaticThumbnail()` (`WindowManager.cs:298-431`) | `HDC`, `HBITMAP`, managed `Image` (`Image.FromHbitmap`) | Every forced refresh, per static thumbnail | All GDI objects are released in `finally`; the managed image is returned to the view and disposed there |
| `WindowManager.CaptureCompositedWindow()` (`WindowManager.cs:472-500`) | One extra `Image` plus a second capture pass | Only when the `BitBlt` frame is blank | See 2.3 |
| `DwmThumbnail.Register()` (`DwmThumbnail.cs:39`) | Native DWM thumbnail handle (`HTHUMBNAIL`) | On client add / force refresh | 1 handle per live thumbnail |
| `ThumbnailView.Refresh()` → `HighlightThumbnail()` → `ResizeThumbnail()` | Structs (`Point`, `Size`, `Rectangle`) | Every tick for visible thumbnails | Small, stack-allocated, low pressure |
| `SnapThumbnailView()` (`ThumbnailManager.cs:807-859` in v1.0; now `:834-858`) | `Point[]` arrays (2×4 elements) | Every tick when hover active & location change dequeued | O(n) allocations per snapped thumbnail |
| `UpdateClientLayouts()` | `ClientLayout` objects, tuples | Every tick when layout tracking enabled | 1 `ClientLayout` per managed client |

### 2.2 GC Pressure Estimate (Hypothetical)

For 6+1 **in static mode**, per second at 500 ms period (2 captures per second per thumbnail, since forced refresh runs every 2nd tick):

- **Gen 0**: up to ~14 `Image` + ~14 `HBITMAP` + ~14 `HDC` per second across 7 thumbnails, in the worst case where every capture lands on a forced refresh.
- **Live mode**: no per-tick capture allocations; the cost is `DwmUpdateThumbnailProperties` calls.
- **Native handles**: 7 DWM thumbnail handles (live mode only) + 15 HWNDs held for the session lifetime.

> **Hypothesis**: At 300 ms period, Gen 0 collections could roughly double. No allocation profiling was performed.

### 2.3 The Capture Path After #46/#47/#48 (Verified)

Static capture is not a bare `BitBlt` anymore:

1. `BitBlt` from the window DC (cheap path, correct for ordinary windows).
2. If the resulting frame is **flat** — all-black or all-white, sampled on a 5×5 grid (`IsBlankSurface`, `WindowManager.cs:437-470`) — retry with `PrintWindow(PW_CLIENTONLY | PW_RENDERFULLCONTENT)`, which reads the DWM-composited content.
3. If `PrintWindow` fails, the `BitBlt` image is kept.

Cost implication for a 6+1 setup: 25 `GetPixel` samples per capture are constant, but every capture that lands on a DirectX client that is not in the foreground pays **two** full-size captures (2560×1080 `HBITMAP` allocated once, filled twice) plus a `PrintWindow` round-trip to the client. In the field runs that was the normal case for 6 of 7 clients whenever the layout is stacked.

This is the main new performance question opened by #46: correctness required the retry, but the retry is expensive and currently runs on every forced refresh for every covered client. **No CPU measurement of the retry exists yet** (see section 8).

---

## 3. GDI / USER Handle Usage

### 3.1 Verified Handle Inventory (6+1 Scenario)

| Handle Type | Count | Mode | Lifetime | Creation Site |
|-------------|-------|------|----------|---------------|
| HWND (ThumbnailView Form) | 7 | both | Session | `ThumbnailViewFactory.Create()` |
| HWND (ThumbnailOverlay Form) | 7 | both | Session | `ThumbnailView` constructor |
| HWND (Main application window) | 1 | both | Session | WPF `Window` |
| DWM Thumbnail (`HTHUMBNAIL`) | ≤7 | live only | Per-client session | `DwmThumbnail.Register()` |
| HDC (`GetDC`) | Transient | static only | Per capture | `WindowManager.GetStaticThumbnail()` |
| HBITMAP (`CreateCompatibleBitmap`) | Transient | static only | Per capture | `WindowManager.GetStaticThumbnail()` |
| HDC (`CreateCompatibleDC`) | Transient | static only | Per capture | `WindowManager.GetStaticThumbnail()` |

**Total persistent USER handles**: 15 HWNDs.
**Total persistent GDI handles**: 0 (transient only).
**Total DWM handles**: ≤7, live mode only.

> Windows default USER handle quota: 10,000 per process. 15 HWNDs is well within limits.
> DWM thumbnail limit: undocumented, but 7 is safe.

### 3.2 Handle Leak Risk Points

1. **`DwmThumbnail.Unregister()`** (`DwmThumbnail.cs:58-75`) swallows `ArgumentException` and `COMException`. If `DwmUnregisterThumbnail` fails, the native handle may leak until process exit. **Verified, still present.**
2. **`GetStaticThumbnail()`** — v1.0 claimed "no `try/finally`; any exception leaks HDC/HBITMAP". **That is no longer true and was already wrong for `main`:** the method has nested `try/finally` blocks (`WindowManager.cs:313-431`) that attempt every cleanup step in order (restore bitmap → delete DC → delete object → release source DC), plus a `catch` that disposes a partially created image. `GdiFailureTests` covers each failure point (16 cases) and the cleanup ordering.
3. **`ThumbnailView.Close()`** — closes the overlay and the form but does not explicitly dispose the `PictureBox` image; the managed `Image` from `FromHbitmap` is released by the GC. **Verified behavior; leak impact not measured.**

---

## 4. CPU Overhead

### 4.1 Verified Per-Tick Work (RefreshThumbnails)

```csharp
// ThumbnailManager.cs (RefreshThumbnails)
RefreshThumbnails() {
    1. GetForegroundWindowHandle()                    // 1x User32 call
    2. IsClientWindowActive() -> iterates 7 thumbnails // 7x IsKnownHandle()
    3. IsMainWindowActive()                           // 1x handle compare
    4. hideAllThumbnails logic                        // O(1)
    5. _refreshCycleCount++ & forceRefresh calc       // O(1)
    6. DisableViewEvents()                            // O(1)
    7. TryDequeueLocationChange()                     // O(1) lock + tuple copy
    8. FOREACH thumbnail (7x):
         a. GetZoomAnchor()                           // Dictionary lookup
         b. Hide/Show/IsThumbnailDisabled checks      // O(1)
         c. GetThumbnailLocation()                    // Dictionary lookup(s)
         d. GetThumbnailSize()                        // Dictionary lookup
         e. SetOpacity()                              // Property set + overlay Opacity
         f. SetTopMost()                              // 2x property sets (form + overlay)
         g. SetHighlight()                            // BackColor set + ResizeThumbnail()
         h. view.Refresh(forceRefresh)                // -> mode-specific work
    9. EnableViewEvents()
}
```

### 4.2 Verified Per-Tick Work (UpdateThumbnailsList)

```csharp
// ThumbnailManager.cs (UpdateThumbnailsList)
UpdateThumbnailsList() {
    1. _processMonitor.GetUpdatedProcesses()         // Process enumeration
    2. FOREACH added process:
         a. ThumbnailViewFactory.Create()             // new Form + Overlay + controls
         b. Event subscriptions (6 delegates)
         c. RegisterHotkey()                          // Global hotkey registration
         d. ApplyClientLayout() -> GetWindowPosition()
         e. ApplyCaptionBar() -> SetWindowLong()
    3. FOREACH updated process: title change handling
    4. FOREACH removed process:
         a. UnregisterHotkey()
         b. Event unsubscriptions
         c. view.Close()
    5. Mediator.Publish(ThumbnailListUpdated)        // Async, if list changed
}
```

### 4.3 O(n²) / Quadratic Behaviors (Verified)

| Location | Complexity | Trigger |
|----------|------------|---------|
| `SnapThumbnailView()` (`ThumbnailManager.cs:834-858`) | O(n²) per snapped thumbnail (n=7 → 49 point-pair checks) | Mouse hover + location change dequeued |
| `CycleNextClient()` (`ThumbnailManager.cs:137-236`) | O(n²) worst-case (nested loops over cycle order × thumbnails) | Hotkey press |
| `IsClientWindowActive()` | O(n) per tick (n=7) | Every refresh tick |

> With n=7, O(n²) = 49 iterations — negligible. Becomes relevant if the client count grows or the refresh period drops to ≤100 ms.

### 4.4 DWM / User32 / GDI Call Volume (Verified)

Per refresh tick (steady state, 7 thumbnails):

| Call | Live mode | Static mode |
|------|-----------|-------------|
| `DwmUpdateThumbnailProperties` | ≤7 | 0 |
| `SetWindowPos` (via `Form.Location/Size`) | ≤14 (thumbnail + overlay) | ≤14 |
| `GetForegroundWindow` | 1 | 1 |
| `GetDC` / `CreateCompatibleBitmap` / `BitBlt` | 0 | up to 7 on forced refresh; a second pass per blank frame via `PrintWindow` |
| `GetPixel` (blank-surface sampling) | 0 | 25 per capture |

---

## 5. 6+1 Specific Considerations

### 5.1 Verified Behavioral Differences

| Aspect | Primary Monitor (6 clients) | Secondary Monitor (1 intel) |
|--------|----------------------------|-----------------------------|
| `GetThumbnailLocation` | Per-client layout dictionary (`PerClientLayout[activeClient][currentClient]`) | Falls back to `FlatLayout` or `LoginThumbnailLocation` |
| `ThumbnailSize` | `PerClientThumbnailSize` or global `ThumbnailSize` | Same |
| DPI scaling | Primary monitor DPI | Secondary monitor DPI (may differ) |
| `ShowThumbnailsAlwaysOnTop` | Global setting applies | Same |
| `HideActiveClientThumbnail` | Hides active client's thumbnail | Same logic |

### 5.2 Hypothesis: Cross-Monitor DWM Performance

- DWM thumbnail composition crosses monitor boundaries natively.
- If the secondary monitor has a different refresh rate or DPI, `DwmUpdateThumbnailProperties` may incur additional compositor work.
- No code handles monitor affinity; thumbnails follow the destination window (`ThumbnailView` HWND), which resides on whichever monitor the user places it.
- **Unverified**: whether 6+1 with mixed DPI causes visible lag or bitmap stretching.

### 5.3 Field Measurements, 13/11/2026 (Static Mode)

Observations from the validation runs recorded in `docs/audits/gdi-validacao-cliente-real.md` (sections 4.1 and 4.2) and issues #46/#47/#48. Environment: Windows 11 Pro 26H2 build 26300, AMD RX 7600 (driver 32.0.32015.7029), 2560×1080 + 1920×1080, DPI 100%, EVE in windowed mode, 7 clients (6 primary + 1 secondary).

| Build | State | GDI | USER | Thumbnails with content |
|---|---|---|---|---|
| `e3a1720` (before the capture fix) | baseline / 20 cycles / minimized / restored / idle | 75 | 87–88 | 1/7 |
| `e3a1720` | after a synthetic thumbnail click | 78 | 90 | 1/7 |
| `#48` (current capture path) | baseline / 20 cycles / minimized / restored | 75 | 78–88 | **7/7** |
| `#48` | after a synthetic thumbnail click | 75–78 | 90–91 | **7/7** |
| `#48` | after 20 s idle | 75–78 | 89–90 | **7/7** |

What this supports:

- Handle count is stable across 40 hotkey cycles, a minimize/restore of all clients, a click and 20 s idle: **GDI delta 0**, USER +2/+3 with the peaks decaying on the next reading.
- The static capture path does not grow handles per capture in this window.

What this does **not** support:

- CPU cost of `PrintWindow` retries (the dominant new cost, section 2.3) — not measured.
- Gen 0/1/2 behavior, timer drift, or behavior beyond a few minutes; no soak test.
- DWM handle counts in live mode.

---

## 6. Verified Code Observations vs. Hypotheses

| # | Statement | Classification | Evidence |
|---|-----------|----------------|----------|
| 1 | Default refresh period is 500 ms (2 Hz) | **Verified** | `ThumbnailConfiguration.cs:106` |
| 2 | Windows clamp is 300–1000 ms; Linux/Wine 10–1000 ms | **Verified** | `ThumbnailConfiguration.cs:544-548` |
| 3 | Forced refresh occurs every 2nd tick | **Verified** | `ThumbnailManager.cs:27, 806-809` |
| 4 | Static vs live is a **global** mode chosen by `EnableWineCompatibilityMode` | **Verified** | `ThumbnailViewFactory.cs:18-23` |
| 5 | `GetStaticThumbnail` allocates GDI objects per call and releases them in `finally` | **Verified** | `WindowManager.cs:298-431`, `GdiFailureTests` |
| 6 | Static capture retries with `PrintWindow` when the frame is blank | **Verified** | `WindowManager.cs:365-390, 437-500` |
| 7 | `DwmThumbnail.Unregister` swallows exceptions | **Verified** | `DwmThumbnail.cs:58-75` |
| 8 | 6+1 uses 15 HWNDs (7 thumbnail + 7 overlay + 1 main) | **Verified** | `ThumbnailView` + `ThumbnailOverlay` are `Form` subclasses |
| 9 | `SnapThumbnailView` is O(n²) per snapped view | **Verified** | `ThumbnailManager.cs:834-858` |
| 10 | Handles stay flat over 40 cycles + minimize/restore + idle (static mode) | **Observed (field)** | Section 5.3 |
| 11 | Timer drift may occur at 300 ms under load | **Hypothesis** | `DispatcherTimer` behavior; no measurements |
| 12 | Gen 0 GC pressure doubles at 300 ms vs 500 ms | **Hypothesis** | Proportional allocation rate; no profiling |
| 13 | `PrintWindow` retries dominate CPU in static mode with stacked clients | **Hypothesis** | Cost is plausible from the call volume in 4.4; not measured |
| 14 | Cross-monitor DPI causes DWM compositor overhead | **Hypothesis** | DWM architecture; no code evidence |
| 15 | Hotkey registration per thumbnail adds USER handles | **Verified** | `HotkeyHandler.Register()` → `RegisterHotKey` |

*Correction note: v1.0 row 11 ("`GetStaticThumbnail` handle leak on exception") was based on the pre-#3/#4/#5 code and is wrong for `main`; it is replaced by row 5.*

---

## 7. Recommended Improvements (Non-Breaking)

### 7.1 Configuration Tuning (Zero Code Change)

| Recommendation | Rationale | Risk |
|----------------|-----------|------|
| Keep `ThumbnailRefreshPeriod >= 500 ms` for 6+1 | Reduces timer drift risk, halves capture rate | Slightly less responsive thumbnails |
| Prefer live (DWM) mode on Windows | Avoids the whole capture path, including `PrintWindow` retries | Not available under Wine |
| Disable `EnableThumbnailSnap` if not used | Eliminates O(n²) snap checks per tick | Loses magnetic docking |
| Disable `ShowThumbnailOverlays` for the intel client | Removes overlay paint overhead | Loses label/indicator |
| Set `HideLoginClientThumbnail = true` | Skips login clients entirely | Login clients invisible in preview |

### 7.2 Code-Level Improvements (For Future PRs)

| Area | Improvement | Effort | Status |
|------|-------------|--------|--------|
| `GetStaticThumbnail` | `try/finally` / `SafeHandle` cleanup | Low | **Done** (GDI series #3/#4/#5) |
| Blank retry | Measure `PrintWindow` cost; consider caching the last good frame per client and retrying at a lower rate (e.g. every N forced refreshes) | Medium | **Open** — opened by #46 |
| `DwmThumbnail.Unregister` | Log swallowed exceptions; consider retry | Low | Open |
| `SnapThumbnailView` | Spatial index (grid) for O(n log n) neighbor search | Medium | Open |
| `RefreshThumbnails` | Split into `UpdateLayout()` + `UpdateVisibility()` + `UpdateContent()`; skip unchanged thumbnails via dirty flags | Medium | Open |
| `UpdateThumbnailsList` | Debounce process enumeration; cache the process list for 1–2 ticks | Low | Open |
| `IsClientWindowActive` | `HashSet<IntPtr>` of known handles for O(1) lookup | Low | Open |
| `ThumbnailView` | Dispose the `PictureBox` image in `Close()` | Low | Open |
| Force refresh | Make `FORCED_REFRESH_CYCLE_THRESHOLD` configurable; raise to 5–10 for stable sessions | Low | Open |

### 7.3 Architectural Considerations (Out of Scope for This Audit)

- Replace `DispatcherTimer` with `System.Threading.PeriodicTimer` (net8) for a precise period without dispatcher queue buildup.
- Move thumbnail capture to a background thread; marshal only bitmap/DWM updates to the UI thread.
- Pool `Bitmap`/`Graphics` objects for the static path to reduce Gen 0 pressure.
- Reuse a single `HBITMAP` per thumbnail instead of reallocating on every capture.

---

## 8. What Still Needs Measurement on Windows

The following cannot be settled by reading code, and need the Windows self-hosted runner or the real client session:

1. **`PrintWindow` retry cost (static mode)** — per-capture duration with 6 stacked clients, and whether it affects EVE frame rate. Measure with `dotnet-trace`/ETW around `CaptureCompositedWindow`, or by timing captures in a harness.
2. **Per-tick CPU time** (`RefreshThumbnails`) at 500 ms and 300 ms with 7 clients.
3. **GC behavior** — Gen 0/1/2 rates at 500 ms vs 300 ms (`dotnet-counters`).
4. **Soak test** — handle count over 30+ minutes with the retry path active, not just the short validation windows in 5.3.
5. **DWM thumbnail handle count** in live mode, and cross-monitor DPI behavior.
6. **Hotkey registration limits** — `RegisterHotKey` atom table pressure with 7 clients.

Windows-only paths are already covered by the `Windows behavior suite` (self-hosted runner, stress GDI of 1,000 captures) and by the manual roteiro in `docs/audits/gdi-validacao-cliente-real.md`.

---

## 9. Files Examined

| File | Lines (`cdf4721`) | Purpose |
|------|-------------------|---------|
| `src/Eve-O-Preview/Services/Implementation/ThumbnailManager.cs` | 1224 | Core refresh loop, client tracking, layout, snap |
| `src/Eve-O-Preview/Services/Interface/IThumbnailManager.cs` | 17 | Interface definition |
| `src/Eve-O-Preview/View/Implementation/ThumbnailView.cs` | 802 | Base thumbnail form, overlay, events, zoom, highlight |
| `src/Eve-O-Preview/View/Implementation/LiveThumbnailView.cs` | 69 | DWM live thumbnail wrapper |
| `src/Eve-O-Preview/View/Implementation/StaticThumbnailView.cs` | 100 | GDI static thumbnail (whole static mode, not only login screens) |
| `src/Eve-O-Preview/View/Implementation/ThumbnailOverlay.cs` | 308 | Overlay form (label, cycle indicator, fake preview) |
| `src/Eve-O-Preview/View/Implementation/ThumbnailViewFactory.cs` | 26 | Factory that selects live vs static view |
| `src/Eve-O-Preview/Services/Implementation/WindowManager.cs` | 432 | DWM/GDI interop, static capture, blank retry |
| `src/Eve-O-Preview/Services/Interop/StaticThumbnailGdi.cs` | 49 | GDI + `PrintWindow` interop surface |
| `src/Eve-O-Preview/Services/Implementation/DwmThumbnail.cs` | 103 | DWM thumbnail registration & property updates |
| `src/Eve-O-Preview/Configuration/Implementation/ThumbnailConfiguration.cs` | 511 | All tunable parameters, clamping, persistence |

---

## 10. Conclusion

The `ThumbnailManager` is **functional and stable at default settings** for a 6+1 workload. The remaining performance risks, in order:

1. **`PrintWindow` retry cost in static mode** — correct for the #46 bug, but a second full-size capture per covered client per forced refresh was never measured.
2. **Timer drift** if the refresh period is lowered to 300 ms without profiling.
3. **Unlogged DWM unregistration failures** leaving orphaned thumbnail handles (live mode).
4. **O(n²) snap logic** — harmless at n=7, a scaling ceiling beyond that.

The GDI exception-path leak listed in v1.0 is **not a risk anymore**: the capture path has ordered `try/finally` cleanup and failure tests, and the 13/11/2026 field runs showed flat handle counts across the exercised states.

All recommendations remain non-breaking configuration changes or isolated refactorings; no production code change is proposed by this audit.

---

*End of Audit*
