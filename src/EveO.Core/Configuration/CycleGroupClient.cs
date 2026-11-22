using System;
using System.Collections.Generic;
using System.Linq;

namespace EveOPreview.Configuration
{
	/// <summary>Cliente disponível para entrar num grupo. <c>IsOpen</c> = janela aberta agora.</summary>
	public sealed class CycleGroupClient
	{
		public CycleGroupClient(string title, bool isOpen)
		{
			this.Title = title;
			this.IsOpen = isOpen;
		}

		public string Title { get; }

		public bool IsOpen { get; }

		/// <summary>Texto da lista: cliente aberto ganha marcador.</summary>
		public override string ToString() => this.IsOpen ? this.Title + "  ●" : this.Title;
	}

	/// <summary>
	/// Junta os clientes que o app conhece (config) com os que estão abertos agora.
	/// Abertos vêm primeiro e marcados — é o que o usuário quer escolher na sessão.
	/// </summary>
	public static class CycleGroupClients
	{
		public static IReadOnlyList<CycleGroupClient> Build(IEnumerable<string> known, IEnumerable<string> openTitles)
		{
			var abertos = new HashSet<string>(
				(openTitles ?? Enumerable.Empty<string>())
					.Where(t => !string.IsNullOrWhiteSpace(t))
					.Select(t => t.Trim()),
				StringComparer.OrdinalIgnoreCase);

			var todos = new HashSet<string>(abertos, StringComparer.OrdinalIgnoreCase);
			foreach (string nome in known ?? Enumerable.Empty<string>())
			{
				if (!string.IsNullOrWhiteSpace(nome))
				{
					todos.Add(nome.Trim());
				}
			}

			return todos
				.Select(nome => new CycleGroupClient(nome, abertos.Contains(nome)))
				.OrderByDescending(cliente => cliente.IsOpen)
				.ThenBy(cliente => cliente.Title, StringComparer.OrdinalIgnoreCase)
				.ToList();
		}
	}

	/// <summary>
	/// O que a tela de edição de grupo mostra e devolve. O apresentador monta, a view
	/// exibe e altera; cancelar significa não usar o resultado.
	/// </summary>
	public sealed class CycleGroupEditModel
	{
		public CycleGroupEditModel(int number, string forward, string backward,
			IReadOnlyList<CycleGroupClient> clients, IEnumerable<string> selectedClients,
			IEnumerable<string> extraForward, IEnumerable<string> extraBackward)
		{
			this.Number = number;
			this.Forward = forward ?? string.Empty;
			this.Backward = backward ?? string.Empty;
			this.Clients = clients ?? new List<CycleGroupClient>();
			this.SelectedClients = (selectedClients ?? Enumerable.Empty<string>()).ToList();
			this.ExtraForward = (extraForward ?? Enumerable.Empty<string>()).ToList();
			this.ExtraBackward = (extraBackward ?? Enumerable.Empty<string>()).ToList();
		}

		public int Number { get; }

		/// <summary>Atalho para avançar (uma tecla).</summary>
		public string Forward { get; set; }

		/// <summary>Atalho para voltar (uma tecla).</summary>
		public string Backward { get; set; }

		public IReadOnlyList<CycleGroupClient> Clients { get; }

		/// <summary>Clientes do grupo, na ordem de ciclagem.</summary>
		public List<string> SelectedClients { get; }

		/// <summary>Atalhos adicionais que já existiam no arquivo e são preservados.</summary>
		public IReadOnlyList<string> ExtraForward { get; }

		public IReadOnlyList<string> ExtraBackward { get; }
	}
}
