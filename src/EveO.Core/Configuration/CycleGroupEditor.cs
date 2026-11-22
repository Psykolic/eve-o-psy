using System;
using System.Collections.Generic;
using System.Linq;

namespace EveOPreview.Configuration
{
	/// <summary>
	/// Edição dos grupos de ciclo: definir, limpar e detectar atalho repetido.
	///
	/// A configuração continua sendo a fonte da verdade; isto só aplica as mudanças de
	/// forma consistente (ordem 1..n, sem atalho vazio, sem repetição entre grupos).
	/// </summary>
	public static class CycleGroupEditor
	{
		public const int MinGroup = 1;
		public const int MaxGroup = 5;

		public static void SetGroup(IThumbnailConfiguration configuration, int number,
			IEnumerable<string> forwardHotkeys, IEnumerable<string> backwardHotkeys, IEnumerable<string> clients)
		{
			if (configuration == null)
			{
				throw new ArgumentNullException(nameof(configuration));
			}

			if (number < CycleGroupEditor.MinGroup || number > CycleGroupEditor.MaxGroup)
			{
				throw new ArgumentOutOfRangeException(nameof(number));
			}

			List<string> forward = CycleGroupEditor.Normalize(forwardHotkeys);
			List<string> backward = CycleGroupEditor.Normalize(backwardHotkeys);
			Dictionary<string, int> order = CycleGroupEditor.ToOrder(clients);

			CycleGroupEditor.Assign(configuration, number, forward, backward, order);
		}

		public static void ClearGroup(IThumbnailConfiguration configuration, int number)
		{
			CycleGroupEditor.SetGroup(configuration, number,
				Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
		}

		/// <summary>
		/// Atalhos que já estão em uso em outro grupo (ou repetidos no próprio grupo).
		/// Devolve textos prontos para mostrar — não bloqueia, só avisa.
		/// </summary>
		public static IReadOnlyList<string> FindHotkeyConflicts(IThumbnailConfiguration configuration,
			int number, IEnumerable<string> forwardHotkeys, IEnumerable<string> backwardHotkeys)
		{
			var avisos = new List<string>();
			if (configuration == null)
			{
				return avisos;
			}

			var emUso = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			for (int grupo = CycleGroupEditor.MinGroup; grupo <= CycleGroupEditor.MaxGroup; grupo++)
			{
				if (grupo == number)
				{
					continue;
				}

				foreach (string tecla in Read(configuration, grupo, true))
				{
					emUso[tecla] = "grupo " + grupo + " (avançar)";
				}

				foreach (string tecla in Read(configuration, grupo, false))
				{
					emUso[tecla] = "grupo " + grupo + " (voltar)";
				}
			}

			var proprios = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (string tecla in CycleGroupEditor.Normalize(forwardHotkeys).Concat(CycleGroupEditor.Normalize(backwardHotkeys)))
			{
				if (!proprios.Add(tecla))
				{
					avisos.Add(tecla + " está repetido neste mesmo grupo");
					continue;
				}

				if (emUso.TryGetValue(tecla, out string onde))
				{
					avisos.Add(tecla + " já é usado pelo " + onde);
				}
			}

			return avisos;
		}

		private static List<string> Normalize(IEnumerable<string> hotkeys)
		{
			if (hotkeys == null)
			{
				return new List<string>();
			}

			return hotkeys
				.Where(h => !string.IsNullOrWhiteSpace(h))
				.Select(h => h.Trim())
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		private static Dictionary<string, int> ToOrder(IEnumerable<string> clients)
		{
			var order = new Dictionary<string, int>();
			if (clients == null)
			{
				return order;
			}

			int posicao = 0;
			foreach (string cliente in clients.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim())
				.Distinct(StringComparer.OrdinalIgnoreCase))
			{
				posicao++;
				order[cliente] = posicao;
			}

			return order;
		}

		private static IEnumerable<string> Read(IThumbnailConfiguration configuration, int grupo, bool forward)
		{
			List<string> lista = null;
			switch (grupo)
			{
				case 1: lista = forward ? configuration.CycleGroup1ForwardHotkeys : configuration.CycleGroup1BackwardHotkeys; break;
				case 2: lista = forward ? configuration.CycleGroup2ForwardHotkeys : configuration.CycleGroup2BackwardHotkeys; break;
				case 3: lista = forward ? configuration.CycleGroup3ForwardHotkeys : configuration.CycleGroup3BackwardHotkeys; break;
				case 4: lista = forward ? configuration.CycleGroup4ForwardHotkeys : configuration.CycleGroup4BackwardHotkeys; break;
				case 5: lista = forward ? configuration.CycleGroup5ForwardHotkeys : configuration.CycleGroup5BackwardHotkeys; break;
			}

			return CycleGroupEditor.Normalize(lista);
		}

		private static void Assign(IThumbnailConfiguration configuration, int grupo,
			List<string> forward, List<string> backward, Dictionary<string, int> order)
		{
			switch (grupo)
			{
				case 1:
					configuration.CycleGroup1ForwardHotkeys = forward;
					configuration.CycleGroup1BackwardHotkeys = backward;
					configuration.CycleGroup1ClientsOrder = order;
					break;
				case 2:
					configuration.CycleGroup2ForwardHotkeys = forward;
					configuration.CycleGroup2BackwardHotkeys = backward;
					configuration.CycleGroup2ClientsOrder = order;
					break;
				case 3:
					configuration.CycleGroup3ForwardHotkeys = forward;
					configuration.CycleGroup3BackwardHotkeys = backward;
					configuration.CycleGroup3ClientsOrder = order;
					break;
				case 4:
					configuration.CycleGroup4ForwardHotkeys = forward;
					configuration.CycleGroup4BackwardHotkeys = backward;
					configuration.CycleGroup4ClientsOrder = order;
					break;
				case 5:
					configuration.CycleGroup5ForwardHotkeys = forward;
					configuration.CycleGroup5BackwardHotkeys = backward;
					configuration.CycleGroup5ClientsOrder = order;
					break;
			}
		}
	}
}
