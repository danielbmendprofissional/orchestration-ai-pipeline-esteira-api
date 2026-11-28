using Esteira.Application.Interfaces.Auditoria;
using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Aplicacoes;
using Esteira.Application.Aplicacoes.Commands.AlterarAmbiente;
using Esteira.Application.Aplicacoes.Commands.Destinos;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using NSubstitute;

namespace Esteira.Application.Tests.Aplicacoes
{
    // A1.4: habilitar/desabilitar Ambiente (critério 9) e cadastrar Destinos (critério 10).
    [Trait("Fatia", "A1")]
    public class AmbientesEDestinosTests : CadastroTestBase
    {
        private readonly AmbienteProjetoEntity _hml;
        private readonly AmbienteProjetoEntity _producao;

        public AmbientesEDestinosTests()
        {
            _hml = AmbienteProjetoEntity.Criar(Projeto.Id, AmbienteDeploy.Hml).Value;
            _producao = AmbienteProjetoEntity.Criar(Projeto.Id, AmbienteDeploy.Producao).Value;
            Projetos.ObterAmbienteAsync(Projeto.Id, AmbienteDeploy.Hml, Arg.Any<CancellationToken>()).Returns(_hml);
            Projetos.ObterAmbienteAsync(Projeto.Id, AmbienteDeploy.Producao, Arg.Any<CancellationToken>()).Returns(_producao);
        }

        private static Dictionary<string, string> Cfg(string chave = "KUBE_CONTEXT", string valor = "hml") => new() { [chave] = valor };

        // ---- Ambiente ----

        private Task<Result<ProjetoResumoDto>> Ambiente(string ambiente, bool? habilitado, Guid? projeto = null)
            => new AlterarAmbienteHandler(Autorizacao, Projetos, Auditoria)
                .Handle(new AlterarAmbienteCommand(projeto ?? Projeto.PublicId, ambiente, habilitado), Ct);

        [Fact, DisplayName("Desabilita um ambiente opcional (hml) e audita antes/depois")]
        public async Task DesabilitaHml()
        {
            var r = await Ambiente("hml", false);

            r.IsSuccess.Should().BeTrue();
            _hml.Habilitado.Should().BeFalse();
            Auditoria.Received(1).Registrar("AmbienteProjeto", _hml.PublicId, AcaoAuditoria.Alterou, Arg.Is<object?>(o => o != null), Arg.Is<object?>(o => o != null), Arg.Any<DadosDoRegistro>());
        }

        [Fact, DisplayName("Critério 9: desabilitar producao é recusado (ambiente.producao-obrigatorio) e nada é auditado")]
        public async Task ProducaoObrigatorio()
        {
            var r = await Ambiente("producao", false);

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("ambiente.producao-obrigatorio");
            _producao.Habilitado.Should().BeTrue();
            Auditoria.DidNotReceiveWithAnyArgs().Registrar(default!, default, default, default, default, default!);
        }

        [Theory, DisplayName("Validação do ambiente: dev e vazio são inválidos; habilitado é obrigatório")]
        [InlineData("dev", true, "ambiente.invalido")]
        [InlineData("review", true, "ambiente.invalido")]
        [InlineData("hml", null, "ambiente.habilitado-obrigatorio")]
        public void ValidacaoAmbiente(string ambiente, bool? habilitado, string codigo)
        {
            new AlterarAmbienteValidator().Validate(new AlterarAmbienteCommand(Guid.NewGuid(), ambiente, habilitado))
                .Errors.Should().ContainSingle().Which.ErrorCode.Should().Be(codigo);
        }

        [Fact, DisplayName("Ambiente sem linha no Projeto responde ambiente.nao-encontrado")]
        public async Task AmbienteInexistente()
        {
            var r = await Ambiente("qa", false);

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("ambiente.nao-encontrado");
        }

        [Fact, DisplayName("Ambiente: sem permissão devolve o erro da autorização")]
        public async Task AmbienteSemPermissao()
        {
            NegarAutorizacao(AutorizacaoAplicacao.PapelInsuficiente);

            (await Ambiente("hml", false)).Errors.Should().ContainSingle().Which.Should().Be(AutorizacaoAplicacao.PapelInsuficiente);
            _hml.Habilitado.Should().BeTrue();
        }

        // ---- Destinos ----

