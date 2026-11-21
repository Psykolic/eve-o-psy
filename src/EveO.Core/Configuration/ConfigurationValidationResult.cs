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
		private readonly List<string> _notices = new List<string>();

		public IReadOnlyList<ConfigurationProblem> Problems => this._problems;

		/// <summary>Avisos informativos (ex.: importação do formato anterior). Não invalidam a config.</summary>
		public IReadOnlyList<string> Notices => this._notices;

		public bool IsValid => this._problems.Count == 0;

		public bool HasNotices => this._notices.Count > 0;

		public ConfigurationValidationResult Add(string key, string message)
		{
			this._problems.Add(new ConfigurationProblem(key, message));
			return this;
		}

		public ConfigurationValidationResult AddNotice(string message)
		{
			this._notices.Add(message);
			return this;
		}

		/// <summary>Texto de uma linha, pronto para log ou caixa de diálogo.</summary>
		public string Summary() => string.Join("; ",
			this._problems.Select(problem => problem.ToString()).Concat(this._notices));
	}
}
