namespace Esteira.Application.Aplicacoes
{
    // Classe com setters (e não record posicional): a ordenação por `sort` usa as propriedades e exige projeção member-init.
    public sealed class AplicacaoListaDto
    {
        public Guid Id { get; set; }
        public string Nome { get; set; } = default!;
        public string Sigla { get; set; } = default!;
        public string Grupo { get; set; } = default!;
        public int QuantidadeProjetos { get; set; }
        public bool Ativa { get; set; }
    }

    // Texto em kebab-case (`qa`, `bloqueia`), o vocabulário da spec.
    public sealed class PoliticaDto
    {
        public Guid Id { get; set; }
        public string Ambiente { get; set; } = default!;
        public int? Critica { get; set; }
        public int? Alta { get; set; }
        public int? Media { get; set; }
        public int? Baixa { get; set; }
        public string Modo { get; set; } = default!;
    }

    // A configuração do Destino nunca carrega segredo (critério 10).
    public sealed class DestinoDto
    {
        public Guid Id { get; set; }
        public string Nome { get; set; } = default!;
        public Dictionary<string, string> Configuracao { get; set; } = [];
        public int Ordem { get; set; }
    }

    public sealed class AmbienteProjetoDto
    {
        public Guid Id { get; set; }
        public string Ambiente { get; set; } = default!;
        public bool Habilitado { get; set; }
        public List<DestinoDto> Destinos { get; set; } = [];
    }

    // Projeto completo (A1.4): stack, ativo, Ambientes com Destinos e dependências (PublicId dos Projetos de que depende).
    // Papéis/pessoas são da A1.5, que estende PapelResumoDto.
    public sealed class ProjetoResumoDto
    {
        public Guid Id { get; set; }
        public string Nome { get; set; } = default!;
        public string Caminho { get; set; } = default!;
        public string Stack { get; set; } = default!;
        public bool Ativo { get; set; }
        public List<AmbienteProjetoDto> Ambientes { get; set; } = [];
        public List<Guid> DependeDe { get; set; } = [];
    }

    // Item da lista de projetos do grupo no GitLab (GET /aplicacoes/{id}/gitlab/projetos).
    public sealed class ProjetoGitLabItemDto
    {
        public long ProjetoGitLabId { get; set; }
        public string Caminho { get; set; } = default!;
        public string Nome { get; set; } = default!;
        public bool Cadastrado { get; set; }
        // `dotnet` | `angular` | null (sem sugestão).
        public string? StackSugerida { get; set; }
    }

    public sealed class PapelResumoDto
    {
        public Guid Id { get; set; }
        public string Usuario { get; set; } = default!;
        // Da Pessoa, quando ela existir; ausentes (null) se o sub não está mais em Pessoa.
        public string? Nome { get; set; }
        public string? Email { get; set; }
        public string Papel { get; set; } = default!;
    }

    // Item da busca de pessoas (GET /pessoas). O identificador na API é o `sub` (o que PapelAplicacao.Usuario guarda); o Id long nunca sai.
    public sealed class PessoaListaDto
    {
        public string Sub { get; set; } = default!;
        public string Nome { get; set; } = default!;
        public string Email { get; set; } = default!;
    }

    // Workspace da Aplicação (P1): projeto do GitLab onde a Esteira lê mapa, specs e issues.
    public sealed class WorkspaceDto
    {
        public long ProjetoGitLabId { get; set; }
        public string Caminho { get; set; } = default!;
    }

    public sealed class AplicacaoDetalheDto
    {
        public Guid Id { get; set; }
        public string Nome { get; set; } = default!;
        public string Sigla { get; set; } = default!;
        public string Grupo { get; set; } = default!;
        public bool Ativa { get; set; }
        public WorkspaceDto? Workspace { get; set; }
        public List<PoliticaDto> Politicas { get; set; } = [];
        public List<ProjetoResumoDto> Projetos { get; set; } = [];
        public List<PapelResumoDto> Papeis { get; set; } = [];
    }
}
