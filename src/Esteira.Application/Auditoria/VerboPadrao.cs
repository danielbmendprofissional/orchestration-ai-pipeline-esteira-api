using Esteira.Domain.Aplicacoes;

namespace Esteira.Application.Auditoria
{
    // Verbo nulo no registro = a API monta "<criou|alterou|removeu> <entidade legível>" na leitura.
    public static class VerboPadrao
    {
        public static string Montar(string? gravado, AcaoAuditoria acao, string entidade)
            => !string.IsNullOrWhiteSpace(gravado) ? gravado : $"{Acao(acao)} {EntidadeLegivel(entidade)}";

        private static string Acao(AcaoAuditoria acao) => acao switch
        {
            AcaoAuditoria.Criou => "criou",
            AcaoAuditoria.Alterou => "alterou",
            _ => "removeu"
        };

        public static string EntidadeLegivel(string entidade) => entidade switch
        {
            "Aplicacao" => "a Aplicação",
            "Projeto" => "o Projeto",
            "AmbienteProjeto" => "o Ambiente do Projeto",
            "Destino" => "o Destino",
            "PoliticaSeveridade" => "a Política de severidade",
            "PapelAplicacao" => "o Papel na Aplicação",
            "AcaoBot" => "a Ação do bot",
            "Entrega" => "a Entrega",
            _ => entidade
        };
    }
}
