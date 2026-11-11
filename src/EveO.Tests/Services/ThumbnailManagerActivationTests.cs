// Regression tests for ThumbnailManager.ThumbnailActivated (TM-04)
//
// These tests exercise the private ThumbnailActivated event handler via reflection and
// install a queued SynchronizationContext on the calling thread so the UI continuation
// (registered with TaskScheduler.FromCurrentSynchronizationContext()) can be dequeued and
// run deterministically on the test thread - no WPF dispatcher is required for these paths.
//
// NOTE: ThumbnailManager itself creates a DispatcherTimer in its constructor, so running
// these tests on a headless machine requires only the Windows Desktop runtime that ships
// with net8.0-windows. The refresh path observed after a successful activation is short-
// circuited by mocking GetForegroundWindowHandle() to return IntPtr.Zero (documented in
// production code), which avoids any real DWM/GDI/window interaction.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using EveOPreview.Configuration;
using EveOPreview.Services;
using EveOPreview.View;
using MediatR;
using Xunit;

namespace EveO.Tests.Services
{
    public class ThumbnailManagerActivationTests
    {
        private static readonly TimeSpan ContinuationTimeout = TimeSpan.FromSeconds(5);

        // A SynchronizationContext whose Post callbacks are queued instead of being executed,
        // so the test can invoke the captured continuation explicitly and deterministically.
        private sealed class QueuedSynchronizationContext : SynchronizationContext
        {
            private readonly BlockingCollection<KeyValuePair<SendOrPostCallback, object?>> _queue =
                new BlockingCollection<KeyValuePair<SendOrPostCallback, object?>>();

            public override void Post(SendOrPostCallback d, object? state)
            {
                _queue.Add(new KeyValuePair<SendOrPostCallback, object?>(d, state));
            }

            public override void Send(SendOrPostCallback d, object? state)
            {
                d(state);
            }

            public bool TryDequeue(out KeyValuePair<SendOrPostCallback, object?> item)
            {
                return _queue.TryTake(out item, ContinuationTimeout);
            }

            public bool TryDequeueNoWait(out KeyValuePair<SendOrPostCallback, object?> item)
            {
                return _queue.TryTake(out item, TimeSpan.FromMilliseconds(250));
            }
        }

        private sealed class TestHarness
        {
            public Mock<IMediator> Mediator { get; } = new Mock<IMediator>(MockBehavior.Loose);
            public Mock<IThumbnailConfiguration> Configuration { get; } = new Mock<IThumbnailConfiguration>(MockBehavior.Loose);
            public Mock<IProcessMonitor> ProcessMonitor { get; } = new Mock<IProcessMonitor>(MockBehavior.Loose);
            public Mock<IWindowManager> WindowManager { get; } = new Mock<IWindowManager>(MockBehavior.Loose);
            public Mock<IThumbnailViewFactory> ViewFactory { get; } = new Mock<IThumbnailViewFactory>(MockBehavior.Loose);
            public ThumbnailManager Manager { get; }

            public TestHarness()
            {
                // The constructor iterates the hotkey lists; keep them empty to avoid
                // registering real hotkeys against the test runner session.
                this.SetupEmptyHotkeyLists();

                // A zero foreground handle makes RefreshThumbnails() return immediately,
                // before any view iteration or window manager interaction.
                this.WindowManager.Setup(w => w.GetForegroundWindowHandle()).Returns(IntPtr.Zero);
                this.Configuration.SetupGet(c => c.EnableClientLayoutTracking).Returns(false);
                this.Configuration.SetupGet(c => c.MinimizeInactiveClients).Returns(false);

                this.Manager = new ThumbnailManager(
                    this.Mediator.Object,
                    this.Configuration.Object,
                    this.ProcessMonitor.Object,
                    this.WindowManager.Object,
                    this.ViewFactory.Object);
            }

            private void SetupEmptyHotkeyLists()
            {
                this.Configuration.SetupGet(c => c.CycleGroup1ForwardHotkeys).Returns(new List<string>());
                this.Configuration.SetupGet(c => c.CycleGroup1BackwardHotkeys).Returns(new List<string>());
                this.Configuration.SetupGet(c => c.CycleGroup2ForwardHotkeys).Returns(new List<string>());
                this.Configuration.SetupGet(c => c.CycleGroup2BackwardHotkeys).Returns(new List<string>());
                this.Configuration.SetupGet(c => c.CycleGroup3ForwardHotkeys).Returns(new List<string>());
                this.Configuration.SetupGet(c => c.CycleGroup3BackwardHotkeys).Returns(new List<string>());
                this.Configuration.SetupGet(c => c.CycleGroup4ForwardHotkeys).Returns(new List<string>());
                this.Configuration.SetupGet(c => c.CycleGroup4BackwardHotkeys).Returns(new List<string>());
                this.Configuration.SetupGet(c => c.CycleGroup5ForwardHotkeys).Returns(new List<string>());
                this.Configuration.SetupGet(c => c.CycleGroup5BackwardHotkeys).Returns(new List<string>());
                this.Configuration.SetupGet(c => c.MinimizeAllClientsHotkeys).Returns(new List<string>());
            }

