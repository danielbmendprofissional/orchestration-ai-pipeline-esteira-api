using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Interfaces.Auditoria;
using Esteira.Application.Aplicacoes;
using Esteira.Application.Aplicacoes.Commands.AlterarProjeto;
using Esteira.Application.Aplicacoes.Commands.SubstituirDependencias;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using NSubstitute;

namespace Esteira.Application.Tests.Aplicacoes
{
    // A1.4: PUT /projetos/{id} (stack, ativo) e PUT /projetos/{id}/dependencias (critério 15).
    [Trait("Fatia", "A1")]
    public class AlterarProjetoEDependenciasTests : CadastroTestBase
    {
        private readonly ProjetoEntity _b;
        private readonly ProjetoEntity _c;
        private readonly ProjetoEntity _d;

        public AlterarProjetoEDependenciasTests()
        {
            _b = NovoProjeto(11, 100);
            _c = NovoProjeto(12, 101);
            _d = NovoProjeto(13, 102);
            Projetos.ProjetosDaAplicacaoAsync(AplicacaoId, Arg.Any<CancellationToken>()).Returns([Projeto, _b, _c, _d]);
            Projetos.DependenciasDaAplicacaoAsync(AplicacaoId, Arg.Any<CancellationToken>()).Returns(new List<DependenciaProjetoEntity>());
        }

        private Task<Result<ProjetoResumoDto>> Dependencias(params Guid[] dependeDe)
            => new SubstituirDependenciasHandler(Autorizacao, Projetos, Auditoria)
                .Handle(new SubstituirDependenciasCommand(Projeto.PublicId, dependeDe), Ct);

        private static DependenciaProjetoEntity Dep(ProjetoEntity de, ProjetoEntity para) => DependenciaProjetoEntity.Criar(de.Id, para.Id).Value;

        // ---- PUT /projetos/{id} ----

        [Fact, DisplayName("Altera stack e ativo do Projeto e audita antes/depois")]
        public async Task Altera()
        {
            var r = await new AlterarProjetoHandler(Autorizacao, Projetos, Auditoria)
                .Handle(new AlterarProjetoCommand(Projeto.PublicId, "angular", false), Ct);

            r.IsSuccess.Should().BeTrue();
            (Projeto.Stack, Projeto.Ativo).Should().Be((Stack.Angular, false));
            Auditoria.Received(1).Registrar("Projeto", Projeto.PublicId, AcaoAuditoria.Alterou, Arg.Is<object?>(o => o != null), Arg.Is<object?>(o => o != null), Arg.Any<DadosDoRegistro>());
        }

        [Fact, Trait("Fatia", "X1"), DisplayName("X1 critério 1: alterar o Projeto grava a Aplicação dona, categoria cadastro e origem tela")]
        public async Task Altera_AuditaComAplicacao()
        {
            await new AlterarProjetoHandler(Autorizacao, Projetos, Auditoria)
                .Handle(new AlterarProjetoCommand(Projeto.PublicId, "angular", false), Ct);

            Auditoria.Received(1).Registrar("Projeto", Projeto.PublicId, AcaoAuditoria.Alterou, Arg.Any<object?>(), Arg.Any<object?>(),
                Arg.Is<DadosDoRegistro>(d => d.AplicacaoId == AplicacaoId && d.Categoria == CategoriaAuditoria.Cadastro && d.Origem == OrigemAuditoria.Tela));
        }

        [Fact, DisplayName("Projeto inexistente responde projeto.nao-encontrado")]
        public async Task Inexistente()
        {
            var r = await new AlterarProjetoHandler(Autorizacao, Projetos, Auditoria)
                .Handle(new AlterarProjetoCommand(Guid.NewGuid(), "dotnet", true), Ct);

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("projeto.nao-encontrado");
        }

        [Fact, DisplayName("Sem permissão devolve o erro da autorização e não altera")]
        public async Task SemPermissao()
        {
            NegarAutorizacao(AutorizacaoAplicacao.PapelInsuficiente);

            var r = await new AlterarProjetoHandler(Autorizacao, Projetos, Auditoria)
                .Handle(new AlterarProjetoCommand(Projeto.PublicId, "angular", false), Ct);

            r.Errors.Should().ContainSingle().Which.Should().Be(AutorizacaoAplicacao.PapelInsuficiente);
            Projeto.Stack.Should().Be(Stack.Dotnet);
        }

        [Theory, DisplayName("Validação: stack válida e ativo obrigatório")]
        [InlineData("cobol", true, "projeto.stack-invalida")]
        [InlineData("dotnet", null, "projeto.ativo-obrigatorio")]
        public void Validacao(string stack, bool? ativo, string codigo)
        {
            new AlterarProjetoValidator().Validate(new AlterarProjetoCommand(Guid.NewGuid(), stack, ativo))
                .Errors.Should().ContainSingle().Which.ErrorCode.Should().Be(codigo);
        }

