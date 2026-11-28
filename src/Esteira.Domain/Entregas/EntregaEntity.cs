using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Common.Validations;

namespace Esteira.Domain.Entregas
{
    public sealed record FatiaDaEntrega(string Codigo, bool ComFlag);

    // Um Projeto tocado pela Entrega. QaHabilitado: o Ambiente qa do Projeto está habilitado no cadastro.
    public sealed record FluxoDaEntrega(ProjetoEntity Projeto, bool QaHabilitado, TipoMudanca TipoMudanca, IReadOnlyList<FatiaDaEntrega> Fatias);

    // Raiz do agregado: contém os Fluxos (um por Projeto) e as Fatias de cada Fluxo (ADR-0009, ADR-0022).
    public sealed class EntregaEntity : AggregateRoot
    {
        private readonly List<FluxoEntity> _fluxos = [];

        private EntregaEntity() { }

        public long AplicacaoId { get; private set; }
        public SituacaoEntrega Situacao { get; private set; }
        public long AbertaPorPessoaId { get; private set; }
        public DateTime AbertaEm { get; private set; }
        public IReadOnlyList<FluxoEntity> Fluxos => _fluxos;

        // Calculado das Etapas dos Fluxos, nunca gravado (E1b).
        public EstadoDaEntrega Estado => EstadoDaEntrega.Calcular(_fluxos.SelectMany(f => f.Etapas));

        public static Result<EntregaEntity> Abrir(AplicacaoEntity aplicacao, PessoaEntity pessoa, IReadOnlyList<FluxoDaEntrega> fluxos, DateTime abertaEm)
        {
            Guard.AgainstNull(aplicacao, nameof(aplicacao));
            Guard.AgainstNull(pessoa, nameof(pessoa));
            Guard.AgainstNull(fluxos, nameof(fluxos));

            if (fluxos.Count == 0 || fluxos.Any(f => f.Fatias is null || f.Fatias.Count == 0))
                return new Erro("entrega.sem-fatias", "Selecione pelo menos uma Fatia para abrir a Entrega.", TipoErro.Validation);

            var entrega = new EntregaEntity
            {
                AplicacaoId = aplicacao.Id,
                Situacao = SituacaoEntrega.Aberta,
                AbertaPorPessoaId = pessoa.Id,
                AbertaEm = abertaEm
            };
            var projetosVistos = new HashSet<Guid>();

            foreach (var item in fluxos)
            {
                Guard.AgainstNull(item.Projeto, nameof(item.Projeto));

                if (item.Projeto.AplicacaoId != aplicacao.Id || !item.Projeto.Ativo)
                    return new Erro("entrega.projeto-invalido", "O Projeto não pertence à Aplicação ou está inativo.", TipoErro.Validation);

                if (!item.QaHabilitado)
                    return new Erro("ambiente.desabilitado", "O Ambiente qa não está habilitado no Projeto.", TipoErro.Validation);

                if (!projetosVistos.Add(item.Projeto.PublicId))
                    return new Erro("entrega.fluxo-duplicado", "A Entrega só pode ter um Fluxo por Projeto.", TipoErro.Validation);

                var fluxo = FluxoEntity.Criar(item.Projeto.Id, item.TipoMudanca, item.Fatias, abertaEm);
                if (!fluxo.IsSuccess)
                    return Result<EntregaEntity>.Falha([.. fluxo.Errors]);

                entrega._fluxos.Add(fluxo.Value);
            }

            return Result<EntregaEntity>.Sucesso(entrega);
        }
    }
}