        private Task<Result<DestinoDto>> Criar(string ambiente = "hml", string nome = "k8s-hml", Dictionary<string, string>? cfg = null, int? ordem = 1)
            => new CriarDestinoHandler(Autorizacao, Projetos, Uow, Auditoria)
                .Handle(new CriarDestinoCommand(Projeto.PublicId, ambiente, nome, cfg ?? Cfg(), ordem), Ct);

        [Fact, DisplayName("Cria Destino no ambiente habilitado, com configuração e ordem, e audita")]
        public async Task CriaDestino()
        {
            DestinoEntity? criado = null;
            await Projetos.AdicionarDestinoAsync(Arg.Do<DestinoEntity>(d => criado = d), Arg.Any<CancellationToken>());

            var r = await Criar();

            r.IsSuccess.Should().BeTrue();
            (criado!.ProjetoId, criado.Ambiente, criado.Nome, criado.Ordem).Should().Be((Projeto.Id, AmbienteDeploy.Hml, "k8s-hml", 1));
            criado.Configuracao.Should().ContainKey("KUBE_CONTEXT");
            r.Value.Should().BeEquivalentTo(new DestinoDto { Id = criado.PublicId, Nome = "k8s-hml", Configuracao = Cfg(), Ordem = 1 });
            Auditoria.Received(1).Registrar("Destino", criado.PublicId, AcaoAuditoria.Criou, Arg.Is<object?>(o => o == null), Arg.Is<object?>(o => o != null), Arg.Any<DadosDoRegistro>());
        }

        [Theory, DisplayName("Critério 10: chave com cara de segredo é recusada (destino.configuracao-com-segredo) e não grava nem audita")]
        [InlineData("DB_PASSWORD")]
        [InlineData("api_token")]
        public async Task Segredo(string chave)
        {
            var r = await Criar(cfg: Cfg(chave, "valor-secreto-123"));

            var erro = r.Errors.Should().ContainSingle().Subject;
            erro.Code.Should().Be("destino.configuracao-com-segredo");
            erro.Message.Should().Contain("GitLab").And.NotContain("valor-secreto-123");
            await Projetos.DidNotReceiveWithAnyArgs().AdicionarDestinoAsync(default!, default);
            Auditoria.DidNotReceiveWithAnyArgs().Registrar(default!, default, default, default, default, default!);
        }

        [Fact, DisplayName("Nome de Destino repetido no mesmo Projeto+Ambiente é recusado (destino.nome-duplicado)")]
        public async Task NomeDuplicado()
        {
            Projetos.DestinoNomeExisteAsync(Projeto.Id, AmbienteDeploy.Hml, "k8s-hml", null, Arg.Any<CancellationToken>()).Returns(true);

            var r = await Criar(nome: "  k8s-hml ");

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("destino.nome-duplicado");
            await Projetos.DidNotReceiveWithAnyArgs().AdicionarDestinoAsync(default!, default);
        }

        [Fact, DisplayName("Corrida: violação de unicidade do nome vira destino.nome-duplicado")]
        public async Task NomeCorrida()
        {
            Uow.SaveChangesAsync().Returns<int>(_ => throw new ViolacaoDeUnicidadeException("IX_Destino_ProjetoId_Ambiente_Nome"));

            (await Criar()).Errors.Should().ContainSingle().Which.Code.Should().Be("destino.nome-duplicado");
        }

        [Fact, DisplayName("Ambiente desabilitado não aceita Destino (ambiente.desabilitado)")]
        public async Task AmbienteDesabilitado()
        {
            _hml.Alterar(false);

            var r = await Criar();

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("ambiente.desabilitado");
            await Projetos.DidNotReceiveWithAnyArgs().AdicionarDestinoAsync(default!, default);
        }

        [Fact, DisplayName("Destino: sem permissão devolve o erro da autorização")]
        public async Task DestinoSemPermissao()
        {
            NegarAutorizacao(AutorizacaoAplicacao.PapelInsuficiente);

            (await Criar()).Errors.Should().ContainSingle().Which.Should().Be(AutorizacaoAplicacao.PapelInsuficiente);
        }

