namespace Esteira.Application.Settings
{
    public class GerenciadorTarefasSettings
    {
        public const string Secao = "GerenciadorTarefas";

        public string UrlTarefa { get; set; } = string.Empty;
        public string UrlNovaGmud { get; set; } = string.Empty;

        public string MontarUrlTarefa(int? idTarefa)
            => idTarefa is > 0 && !string.IsNullOrWhiteSpace(UrlTarefa)
                ? string.Format(UrlTarefa, idTarefa)
                : null;
    }
}
