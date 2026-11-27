using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Resultados;

namespace Esteira.Domain.Eventos
{
    // Persistido como texto em kebab-case (pendente, registrado, falhou).
    public enum EstadoWebhookProjeto { Pendente, Registrado, Falhou }

    // Registro do hook do GitLab em um Projeto (spec T1 "Dados"): uma linha por Projeto; a reexecução volta a pendente.
    // pendente -> registrado | falhou; falhou -> pendente; nenhuma outra. O segredo do webhook nunca é guardado aqui.
    public sealed class WebhookProjetoEntity : EntityBase
    {
        private WebhookProjetoEntity() { }

        public long ProjetoId { get; private set; }
        public long? GitLabHookId { get; private set; }
        public EstadoWebhookProjeto Estado { get; private set; }
        public string? CodigoErro { get; private set; }
        public DateTime AtualizadaEm { get; private set; }

        public static WebhookProjetoEntity Criar(long projetoId, DateTime quando)
            => new()
            {
                ProjetoId = projetoId,
                Estado = EstadoWebhookProjeto.Pendente,
                AtualizadaEm = quando
            };

        public Result Registrar(long gitLabHookId, DateTime quando)
        {
            if (gitLabHookId <= 0)
                throw new ArgumentOutOfRangeException(nameof(gitLabHookId), "O id do hook precisa ser positivo.");

            var r = Transitar(EstadoWebhookProjeto.Pendente, EstadoWebhookProjeto.Registrado, quando, null);
            if (r.IsSuccess) GitLabHookId = gitLabHookId;
            return r;
        }

        public Result Falhar(string codigoErro, DateTime quando)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(codigoErro);
            return Transitar(EstadoWebhookProjeto.Pendente, EstadoWebhookProjeto.Falhou, quando, codigoErro);
        }

        public Result Reexecutar(DateTime quando)
            => Transitar(EstadoWebhookProjeto.Falhou, EstadoWebhookProjeto.Pendente, quando, null);

        private Result Transitar(EstadoWebhookProjeto de, EstadoWebhookProjeto para, DateTime quando, string? codigoErro)
        {
            if (Estado != de)
                return Result.Falha(new Erro("webhook.transicao-invalida",
                    $"O webhook não pode ir de {Estado} para {para}.", TipoErro.Conflict));

            Estado = para;
            AtualizadaEm = quando;
            CodigoErro = codigoErro;
            return Result.Sucesso();
        }
    }
}
