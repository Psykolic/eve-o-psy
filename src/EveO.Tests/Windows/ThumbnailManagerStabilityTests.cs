using System;
using System.Collections.Generic;
using System.Reflection;
using EveOPreview.Configuration;
using EveOPreview.Services;
using EveOPreview.View;
using MediatR;
using Moq;
using Xunit;

namespace EveO.Tests.Windows
{
    public class ThumbnailManagerStabilityTests
    {
        [Fact]
        public void RemovingAnUnknownHandleDoesNotCrashOrAnnounceARemoval()
        {
            var manager = CreateManager(out _, out _);
            var removed = new List<string>();
            var process = Process(new IntPtr(101));

            Assert.False(manager.TryRemoveThumbnail(process, removed));
            Assert.Empty(removed);
        }

        [Fact]
        public void RemovingAnExistingViewDetachesCallbacksAndClosesIt()
        {
            var manager = CreateManager(out _, out _);
            var handle = new IntPtr(202);
            var view = new Mock<IThumbnailView>();
            view.SetupAllProperties();
            view.Object.Id = handle;
            view.Object.Title = "EVE - Test";
            view.Object.ThumbnailDeactivated = (_, _) => { };
            view.Object.ThumbnailActivated = _ => { };
            view.Object.ThumbnailResized = _ => { };
            view.Object.ThumbnailMoved = _ => { };
            view.Object.ThumbnailFocused = _ => { };
            view.Object.ThumbnailLostFocus = _ => { };
            view.Object.ThumbnailToggleCycleGroup = _ => { };
            Views(manager).Add(handle, view.Object);

            var removed = new List<string>();
            Assert.True(manager.TryRemoveThumbnail(Process(handle), removed));

            Assert.Equal(new[] { "EVE - Test" }, removed);
            Assert.Empty(Views(manager));
            Assert.Null(view.Object.ThumbnailDeactivated);
            Assert.Null(view.Object.ThumbnailActivated);
            Assert.Null(view.Object.ThumbnailResized);
            Assert.Null(view.Object.ThumbnailMoved);
            Assert.Null(view.Object.ThumbnailFocused);
            Assert.Null(view.Object.ThumbnailLostFocus);
            Assert.Null(view.Object.ThumbnailToggleCycleGroup);
            view.Verify(x => x.UnregisterHotkey(), Times.Once);
            view.Verify(x => x.Close(), Times.Once);
        }

        [Fact]
        public void RefreshFailureReenablesViewCallbacks()
        {
            var manager = CreateManager(out var processMonitor, out var windowManager);
            windowManager.Setup(x => x.GetForegroundWindowHandle()).Returns(new IntPtr(999));
            processMonitor.Setup(x => x.GetMainProcess()).Returns(Process(IntPtr.Zero));

            var view = new Mock<IThumbnailView>();
            view.SetupAllProperties();
            view.Object.Id = new IntPtr(303);
            view.Object.Title = "EVE - Test";
            view.Object.IsActive = true;
            view.Setup(x => x.Refresh(It.IsAny<bool>())).Throws(new InvalidOperationException("Injected refresh failure"));
            Views(manager).Add(view.Object.Id, view.Object);

            var refresh = typeof(ThumbnailManager).GetMethod("RefreshThumbnails", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(refresh);
            var exception = Assert.Throws<TargetInvocationException>(() => refresh!.Invoke(manager, null));
            Assert.IsType<InvalidOperationException>(exception.InnerException);

            var ignoreEvents = typeof(ThumbnailManager).GetField("_ignoreViewEvents", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(ignoreEvents);
            Assert.False((bool)ignoreEvents!.GetValue(manager)!);
        }

        private static ThumbnailManager CreateManager(
            out Mock<IProcessMonitor> processMonitor,
            out Mock<IWindowManager> windowManager)
        {
            processMonitor = new Mock<IProcessMonitor>();
            windowManager = new Mock<IWindowManager>();
            var configuration = new Mock<IThumbnailConfiguration>();
            configuration.Setup(x => x.ThumbnailRefreshPeriod).Returns(500);

            // Missing optional hotkey groups should not cause constructor failure.
            return new ThumbnailManager(
                new Mock<IMediator>().Object,
                configuration.Object,
                processMonitor.Object,
                windowManager.Object,
                new Mock<IThumbnailViewFactory>().Object);
        }

        private static IProcessInfo Process(IntPtr handle)
        {
            var process = new Mock<IProcessInfo>();
            process.Setup(x => x.Handle).Returns(handle);
            return process.Object;
        }

        private static Dictionary<IntPtr, IThumbnailView> Views(ThumbnailManager manager)
        {
            var field = typeof(ThumbnailManager).GetField("_thumbnailViews", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            return (Dictionary<IntPtr, IThumbnailView>)field!.GetValue(manager)!;
        }
    }
}
