using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using EveOPreview.Configuration;
using EveOPreview.Refresh;
using EveOPreview.Services;
using EveOPreview.View;
using MediatR;
using Moq;
using Xunit;

namespace EveO.Tests.Services
{
	/// <summary>
	/// Regressão do resize do spotlight (issue #163): o refresh periódico reaplicava
	/// tamanho e posição da configuração na view que estava sendo arrastada, fazendo a
	/// janela voltar no meio do gesto.
	/// </summary>
	public sealed class ThumbnailManagerSpotlightResizeTests
	{
		private static ThumbnailManager CriarManager()
		{
			var configuracao = new Mock<IThumbnailConfiguration>(MockBehavior.Loose);
			configuracao.SetupGet(c => c.CycleGroup1ForwardHotkeys).Returns(new List<string>());
			configuracao.SetupGet(c => c.CycleGroup1BackwardHotkeys).Returns(new List<string>());
			configuracao.SetupGet(c => c.CycleGroup2ForwardHotkeys).Returns(new List<string>());
			configuracao.SetupGet(c => c.CycleGroup2BackwardHotkeys).Returns(new List<string>());
			configuracao.SetupGet(c => c.CycleGroup3ForwardHotkeys).Returns(new List<string>());
			configuracao.SetupGet(c => c.CycleGroup3BackwardHotkeys).Returns(new List<string>());
			configuracao.SetupGet(c => c.CycleGroup4ForwardHotkeys).Returns(new List<string>());
			configuracao.SetupGet(c => c.CycleGroup4BackwardHotkeys).Returns(new List<string>());
			configuracao.SetupGet(c => c.CycleGroup5ForwardHotkeys).Returns(new List<string>());
			configuracao.SetupGet(c => c.CycleGroup5BackwardHotkeys).Returns(new List<string>());
			configuracao.SetupGet(c => c.MinimizeAllClientsHotkeys).Returns(new List<string>());
			configuracao.SetupGet(c => c.ToggleSpotlightHotkeys).Returns(new List<string>());
			configuracao.SetupGet(c => c.SpotlightSize).Returns(new Size(640, 360));

			return new ThumbnailManager(
				new Mock<IMediator>(MockBehavior.Loose).Object,
				configuracao.Object,
				new Mock<IProcessMonitor>(MockBehavior.Loose).Object,
				new Mock<IWindowManager>(MockBehavior.Loose).Object,
				new Mock<IThumbnailViewFactory>(MockBehavior.Loose).Object,
				Mock.Of<IClientChangeSignal>(),
				Mock.Of<IWindowEventSource>());
		}

		[Fact]
		public void RefreshSpotlightViews_NaoMexeNaViewQueEstaSendoArrastada()
		{
			ThumbnailManager manager = CriarManager();

			var emArrasto = new Mock<IThumbnailView>(MockBehavior.Loose);
			emArrasto.SetupGet(v => v.IsUserResizing).Returns(true);
			emArrasto.SetupGet(v => v.Title).Returns("EVE - Em Arrasto");
			emArrasto.SetupGet(v => v.IsActive).Returns(true);

			var parada = new Mock<IThumbnailView>(MockBehavior.Loose);
			parada.SetupGet(v => v.IsUserResizing).Returns(false);
			parada.SetupGet(v => v.Title).Returns("EVE - Parada");
			parada.SetupGet(v => v.IsActive).Returns(true);

			FieldInfo campo = typeof(ThumbnailManager).GetField("_spotlightViews", BindingFlags.Instance | BindingFlags.NonPublic);
			var views = (Dictionary<IntPtr, IThumbnailView>)campo.GetValue(manager);
			views[new IntPtr(1)] = emArrasto.Object;
			views[new IntPtr(2)] = parada.Object;

			MethodInfo metodo = typeof(ThumbnailManager).GetMethod("RefreshSpotlightViews", BindingFlags.Instance | BindingFlags.NonPublic);
			metodo.Invoke(manager, new object[] { false });

			emArrasto.VerifySet(v => v.ThumbnailSize = It.IsAny<Size>(), Times.Never);
			emArrasto.VerifySet(v => v.ThumbnailLocation = It.IsAny<Point>(), Times.Never);
			emArrasto.Verify(v => v.Refresh(It.IsAny<bool>()), Times.Never);

			parada.VerifySet(v => v.ThumbnailSize = It.IsAny<Size>(), Times.Once);
		}
	}
}
