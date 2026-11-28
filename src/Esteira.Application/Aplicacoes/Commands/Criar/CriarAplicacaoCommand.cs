using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Auditoria;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Application.Interfaces.Transaction;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using FluentValidation;
using MediatR;

namespace Esteira.Application.Aplicacoes.Commands.Criar
{
    public sealed record CriarAplicacaoCommand(string Nome, string Sigla, string GrupoGitLab)
        : IRequest<Result<AplicacaoListaDto>>, ITransactionRequest;

    public sealed class CriarAplicacaoValidator : AbstractValidator<CriarAplicacaoCommand>
    {
        public CriarAplicacaoValidator()
        {
            RuleFor(x => x.Nome).NotEmpty().WithErrorCode("aplicacao.nome-obrigatorio").WithMessage("Informe o nome da Aplicação.");
            RuleFor(x => x.Sigla).NotEmpty().WithErrorCode("aplicacao.sigla-obrigatoria").WithMessage("Informe a sigla da Aplicação.");
            RuleFor(x => x.GrupoGitLab).NotEmpty().WithErrorCode("aplicacao.grupo-obrigatorio").WithMessage("Informe o grupo do GitLab.");
        }
    }

    public sealed class CriarAplicacaoHandler(
        IAutorizacaoAplicacao autorizacao,
        IGitLab gitlab,
        IAplicacaoRepositorio repositorio,
        IUnitOfWork uow,
        IAuditoria auditoria) : IRequestHandler<CriarAplicacaoCommand, Result<AplicacaoListaDto>>
    {
        private static readonly AmbientePolitica[] Ambientes = Enum.GetValues<AmbientePolitica>();

        public async Task<Result<AplicacaoListaDto>> Handle(CriarAplicacaoCommand request, CancellationToken cancellationToken)
        {
            if (!autorizacao.PodeCriar())
                return Result<AplicacaoListaDto>.Falha(AutorizacaoAplicacao.PapelInsuficiente);

            var sigla = AplicacaoEntity.NormalizarSigla(request.Sigla);
            if (await repositorio.SiglaExisteAsync(sigla, null, cancellationToken))
                return Result<AplicacaoListaDto>.Falha(AplicacaoErros.SiglaDuplicada);

            var grupo = await gitlab.ObterGrupoAsync(request.GrupoGitLab.Trim(), cancellationToken);
            if (!grupo.IsSuccess)
                return Result<AplicacaoListaDto>.Falha(grupo.Errors.ToArray());

            if (await repositorio.GrupoVinculadoAsync(grupo.Value.Id, cancellationToken))
                return Result<AplicacaoListaDto>.Falha(AplicacaoErros.GrupoJaVinculado);

            var criada = AplicacaoEntity.Criar(request.Nome, sigla, grupo.Value.Id, grupo.Value.Caminho);
            if (!criada.IsSuccess)
                return Result<AplicacaoListaDto>.Falha(criada.Errors.ToArray());

            var aplicacao = criada.Value;
            await repositorio.AdicionarAsync(aplicacao, cancellationToken);

            // Grava já: as Políticas precisam do Id da Aplicação, e a corrida de unicidade aparece aqui.
            try
            {
                await uow.SaveChangesAsync();
            }
            catch (ViolacaoDeUnicidadeException violacao) when (AplicacaoErros.DaViolacao(violacao) is not null)
            {
                return Result<AplicacaoListaDto>.Falha(AplicacaoErros.DaViolacao(violacao)!);
            }

            var politicas = new List<PoliticaSeveridadeEntity>();
            foreach (var ambiente in Ambientes)
            {
                var politica = PoliticaSeveridadeEntity.Padrao(aplicacao.Id, ambiente);
                politicas.Add(politica);
                await repositorio.AdicionarPoliticaAsync(politica, cancellationToken);
            }

            auditoria.Registrar("Aplicacao", aplicacao.PublicId, AcaoAuditoria.Criou, null, new
            {
                aplicacao.Nome,
                aplicacao.Sigla,
                aplicacao.GrupoGitLabId,
                aplicacao.GrupoGitLabCaminho,
                aplicacao.Ativa,
                Politicas = politicas.Select(AplicacaoErros.Dto).ToList()
            }, new DadosDoRegistro(aplicacao.Id, CategoriaAuditoria.Cadastro));

            return Result<AplicacaoListaDto>.Sucesso(AplicacaoErros.Lista(aplicacao));
        }
    }
}
