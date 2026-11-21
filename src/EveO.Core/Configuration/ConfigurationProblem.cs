namespace EveOPreview.Configuration
{
	/// <summary>
	/// Um valor da configuração que estava fora da faixa, com a chave do arquivo e o
	/// motivo em linguagem simples. O ajuste em si é feito por <c>ApplyRestrictions</c>.
	/// </summary>
	public sealed class ConfigurationProblem
	{
		public ConfigurationProblem(string key, string message)
		{
			this.Key = key;
			this.Message = message;
		}

		/// <summary>Nome da chave como aparece no EVE-O-Preview.json.</summary>
		public string Key { get; }

		/// <summary>O que estava errado e para quanto será ajustado.</summary>
		public string Message { get; }

		public override string ToString() => this.Key + ": " + this.Message;
	}
}
