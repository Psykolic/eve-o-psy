// Regressao das Ondas 4a/4b (#167/#168).
//
// Mover os handlers do MediatR e as implementacoes de servico para EveO.Presentation
// quebrou o registro por convencao do LightInject, que varre o assembly do TIPO
// INFORMADO (nao o de quem chama). O app subia e morria no primeiro StartService.
//
// Este teste valida o grafo real da CompositionRoot sem depender da UI.
using System;
using System.Linq;
using EveOPreview;

namespace EveO.Tests
{
	public class CompositionRootTests
	{
		private static readonly Type[] HandlerDefinitions =
		{
			typeof(MediatR.IRequestHandler<>),
			typeof(MediatR.IRequestHandler<,>),
			typeof(MediatR.INotificationHandler<>)
		};

		[Fact]
		public void CompositionRoot_registra_todos_os_handlers_do_MediatR()
		{
			IIocContainer container = new LightInjectContainer();
			CompositionRoot.Configure(container);

			Type[] handlers = typeof(CompositionRoot).Assembly
				.GetTypes()
				.Where(type => type.IsClass && !type.IsAbstract)
				.SelectMany(type => type.GetInterfaces())
				.Where(candidate => candidate.IsGenericType && HandlerDefinitions.Contains(candidate.GetGenericTypeDefinition()))
				.Distinct()
				.ToArray();

			handlers.Should().NotBeEmpty("o assembly de apresentacao precisa conter os handlers");

			foreach (Type handler in handlers)
			{
				container.IsRegistered(handler).Should().BeTrue(
					"o handler {0} precisa estar registrado na CompositionRoot", handler.Name);
			}
		}

		/// <summary>
		/// O bug de verdade foi uma dependencia transitiva ausente (IProcessMonitor):
		/// o handler estava registrado, mas o grafo nao fechava. Resolver a cadeia
		/// completa e a unica forma de provar que a aplicacao consegue iniciar.
		/// </summary>
		/// <summary>
		/// O sinal precisa ser singleton: o produtor (hook de janela) e o consumidor
		/// (ThumbnailManager) têm que compartilhar o mesmo canal. Com lifetime transient,
		/// cada resolve cria um canal novo e o refresh por evento simplesmente não acontece
		/// — foi exatamente o bug encontrado no smoke da 2.2b.
		/// </summary>
		[Fact]
		public void ClientChangeSignal_e_WindowEventSource_sao_singletons()
		{
			IIocContainer container = new LightInjectContainer();
			CompositionRoot.Configure(container);

			container.Resolve(typeof(EveOPreview.Refresh.IClientChangeSignal))
				.Should().BeSameAs(container.Resolve(typeof(EveOPreview.Refresh.IClientChangeSignal)));

			container.Resolve(typeof(EveOPreview.Services.IWindowEventSource))
				.Should().BeSameAs(container.Resolve(typeof(EveOPreview.Services.IWindowEventSource)));
		}

		[Fact]
		public void CompositionRoot_fecha_o_grafo_de_dependencias_do_ThumbnailManager()
		{
			IIocContainer container = new LightInjectContainer();
			CompositionRoot.Create(container);

			container.Resolve(typeof(EveOPreview.Services.IThumbnailManager)).Should().NotBeNull();
		}
	}
}