            public void AddView(IThumbnailView view)
            {
                GetThumbnailViews(this.Manager)[view.Id] = view;
            }

            public bool RemoveView(IntPtr id)
            {
                return GetThumbnailViews(this.Manager).Remove(id);
            }

            public void ReplaceView(IThumbnailView view)
            {
                GetThumbnailViews(this.Manager)[view.Id] = view;
            }
        }

        private static Dictionary<IntPtr, IThumbnailView> GetThumbnailViews(ThumbnailManager manager)
        {
            var field = typeof(ThumbnailManager).GetField("_thumbnailViews", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new InvalidOperationException("ThumbnailManager._thumbnailViews field was not found (renamed?).");
            }

            return (Dictionary<IntPtr, IThumbnailView>)field.GetValue(manager)!;
        }

        private static void InvokeThumbnailActivated(ThumbnailManager manager, IntPtr id)
        {
            var method = typeof(ThumbnailManager).GetMethod("ThumbnailActivated", BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null)
            {
                throw new InvalidOperationException("ThumbnailManager.ThumbnailActivated method was not found (renamed?).");
            }

            method.Invoke(manager, new object[] { id });
        }

        private static Mock<IThumbnailView> CreateView(IntPtr id, string title)
        {
            var view = new Mock<IThumbnailView>(MockBehavior.Loose);
            view.SetupGet(v => v.Id).Returns(id);
            view.SetupGet(v => v.Title).Returns(title);
            return view;
        }

        private static void InvokeContinuation(QueuedSynchronizationContext context)
        {
            Assert.True(context.TryDequeue(out var item), "Timed out waiting for the UI continuation to be queued.");
            item.Key(item.Value);
        }

        private static void SetupActivateWindowBlocks(Mock<IWindowManager> windowManager, ManualResetEventSlim entered, ManualResetEventSlim release)
        {
#if LINUX
            windowManager.Setup(w => w.ActivateWindow(It.IsAny<IntPtr>(), It.IsAny<string>()))
#else
            windowManager.Setup(w => w.ActivateWindow(It.IsAny<IntPtr>(), It.IsAny<AnimationStyle>()))
#endif
                .Callback(() =>
                {
                    entered.Set();
                    release.Wait(ContinuationTimeout);
                });
        }

