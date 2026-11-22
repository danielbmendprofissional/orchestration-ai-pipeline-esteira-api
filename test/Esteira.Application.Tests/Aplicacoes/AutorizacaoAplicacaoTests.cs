using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Aplicacoes;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Usuario;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using NSubstitute;

namespace Esteira.Application.Tests.Aplicacoes
{
    // Critérios 12, 13 e 18 da A1: papel por Aplicação + MOR, calculado num serviço só.
    [Trait("Fatia", "A1")]
    public class AutorizacaoAplicacaoTests
    {
        private const long AplicacaoId = 7;
        private const string Sub = "sub-1";

        private readonly IUsuarioAtual _usuario = Substitute.For<IUsuarioAtual>();
        private readonly IAcessoAplicacaoQuery _query = Substitute.For<IAcessoAplicacaoQuery>();

        private AutorizacaoAplicacao Servico(bool mor, params PapelAplicacao[] papeis)
        {
            _usuario.Id.Returns(Sub);
            _usuario.EhMor.Returns(mor);
            _query.PapeisAsync(AplicacaoId, Sub, Arg.Any<CancellationToken>()).Returns(papeis);
            return new AutorizacaoAplicacao(_usuario, _query);
        }

        public static TheoryData<PapelAplicacao, OperacaoAplicacao, bool> TabelaPapelXOperacao()
        {
            var dados = new TheoryData<PapelAplicacao, OperacaoAplicacao, bool>();
            foreach (var papel in Enum.GetValues<PapelAplicacao>())
            {
                dados.Add(papel, OperacaoAplicacao.Ver, true);
                dados.Add(papel, OperacaoAplicacao.EditarCadastro, papel == PapelAplicacao.Analista);
                dados.Add(papel, OperacaoAplicacao.EditarPolitica, papel == PapelAplicacao.AprovadorTecnico);
                dados.Add(papel, OperacaoAplicacao.GerenciarPapeis, false);
                dados.Add(papel, OperacaoAplicacao.EditarAplicacao, false);
            }
            return dados;
        }

        [Theory, DisplayName("Deve autorizar por papel conforme a tabela papel x operação")]
        [MemberData(nameof(TabelaPapelXOperacao))]
        public async Task Papel_Operacao(PapelAplicacao papel, OperacaoAplicacao operacao, bool permitido)
        {
            var resultado = await Servico(false, papel).AutorizarAsync(AplicacaoId, operacao);

            resultado.IsSuccess.Should().Be(permitido);
            if (!permitido)
            {
                resultado.Errors[0].Code.Should().Be("acesso.papel-insuficiente");
                resultado.Errors[0].Type.Should().Be(TipoErro.Forbidden);
            }
        }

        [Theory, DisplayName("MOR pode toda operação, mesmo sem papel")]
        [InlineData(OperacaoAplicacao.Ver)]
        [InlineData(OperacaoAplicacao.EditarCadastro)]
        [InlineData(OperacaoAplicacao.EditarPolitica)]
        [InlineData(OperacaoAplicacao.GerenciarPapeis)]
        [InlineData(OperacaoAplicacao.EditarAplicacao)]
        public async Task Mor_PodeTudo(OperacaoAplicacao operacao)
        {
            var resultado = await Servico(true).AutorizarAsync(AplicacaoId, operacao);

            resultado.IsSuccess.Should().BeTrue();
        }

        [Theory, DisplayName("Sem papel e sem MOR devolve 404 aplicacao.nao-encontrada, nunca 403")]
        [InlineData(OperacaoAplicacao.Ver)]
        [InlineData(OperacaoAplicacao.EditarCadastro)]
        [InlineData(OperacaoAplicacao.GerenciarPapeis)]
        public async Task SemPapel_NaoRevelaExistencia(OperacaoAplicacao operacao)
        {
            var resultado = await Servico(false).AutorizarAsync(AplicacaoId, operacao);

            resultado.IsSuccess.Should().BeFalse();
            resultado.Errors[0].Code.Should().Be("aplicacao.nao-encontrada");
            resultado.Errors[0].Type.Should().Be(TipoErro.NotFound);
        }

        [Fact, DisplayName("Pessoa com vários papéis soma as permissões")]
        public async Task VariosPapeis_Somam()
        {
            var servico = Servico(false, PapelAplicacao.Analista, PapelAplicacao.AprovadorTecnico);

            (await servico.AutorizarAsync(AplicacaoId, OperacaoAplicacao.EditarCadastro)).IsSuccess.Should().BeTrue();
            (await servico.AutorizarAsync(AplicacaoId, OperacaoAplicacao.EditarPolitica)).IsSuccess.Should().BeTrue();
            (await servico.AutorizarAsync(AplicacaoId, OperacaoAplicacao.GerenciarPapeis)).IsSuccess.Should().BeFalse();
        }

        [Fact, DisplayName("Deve informar os papéis da pessoa na Aplicação")]
        public async Task ObterAcesso_DevolvePapeis()
        {
            var acesso = await Servico(false, PapelAplicacao.Membro, PapelAplicacao.Publicador).ObterAcessoAsync(AplicacaoId);

            acesso.TemPapel.Should().BeTrue();
            acesso.Papeis.Should().BeEquivalentTo([PapelAplicacao.Membro, PapelAplicacao.Publicador]);
            acesso.EhMor.Should().BeFalse();
        }

        [Fact, DisplayName("Permissões do analista: só editar cadastro")]
        public async Task Permissoes_Analista()
        {
            var resultado = await Servico(false, PapelAplicacao.Analista).PermissoesAsync(AplicacaoId);

            resultado.IsSuccess.Should().BeTrue();
            resultado.Value.Should().Be(new PermissoesAplicacao(PodeEditar: false, PodeEditarCadastro: true, PodeEditarPolitica: false, PodeGerenciarPapeis: false));
        }

        [Fact, DisplayName("Permissões do aprovador técnico: só editar política")]
        public async Task Permissoes_AprovadorTecnico()
        {
            var resultado = await Servico(false, PapelAplicacao.AprovadorTecnico).PermissoesAsync(AplicacaoId);

            resultado.Value.Should().Be(new PermissoesAplicacao(false, false, true, false));
        }

        [Fact, DisplayName("Permissões do MOR: tudo")]
        public async Task Permissoes_Mor()
        {
            var resultado = await Servico(true).PermissoesAsync(AplicacaoId);

            resultado.Value.Should().Be(new PermissoesAplicacao(true, true, true, true));
        }

        [Fact, DisplayName("Permissões sem papel e sem MOR: 404 aplicacao.nao-encontrada")]
        public async Task Permissoes_SemPapel()
        {
            var resultado = await Servico(false).PermissoesAsync(AplicacaoId);

            resultado.IsSuccess.Should().BeFalse();
            resultado.Errors[0].Code.Should().Be("aplicacao.nao-encontrada");
            resultado.Errors[0].Type.Should().Be(TipoErro.NotFound);
        }

        [Theory, DisplayName("Só o MOR pode criar Aplicação")]
        [InlineData(true)]
        [InlineData(false)]
        public void PodeCriar_SoMor(bool mor)
        {
            Servico(mor).PodeCriar().Should().Be(mor);
        }
    }
}
