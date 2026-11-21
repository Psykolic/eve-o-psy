using System.Collections.Generic;
using System.Linq;

namespace EveOPreview.Configuration
{
	/// <summary>
	/// Resultado estruturado de um carregamento de configuração. Substitui a exceção
	/// genérica: quem chama sabe exatamente o que estava errado e pode avisar o usuário.
	/// </summary>
	public sealed class ConfigurationValidationResult
	{
		private readonly List<ConfigurationProblem> _problems = new List<ConfigurationProblem>();

		public IReadOnlyList<ConfigurationProblem> Problems => this._problems;

		public bool IsValid => this._problems.Count == 0;

		public ConfigurationValidationResult Add(string key, string message)
		{
			this._problems.Add(new ConfigurationProblem(key, message));
			return this;
		}

		/// <summary>Texto de uma linha, pronto para log ou caixa de diálogo.</summary>
		public string Summary() => string.Join("; ", this._problems.Select(problem => problem.ToString()));
	}
}