        // ---- PUT /projetos/{id}/dependencias ----

        [Fact, DisplayName("Critério 15: substitui a lista — grava as novas, remove as que saíram e audita antes/depois")]
        public async Task Substitui()
        {
            var antiga = Dep(Projeto, _b);
            Projetos.DependenciasDaAplicacaoAsync(AplicacaoId, Arg.Any<CancellationToken>()).Returns(new List<DependenciaProjetoEntity> { antiga });
            var adicionadas = new List<DependenciaProjetoEntity>();
            await Projetos.AdicionarDependenciaAsync(Arg.Do<DependenciaProjetoEntity>(adicionadas.Add), Arg.Any<CancellationToken>());

            var r = await Dependencias(_c.PublicId, _d.PublicId);

            r.IsSuccess.Should().BeTrue();
            Projetos.Received(1).RemoverDependencia(antiga);
            adicionadas.Select(d => (d.ProjetoId, d.DependeDeProjetoId)).Should().BeEquivalentTo([(10L, 12L), (10L, 13L)]);
            Auditoria.Received(1).Registrar("Projeto", Projeto.PublicId, AcaoAuditoria.Alterou, Arg.Is<object?>(o => o != null), Arg.Is<object?>(o => o != null), Arg.Any<DadosDoRegistro>());
        }

        [Fact, DisplayName("Critério 15: manter uma dependência já gravada não a remove nem a duplica")]
        public async Task Mantem()
        {
            var existente = Dep(Projeto, _b);
            Projetos.DependenciasDaAplicacaoAsync(AplicacaoId, Arg.Any<CancellationToken>()).Returns(new List<DependenciaProjetoEntity> { existente });

            var r = await Dependencias(_b.PublicId);

            r.IsSuccess.Should().BeTrue();
            Projetos.DidNotReceiveWithAnyArgs().RemoverDependencia(default!);
            await Projetos.DidNotReceiveWithAnyArgs().AdicionarDependenciaAsync(default!, default);
        }

        [Fact, DisplayName("Critério 15: lista vazia remove todas as dependências")]
        public async Task Esvazia()
        {
            var existente = Dep(Projeto, _b);
            Projetos.DependenciasDaAplicacaoAsync(AplicacaoId, Arg.Any<CancellationToken>()).Returns(new List<DependenciaProjetoEntity> { existente });

            var r = await Dependencias();

            r.IsSuccess.Should().BeTrue();
            Projetos.Received(1).RemoverDependencia(existente);
        }

        [Fact, DisplayName("Critério 15: Projeto de outra Aplicação (ou inexistente) é recusado (projeto.dependencia-invalida)")]
        public async Task OutraAplicacao()
        {
            var r = await Dependencias(Guid.NewGuid());

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("projeto.dependencia-invalida");
            await Projetos.DidNotReceiveWithAnyArgs().AdicionarDependenciaAsync(default!, default);
            Auditoria.DidNotReceiveWithAnyArgs().Registrar(default!, default, default, default, default, default!);
        }

        [Fact, DisplayName("Critério 15: dependência do próprio Projeto é recusada")]
        public async Task Propria()
        {
            var r = await Dependencias(Projeto.PublicId);

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("projeto.dependencia-invalida");
        }

        [Fact, DisplayName("Critério 15: dependência que fecha ciclo é recusada")]
        public async Task Ciclo()
        {
            // B depende de A; A passar a depender de B fecha o ciclo.
            Projetos.DependenciasDaAplicacaoAsync(AplicacaoId, Arg.Any<CancellationToken>()).Returns(new List<DependenciaProjetoEntity> { Dep(_b, Projeto) });

            var r = await Dependencias(_b.PublicId);

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("projeto.dependencia-invalida");
            await Projetos.DidNotReceiveWithAnyArgs().AdicionarDependenciaAsync(default!, default);
        }

        [Fact, DisplayName("Sem permissão devolve o erro da autorização")]
        public async Task DependenciasSemPermissao()
        {
            NegarAutorizacao(AutorizacaoAplicacao.PapelInsuficiente);

            var r = await Dependencias(_b.PublicId);

            r.Errors.Should().ContainSingle().Which.Should().Be(AutorizacaoAplicacao.PapelInsuficiente);
        }

        [Fact, DisplayName("Validação: dependeDe é obrigatório (para não esvaziar por omissão)")]
        public void DependeDeObrigatorio()
        {
            new SubstituirDependenciasValidator().Validate(new SubstituirDependenciasCommand(Guid.NewGuid(), null))
                .Errors.Should().ContainSingle().Which.ErrorCode.Should().Be("projeto.depende-de-obrigatorio");
        }
    }
}