        [Theory, DisplayName("Validação do Destino: ambiente válido e configuração obrigatória")]
        [InlineData("dev", false, "ambiente.invalido")]
        [InlineData("hml", true, "destino.configuracao-obrigatoria")]
        public void ValidacaoDestino(string ambiente, bool semConfiguracao, string codigo)
        {
            var r = new CriarDestinoValidator().Validate(new CriarDestinoCommand(Guid.NewGuid(), ambiente, "n", semConfiguracao ? null : Cfg(), 0));

            r.Errors.Should().ContainSingle().Which.ErrorCode.Should().Be(codigo);
        }

        private DestinoEntity Existente(string nome = "k8s-hml")
        {
            var d = DestinoEntity.Criar(Projeto.Id, AmbienteDeploy.Hml, nome, Cfg(), 0).Value;
            Projetos.ObterDestinoAsync(Projeto.Id, AmbienteDeploy.Hml, d.PublicId, Arg.Any<CancellationToken>()).Returns(d);
            return d;
        }

        private Task<Result<DestinoDto>> Alterar(Guid id, string nome = "novo", Dictionary<string, string>? cfg = null, int? ordem = 5)
            => new AlterarDestinoHandler(Autorizacao, Projetos, Uow, Auditoria)
                .Handle(new AlterarDestinoCommand(Projeto.PublicId, "hml", id, nome, cfg ?? Cfg("NAMESPACE", "valuto"), ordem), Ct);

        [Fact, DisplayName("Altera nome, configuração e ordem do Destino e audita antes/depois")]
        public async Task AlteraDestino()
        {
            var d = Existente();

            var r = await Alterar(d.PublicId);

            r.IsSuccess.Should().BeTrue();
            (d.Nome, d.Ordem).Should().Be(("novo", 5));
            d.Configuracao.Should().ContainKey("NAMESPACE");
            Auditoria.Received(1).Registrar("Destino", d.PublicId, AcaoAuditoria.Alterou, Arg.Is<object?>(o => o != null), Arg.Is<object?>(o => o != null), Arg.Any<DadosDoRegistro>());
        }

        [Fact, DisplayName("Critério 10: alterar com chave de segredo é recusado e o Destino fica como estava")]
        public async Task AlteraComSegredo()
        {
            var d = Existente();

            var r = await Alterar(d.PublicId, cfg: Cfg("SECRET_KEY", "x"));

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("destino.configuracao-com-segredo");
            d.Nome.Should().Be("k8s-hml");
            Auditoria.DidNotReceiveWithAnyArgs().Registrar(default!, default, default, default, default, default!);
        }

        [Fact, DisplayName("Alterar para nome de outro Destino do Projeto+Ambiente é recusado (destino.nome-duplicado)")]
        public async Task AlteraNomeDuplicado()
        {
            var d = Existente();
            Projetos.DestinoNomeExisteAsync(Projeto.Id, AmbienteDeploy.Hml, "novo", d.Id, Arg.Any<CancellationToken>()).Returns(true);

            var r = await Alterar(d.PublicId);

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("destino.nome-duplicado");
            d.Nome.Should().Be("k8s-hml");
        }

        [Fact, DisplayName("Destino inexistente (alterar/remover) responde destino.nao-encontrado")]
        public async Task DestinoInexistente()
        {
            (await Alterar(Guid.NewGuid())).Errors.Should().ContainSingle().Which.Code.Should().Be("destino.nao-encontrado");
            var r = await new RemoverDestinoHandler(Autorizacao, Projetos, Auditoria)
                .Handle(new RemoverDestinoCommand(Projeto.PublicId, "hml", Guid.NewGuid()), Ct);
            r.Errors.Should().ContainSingle().Which.Code.Should().Be("destino.nao-encontrado");
        }

        [Fact, DisplayName("Remove o Destino e audita (depois = null)")]
        public async Task Remove()
        {
            var d = Existente();

            var r = await new RemoverDestinoHandler(Autorizacao, Projetos, Auditoria)
                .Handle(new RemoverDestinoCommand(Projeto.PublicId, "hml", d.PublicId), Ct);

            r.IsSuccess.Should().BeTrue();
            Projetos.Received(1).RemoverDestino(d);
            Auditoria.Received(1).Registrar("Destino", d.PublicId, AcaoAuditoria.Removeu, Arg.Is<object?>(o => o != null), Arg.Is<object?>(o => o == null), Arg.Any<DadosDoRegistro>());
        }
    }
}
