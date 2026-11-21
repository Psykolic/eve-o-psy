using System.Windows.Forms;
using EveOPreview.Configuration;
using EveOPreview.Presenters;
using EveOPreview.Refresh;
using EveOPreview.Services;
using EveOPreview.View;
using MediatR;

namespace EveOPreview
{
	/// <summary>
	/// Raiz de composição do aplicativo.
	///
	/// Vive no EveO.Presentation porque é ela quem conhece as implementações: o
	/// exe (EVE-O-Preview) só tem o Program. Manter o registro aqui permite que os
	/// testes validem o grafo do container sem instanciar a UI (ver CompositionRootTests).
	/// </summary>
	public static class CompositionRoot
	{
		public static IApplicationController Create()
		{
			return CompositionRoot.Create(new LightInjectContainer());
		}

		public static IApplicationController Create(IIocContainer container)
		{
			CompositionRoot.Configure(container);

			IApplicationController controller = new ApplicationController(container);

			// UI classes
			controller.RegisterView<StaticThumbnailView, StaticThumbnailView>();
			controller.RegisterView<LiveThumbnailView, LiveThumbnailView>();

			controller.RegisterView<IMainFormView, MainForm>();
			controller.RegisterInstance(new ApplicationContext());

			return controller;
		}

		public static void Configure(IIocContainer container)
		{
			// Singleton registration is used for services
			// Low-level services
			// O registro por convenção varre o assembly do TIPO INFORMADO, não o de quem chama.
			// Contratos que moram no Core (IWindowManager, IProcessMonitor) mas são implementados
			// no Infrastructure precisam apontar explicitamente para o assembly do Infrastructure.
			var infrastructureAssembly = typeof(EveOPreview.Services.Implementation.WindowManager).Assembly;

			container.Register(typeof(IWindowManager), infrastructureAssembly);
			container.Register(typeof(IProcessMonitor), infrastructureAssembly);
			container.Register<IStaticThumbnailProvider>();

			// Fase 2.2 (ADR-006): fonte de eventos de janela + sinal coalescido que
			// substitui o polling de 500 ms (com reconciliação lenta de segurança).
			// O sinal PRECISA ser singleton: produtor (hook) e consumidor (ThumbnailManager)
			// têm que compartilhar o mesmo canal. Register<TService,TImpl>() é transient.
			container.RegisterInstance<IClientChangeSignal>(new ClientChangeSignal());
			container.Register(typeof(IWindowEventSource), infrastructureAssembly);

			// MediatR
			container.Register<IMediator, MediatR.Mediator>();
			container.RegisterInstance<ServiceFactory>(t => container.Resolve(t));
			// Os handlers do MediatR vivem neste assembly desde a Onda 4b (#168).
			// A varredura precisa ser explícita: o registro por convenção usa o
			// assembly do tipo informado, nunca o de quem chama.
			var handlerAssembly = typeof(CompositionRoot).Assembly;

			container.Register(typeof(INotificationHandler<>), handlerAssembly);
			container.Register(typeof(IRequestHandler<>), handlerAssembly);
			container.Register(typeof(IRequestHandler<,>), handlerAssembly);

			// Configuration services
			container.Register<IConfigurationStorage>();
			container.Register<IConfigurationValidator>();
			container.Register<IAppConfig>();
			container.Register<IThumbnailConfiguration>();

			// Application services
			container.Register<IThumbnailManager>();
			container.Register<IThumbnailViewFactory>();
			container.Register<IThumbnailDescription>();
		}
	}
}
