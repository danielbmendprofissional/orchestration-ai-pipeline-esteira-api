namespace Esteira.Domain.Aplicacoes
{
    // Enums do cadastro (A1). Persistidos como texto em kebab-case (ex.: AprovadorTecnico -> "aprovador-tecnico").
    public enum Stack { Dotnet, Angular, Outro }

    // Ambientes cadastráveis num Projeto. Sem dev (ADR-0021); review é efêmero (ADR-0020).
    public enum AmbienteDeploy { Qa, Hml, Producao }

    // Ambientes que têm Política de severidade (inclui review).
    public enum AmbientePolitica { Review, Qa, Hml, Producao }

    public enum ModoPolitica { Avisa, Bloqueia }

    public enum PapelAplicacao { Analista, Membro, AprovadorTecnico, AprovadorNegocio, Publicador }

    public enum AcaoAuditoria { Criou, Alterou, Removeu }

    // X1: as oito categorias da trilha e quem originou o registro (tela = pessoa, bot, pipeline = webhook/pipeline do GitLab).
    public enum CategoriaAuditoria { Aprovacao, Fatias, Entrega, Publicacao, Excecao, Politica, Cadastro, Papeis }

    public enum OrigemAuditoria { Tela, Bot, Pipeline }
}
