using System.Reflection;
using Esteira.Application.Aplicacoes;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Auditoria;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Resultados;
using NSubstitute;

namespace Esteira.Application.Tests.Aplicacoes
{
    // Base dos testes da A1.4: um Projeto (Id 10, Aplicação 7) resolvido pelo PublicId, autorização liberada para editar cadastro.
    public abstract class CadastroTestBase
    {
        protected const long AplicacaoId = 7;

        protected static CancellationToken Ct => TestContext.Current.CancellationToken;

        protected readonly IAutorizacaoAplicacao Autorizacao = Substitute.For<IAutorizacaoAplicacao>();
        protected readonly IProjetoRepositorio Projetos = Substitute.For<IProjetoRepositorio>();
        protected readonly IUnitOfWork Uow = Substitute.For<IUnitOfWork>();
        protected readonly IAuditoria Auditoria = Substitute.For<IAuditoria>();
        protected readonly ProjetoEntity Projeto;

        protected CadastroTestBase()
        {
            Projeto = NovoProjeto(10, 99);
            Projetos.ObterPorPublicIdAsync(Projeto.PublicId, Arg.Any<CancellationToken>()).Returns(Projeto);
            Autorizacao.AutorizarAsync(AplicacaoId, OperacaoAplicacao.EditarCadastro, Arg.Any<CancellationToken>()).Returns(Result.Sucesso());
            Projetos.MontarDtoAsync(Arg.Any<ProjetoEntity>(), Arg.Any<CancellationToken>())
                .Returns(c => new ProjetoResumoDto { Id = c.Arg<ProjetoEntity>().PublicId, Nome = "api" });
        }

        protected static ProjetoEntity NovoProjeto(long id, long gitlabId)
        {
            var projeto = ProjetoEntity.Criar(AplicacaoId, gitlabId, $"valuto/p{gitlabId}", $"p{gitlabId}", Stack.Dotnet);
            DefinirId(projeto, id);
            return projeto;
        }

        // `Id` tem setter interno ao Domain (quem atribui é o banco); o teste simula o que o banco faria.
        protected static T DefinirId<T>(T entidade, long id) where T : EntityBase
        {
            typeof(EntityBase).GetProperty(nameof(EntityBase.Id), BindingFlags.Instance | BindingFlags.Public)!.SetValue(entidade, id);
            return entidade;
        }

        protected void NegarAutorizacao(Erro erro)
            => Autorizacao.AutorizarAsync(AplicacaoId, OperacaoAplicacao.EditarCadastro, Arg.Any<CancellationToken>()).Returns(Result.Falha(erro));
    }
}
