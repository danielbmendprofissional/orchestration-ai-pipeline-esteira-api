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

    // Resumos mínimos: Projetos/Destinos completos são da A1.4 e papéis/pessoas da A1.5, que estendem estes tipos.
    public sealed class ProjetoResumoDto
    {
        public Guid Id { get; set; }
        public string Nome { get; set; } = default!;
        public string Caminho { get; set; } = default!;
        public string Stack { get; set; } = default!;
        public bool Ativo { get; set; }
    }

    public sealed class PapelResumoDto
    {
        public Guid Id { get; set; }
        public string Usuario { get; set; } = default!;
        public string Papel { get; set; } = default!;
    }

    public sealed class AplicacaoDetalheDto
    {
        public Guid Id { get; set; }
        public string Nome { get; set; } = default!;
        public string Sigla { get; set; } = default!;
        public string Grupo { get; set; } = default!;
        public bool Ativa { get; set; }
        public List<PoliticaDto> Politicas { get; set; } = [];
        public List<ProjetoResumoDto> Projetos { get; set; } = [];
        public List<PapelResumoDto> Papeis { get; set; } = [];
    }
}
