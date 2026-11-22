using System.Collections.Generic;
using System.Linq;

namespace EveOPreview.Configuration
{
	/// <summary>
	/// Retrato de um grupo de ciclo para exibição: atalhos e clientes na ordem em que o
	/// aplicativo vai ciclar. Existe porque configurar grupo só era possível editando o
	/// JSON — e quem não acha o arquivo não configura nada.
	/// </summary>
	public sealed class CycleGroupSummary
	{
		public CycleGroupSummary(int number, IEnumerable<string> forwardHotkeys,
			IEnumerable<string> backwardHotkeys, IEnumerable<string> clients)
		{
			this.Number = number;
			this.ForwardHotkeys = (forwardHotkeys ?? Enumerable.Empty<string>())
				.Where(h => !string.IsNullOrWhiteSpace(h)).ToList();
			this.BackwardHotkeys = (backwardHotkeys ?? Enumerable.Empty<string>())
				.Where(h => !string.IsNullOrWhiteSpace(h)).ToList();
			this.Clients = (clients ?? Enumerable.Empty<string>()).ToList();
		}

		public int Number { get; }

		public IReadOnlyList<string> ForwardHotkeys { get; }

		public IReadOnlyList<string> BackwardHotkeys { get; }

		/// <summary>Clientes já na ordem de ciclagem.</summary>
		public IReadOnlyList<string> Clients { get; }

		public bool IsEmpty => this.ForwardHotkeys.Count == 0
			&& this.BackwardHotkeys.Count == 0
			&& this.Clients.Count == 0;

		/// <summary>Texto pronto para a tela: "F14 · Control+F14" ou "(sem atalho)".</summary>
		public string HotkeyText
		{
			get
			{
				string avancar = this.ForwardHotkeys.Count == 0
					? "(sem atalho)"
					: string.Join(" · ", this.ForwardHotkeys);
				string voltar = this.BackwardHotkeys.Count == 0
					? "(sem atalho)"
					: string.Join(" · ", this.BackwardHotkeys);

				return "avançar: " + avancar + " | voltar: " + voltar;
			}
		}

		public override string ToString() =>
			"grupo " + this.Number + ": " + this.HotkeyText + " | " + this.Clients.Count + " cliente(s)";
	}

	/// <summary>Monta os resumos dos cinco grupos a partir da configuração carregada.</summary>
	public static class CycleGroupSummaryBuilder
	{
		public static IReadOnlyList<CycleGroupSummary> Build(IThumbnailConfiguration configuration)
		{
			if (configuration == null)
			{
				return new List<CycleGroupSummary>();
			}

			return new List<CycleGroupSummary>
			{
				Create(1, configuration.CycleGroup1ForwardHotkeys, configuration.CycleGroup1BackwardHotkeys, configuration.CycleGroup1ClientsOrder),
				Create(2, configuration.CycleGroup2ForwardHotkeys, configuration.CycleGroup2BackwardHotkeys, configuration.CycleGroup2ClientsOrder),
				Create(3, configuration.CycleGroup3ForwardHotkeys, configuration.CycleGroup3BackwardHotkeys, configuration.CycleGroup3ClientsOrder),
				Create(4, configuration.CycleGroup4ForwardHotkeys, configuration.CycleGroup4BackwardHotkeys, configuration.CycleGroup4ClientsOrder),
				Create(5, configuration.CycleGroup5ForwardHotkeys, configuration.CycleGroup5BackwardHotkeys, configuration.CycleGroup5ClientsOrder),
			};
		}

		private static CycleGroupSummary Create(int number, IEnumerable<string> forward,
			IEnumerable<string> backward, IDictionary<string, int> order)
		{
			List<string> clientes = order == null
				? new List<string>()
				: order.OrderBy(par => par.Value).Select(par => par.Key).ToList();

			return new CycleGroupSummary(number, forward, backward, clientes);
		}
	}
}