        [Fact]
        public void ThumbnailActivated_UnknownId_DoesNotThrow_AndDoesNothing()
        {
            var harness = new TestHarness();
            var previousContext = SynchronizationContext.Current;
            var context = new QueuedSynchronizationContext();

            try
            {
                SynchronizationContext.SetSynchronizationContext(context);

                var unknownId = new IntPtr(0xDEADBEEF);
                var exception = Record.Exception(() => InvokeThumbnailActivated(harness.Manager, unknownId));

                Assert.Null(exception);
#if LINUX
                harness.WindowManager.Verify(w => w.ActivateWindow(It.IsAny<IntPtr>(), It.IsAny<string>()), Times.Never);
#else
                harness.WindowManager.Verify(w => w.ActivateWindow(It.IsAny<IntPtr>(), It.IsAny<AnimationStyle>()), Times.Never);
#endif
                Assert.False(context.TryDequeueNoWait(out _), "No UI continuation should be queued for an unknown id.");
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        }

        [Fact]
        public void ThumbnailActivated_ViewRemovedBeforeContinuation_SkipsStateTransition()
        {
            var harness = new TestHarness();
            var id = new IntPtr(0x1111);
            var view = CreateView(id, "EVE - Alpha");
            harness.AddView(view.Object);

            var entered = new ManualResetEventSlim(false);
            var release = new ManualResetEventSlim(false);
            SetupActivateWindowBlocks(harness.WindowManager, entered, release);

            RunWithQueuedContext(context =>
            {
                InvokeThumbnailActivated(harness.Manager, id);

                Assert.True(entered.Wait(ContinuationTimeout), "Background ActivateWindow was not started.");

                // Simulate the client window closing while activation is in flight
                Assert.True(harness.RemoveView(id));

                release.Set();
                InvokeContinuation(context);
            });

            // RefreshThumbnails must not run (it would touch the foreground window), and no
            // client can become active from a stale id.
            harness.WindowManager.Verify(w => w.GetForegroundWindowHandle(), Times.Never);
            Assert.Null(harness.Manager.GetActiveClient());
        }

        [Fact]
        public void ThumbnailActivated_ViewReplacedBeforeContinuation_SkipsStateTransition()
        {
            var harness = new TestHarness();
            var id = new IntPtr(0x2222);
            var original = CreateView(id, "EVE - Alpha");
            var replacement = CreateView(id, "EVE - Alpha (new)");
            harness.AddView(original.Object);

            var entered = new ManualResetEventSlim(false);
            var release = new ManualResetEventSlim(false);
            SetupActivateWindowBlocks(harness.WindowManager, entered, release);

            RunWithQueuedContext(context =>
            {
                InvokeThumbnailActivated(harness.Manager, id);

                Assert.True(entered.Wait(ContinuationTimeout), "Background ActivateWindow was not started.");

                // Simulate the old view being closed and a new view registered under the same handle
                harness.ReplaceView(replacement.Object);

                release.Set();
                InvokeContinuation(context);
            });

            harness.WindowManager.Verify(w => w.GetForegroundWindowHandle(), Times.Never);
            Assert.Null(harness.Manager.GetActiveClient());
            replacement.Verify(v => v.Refresh(It.IsAny<bool>()), Times.Never);
            replacement.Verify(v => v.SetHighlight(), Times.Never);
        }

        [Fact]
        public void ThumbnailActivated_ActivateWindowFaults_SkipsStateTransition()
        {
            var harness = new TestHarness();
            var id = new IntPtr(0x3333);
            var view = CreateView(id, "EVE - Alpha");
            harness.AddView(view.Object);

#if LINUX
            harness.WindowManager.Setup(w => w.ActivateWindow(It.IsAny<IntPtr>(), It.IsAny<string>()))
                .Throws(new InvalidOperationException("simulated activation failure"));
#else
            harness.WindowManager.Setup(w => w.ActivateWindow(It.IsAny<IntPtr>(), It.IsAny<AnimationStyle>()))
                .Throws(new InvalidOperationException("simulated activation failure"));
#endif

            RunWithQueuedContext(context =>
            {
                InvokeThumbnailActivated(harness.Manager, id);
                InvokeContinuation(context);
            });

            // A faulted activation must not perform the "successful" refresh/layout pass,
            // and no client must be switched into the active state.
            harness.WindowManager.Verify(w => w.GetForegroundWindowHandle(), Times.Never);
            Assert.Null(harness.Manager.GetActiveClient());
        }

        [Fact]
        public void ThumbnailActivated_CancelledActivation_SkipsStateTransition()
        {
            var harness = new TestHarness();
            var id = new IntPtr(0x5555);
            var view = CreateView(id, "EVE - Alpha");
            harness.AddView(view.Object);

            var entered = new ManualResetEventSlim(false);

#if LINUX
            harness.WindowManager.Setup(w => w.ActivateWindow(It.IsAny<IntPtr>(), It.IsAny<string>()))
#else
            harness.WindowManager.Setup(w => w.ActivateWindow(It.IsAny<IntPtr>(), It.IsAny<AnimationStyle>()))
#endif
                .Callback(() =>
                {
                    entered.Set();
                    // Simulate cancellation by throwing OperationCanceledException
                    throw new OperationCanceledException("Activation cancelled");
                });

            RunWithQueuedContext(context =>
            {
                InvokeThumbnailActivated(harness.Manager, id);

                Assert.True(entered.Wait(ContinuationTimeout), "Background ActivateWindow was not started.");

                InvokeContinuation(context);
            });

            // A cancelled activation must not perform the "successful" refresh/layout pass,
            // and no client must be switched into the active state.
            harness.WindowManager.Verify(w => w.GetForegroundWindowHandle(), Times.Never);
            Assert.Null(harness.Manager.GetActiveClient());
        }

        [Fact]
        public void ThumbnailActivated_SuccessfulActivation_SwitchesActiveClient()
        {
            var harness = new TestHarness();
            var id = new IntPtr(0x4444);
            var view = CreateView(id, "EVE - Alpha");
            harness.AddView(view.Object);

#if LINUX
            harness.WindowManager.Setup(w => w.ActivateWindow(It.IsAny<IntPtr>(), It.IsAny<string>()));
#else
            harness.WindowManager.Setup(w => w.ActivateWindow(It.IsAny<IntPtr>(), It.IsAny<AnimationStyle>()));
#endif

            IThumbnailView activeBefore = harness.Manager.GetActiveClient();
            Assert.Null(activeBefore);

            RunWithQueuedContext(context =>
            {
                InvokeThumbnailActivated(harness.Manager, id);
                InvokeContinuation(context);
            });

            // The continuation must perform the normal UI-side work: RefreshThumbnails calls
            // GetForegroundWindowHandle first (mocked to IntPtr.Zero, which short-circuits it),
            // and the activated client becomes the active client.
            harness.WindowManager.Verify(w => w.GetForegroundWindowHandle(), Times.Once);
            Assert.Same(view.Object, harness.Manager.GetActiveClient());
        }

        private static void RunWithQueuedContext(Action<QueuedSynchronizationContext> action)
        {
            var previousContext = SynchronizationContext.Current;
            var context = new QueuedSynchronizationContext();

            try
            {
                SynchronizationContext.SetSynchronizationContext(context);
                action(context);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        }
    }
}
