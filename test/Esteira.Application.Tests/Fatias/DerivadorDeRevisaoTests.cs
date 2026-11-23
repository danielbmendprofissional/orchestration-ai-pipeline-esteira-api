using AwesomeAssertions;
using Esteira.Application.Aplicacoes;
using Esteira.Application.Fatias;
using Esteira.Application.Fatias.Decisoes;
using Esteira.Application.Interfaces.Fatias;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Fatias;

namespace Esteira.Application.Tests.Fatias
{
    // P2.5, critérios 12, 13 e 14: o que a pessoa pode fazer, o estado do review e da decisão e o link, só de papel, estado e Ações.
    [Trait("Fatia", "P2")]
    public class DerivadorDeRevisaoTests
    {
        private const string Modelo = "https://{fatia}.preview.exemplo.com";

        private static readonly AplicacaoNoPainel App = new(
            Guid.NewGuid(), "App", "APP", "g", 10, "g/ws",
            [new ProjetoNoPainel(RepositorioFatia.Api, 1, "g/api", 100), new ProjetoNoPainel(RepositorioFatia.Frontend, 2, "g/front", 200)]);

        private static AcessoAplicacao Acesso(params PapelAplicacao[] papeis) => new(false, papeis);

        private static MrDaGaveta Mr(RepositorioFatia repo, long iid, string estado = "opened", string? ci = "success")
            => new(repo, iid, "P2 — T", estado, "feature/p2", ci);

        private static FatiaLida Fatia(EstadoDaFatia estado, RepositorioFatia[] repos, params MrDaGaveta[] mrs) => new(
            "P2", "T", "—", true, repos, "alto", true, "specs/x.md", 7, new ResultadoDeEstado(estado, null, false, false), false,
            new DetalheDaFatia(null, null, [], [], mrs));

        private static FatiaLida EmRevisao(RepositorioFatia[]? repos = null, string? ci = "success")
            => Fatia(EstadoDaFatia.EmRevisao, repos ?? [RepositorioFatia.Api, RepositorioFatia.Frontend],
                Mr(RepositorioFatia.Api, 3, ci: ci), Mr(RepositorioFatia.Frontend, 8, ci: ci));

        private static AcaoNaFatia Acao(
            TipoAcaoBot tipo, EstadoAcaoBot estado, RepositorioFatia repo = RepositorioFatia.Api, string? comentario = null, string codigo = "P2",
            DateTime? em = null)
            => new(Guid.NewGuid(), codigo, tipo, estado, repo == RepositorioFatia.Api ? 100 : 200, repo, repo == RepositorioFatia.Api ? 3 : 8,
                "Ana", em ?? new DateTime(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 23, 10, 5, 0, DateTimeKind.Utc),
                estado == EstadoAcaoBot.Falhou ? "gitlab.indisponivel" : null, comentario);

        private static RevisaoDaFatia Derivar(AcessoAplicacao acesso, FatiaLida fatia, params AcaoNaFatia[] acoes)
            => DerivadorDeRevisao.Derivar(acesso, App, fatia, acoes, Modelo);

        [Fact]
        public void Membro_pode_abrir_review_mas_nao_decidir_e_o_motivo_e_o_papel()
        {
            var r = Derivar(Acesso(PapelAplicacao.Membro), EmRevisao());

            r.Acoes.PodeAbrirReview.Should().Be(new PermissaoDaAcaoDto(true, null));
            r.Acoes.PodeAprovar.Should().Be(new PermissaoDaAcaoDto(false, "papel"));
            r.Acoes.PodeReprovar.Should().Be(new PermissaoDaAcaoDto(false, "papel"));
        }

        [Fact]
        public void Analista_sem_papel_de_decisao_nao_faz_nada_e_mor_faz_tudo()
        {
            var analista = Derivar(Acesso(PapelAplicacao.Analista), EmRevisao());
            var mor = Derivar(new AcessoAplicacao(true, []), EmRevisao());

            analista.Acoes.PodeAbrirReview.Motivo.Should().Be("papel");
            analista.Acoes.PodeAprovar.Pode.Should().BeFalse();
            mor.Acoes.Should().Be(new AcoesDaFatiaDto(new(true, null), new(true, null), new(true, null)));
        }

        [Fact]
        public void Aprovador_tecnico_com_ci_verde_pode_aprovar_e_reprovar()
        {
            var r = Derivar(Acesso(PapelAplicacao.AprovadorTecnico), EmRevisao());

            r.Acoes.PodeAprovar.Pode.Should().BeTrue();
            r.Acoes.PodeReprovar.Pode.Should().BeTrue();
        }

        [Theory]
        [InlineData("failed")]
        [InlineData("running")]
        [InlineData(null)]
        public void Ci_nao_verde_bloqueia_so_o_aprovar(string? ci)
        {
            var r = Derivar(Acesso(PapelAplicacao.AprovadorTecnico), EmRevisao(ci: ci));

            r.Acoes.PodeAprovar.Should().Be(new PermissaoDaAcaoDto(false, "ci-nao-verde"));
            r.Acoes.PodeReprovar.Pode.Should().BeTrue();
        }

        [Theory]
        [InlineData(EstadoDaFatia.EmExecucao)]
        [InlineData(EstadoDaFatia.ProntaParaEntrega)]
        public void Fatia_fora_de_revisao_ou_sem_mr_aberta_nao_pode_nada(EstadoDaFatia estado)
        {
            var fora = Fatia(estado, [RepositorioFatia.Api], Mr(RepositorioFatia.Api, 3));
            var semMr = Fatia(EstadoDaFatia.EmRevisao, [RepositorioFatia.Api], Mr(RepositorioFatia.Api, 3, "merged"));

            foreach (var f in new[] { fora, semMr })
            {
                var r = Derivar(new AcessoAplicacao(true, []), f);
                r.Acoes.PodeAbrirReview.Should().Be(new PermissaoDaAcaoDto(false, "sem-mr-aberta"));
                r.Acoes.PodeAprovar.Should().Be(new PermissaoDaAcaoDto(false, "sem-mr-aberta"));
                r.Acoes.PodeReprovar.Should().Be(new PermissaoDaAcaoDto(false, "sem-mr-aberta"));
            }
        }

        [Theory]
        [InlineData(TipoAcaoBot.Aprovar, EstadoAcaoBot.Pedida, "aprovando")]
        [InlineData(TipoAcaoBot.Aprovar, EstadoAcaoBot.Executando, "aprovando")]
        [InlineData(TipoAcaoBot.Reprovar, EstadoAcaoBot.Pedida, "reprovando")]
        public void Decisao_em_andamento_bloqueia_as_duas_decisoes_e_aparece_no_estado(TipoAcaoBot tipo, EstadoAcaoBot estado, string esperado)
        {
            var r = Derivar(Acesso(PapelAplicacao.AprovadorTecnico), EmRevisao(), Acao(tipo, estado, comentario: tipo == TipoAcaoBot.Reprovar ? "x" : null));

            r.Decisao.Should().Be(esperado);
            r.Acoes.PodeAprovar.Should().Be(new PermissaoDaAcaoDto(false, "decisao-em-andamento"));
            r.Acoes.PodeReprovar.Should().Be(new PermissaoDaAcaoDto(false, "decisao-em-andamento"));
        }

        [Fact]
        public void Decisao_falhada_aparece_como_falhou_e_libera_nova_tentativa()
        {
            var r = Derivar(Acesso(PapelAplicacao.AprovadorTecnico), EmRevisao(), Acao(TipoAcaoBot.Aprovar, EstadoAcaoBot.Falhou));

            r.Decisao.Should().Be("falhou");
            r.Acoes.PodeAprovar.Pode.Should().BeTrue();
        }

        [Fact]
        public void Sem_acao_a_decisao_e_nenhuma_e_decisao_de_outra_fatia_nao_conta()
        {
            var outra = Acao(TipoAcaoBot.Aprovar, EstadoAcaoBot.Pedida, codigo: "P9");

            Derivar(Acesso(PapelAplicacao.AprovadorTecnico), EmRevisao()).Decisao.Should().Be("nenhuma");
            Derivar(Acesso(PapelAplicacao.AprovadorTecnico), EmRevisao(), outra).Decisao.Should().Be("nenhuma");
        }

        [Fact]
        public void Review_sem_acao_e_nenhum_e_com_acao_viva_e_pedido_e_o_botao_some_com_motivo()
        {
            Derivar(Acesso(PapelAplicacao.Membro), EmRevisao()).Review.Should().Be(new ReviewDaFatiaDto("nenhum", null));

            var r = Derivar(Acesso(PapelAplicacao.Membro), EmRevisao(),
                Acao(TipoAcaoBot.AbrirReview, EstadoAcaoBot.Pedida), Acao(TipoAcaoBot.AbrirReview, EstadoAcaoBot.Pedida, RepositorioFatia.Frontend));

            r.Review.Should().Be(new ReviewDaFatiaDto("pedido", null));
            r.Acoes.PodeAbrirReview.Should().Be(new PermissaoDaAcaoDto(false, "review-ja-pedido"));
        }

        [Fact]
        public void Review_falhou_quando_a_ultima_acao_falhou_e_pode_pedir_de_novo()
        {
            var r = Derivar(Acesso(PapelAplicacao.Membro), EmRevisao(), Acao(TipoAcaoBot.AbrirReview, EstadoAcaoBot.Falhou));

            r.Review.Estado.Should().Be("falhou");
            r.Acoes.PodeAbrirReview.Pode.Should().BeTrue();
        }

        [Fact]
        public void Review_aberto_com_front_e_modelo_valido_devolve_o_link_com_o_codigo_em_minusculo()
        {
            var r = Derivar(Acesso(PapelAplicacao.Membro), EmRevisao(),
                Acao(TipoAcaoBot.AbrirReview, EstadoAcaoBot.Confirmada), Acao(TipoAcaoBot.AbrirReview, EstadoAcaoBot.Confirmada, RepositorioFatia.Frontend));

            r.Review.Should().Be(new ReviewDaFatiaDto("aberto", "https://p2.preview.exemplo.com"));
        }

        [Fact]
        public void Fatia_so_de_api_mostra_review_aberto_sem_link()
        {
            var fatia = Fatia(EstadoDaFatia.EmRevisao, [RepositorioFatia.Api], Mr(RepositorioFatia.Api, 3));

            var r = Derivar(Acesso(PapelAplicacao.Membro), fatia, Acao(TipoAcaoBot.AbrirReview, EstadoAcaoBot.Confirmada));

            r.Review.Should().Be(new ReviewDaFatiaDto("aberto", null));
        }

        [Fact]
        public void Review_so_com_a_acao_da_api_confirmada_ainda_e_pedido_e_sem_link()
        {
            var r = Derivar(Acesso(PapelAplicacao.Membro), EmRevisao(),
                Acao(TipoAcaoBot.AbrirReview, EstadoAcaoBot.Confirmada), Acao(TipoAcaoBot.AbrirReview, EstadoAcaoBot.Pedida, RepositorioFatia.Frontend));

            r.Review.Should().Be(new ReviewDaFatiaDto("pedido", null));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("http://{fatia}.preview.exemplo.com")]
        [InlineData("https://preview.exemplo.com")]
        [InlineData("nao-e-url-{fatia}")]
        [InlineData("javascript:{fatia}")]
        public void Modelo_ausente_ou_invalido_nao_devolve_link_e_nao_falha(string? modelo)
        {
            var r = DerivadorDeRevisao.Derivar(Acesso(PapelAplicacao.Membro), App, EmRevisao(),
                [Acao(TipoAcaoBot.AbrirReview, EstadoAcaoBot.Confirmada), Acao(TipoAcaoBot.AbrirReview, EstadoAcaoBot.Confirmada, RepositorioFatia.Frontend)], modelo);

            r.Review.Should().Be(new ReviewDaFatiaDto("aberto", null));
        }

        [Fact]
        public void Codigo_com_caractere_fora_de_letra_numero_e_hifen_nao_vira_link()
        {
            var fatia = EmRevisao() with { Codigo = "P2/../x" };

            var r = DerivadorDeRevisao.Derivar(Acesso(PapelAplicacao.Membro), App, fatia,
                [Acao(TipoAcaoBot.AbrirReview, EstadoAcaoBot.Confirmada, codigo: "P2/../x"), Acao(TipoAcaoBot.AbrirReview, EstadoAcaoBot.Confirmada, RepositorioFatia.Frontend, codigo: "P2/../x")], Modelo);

            r.Review.Link.Should().BeNull();
        }

        [Fact]
        public void Lista_traz_tipo_estado_quem_quando_erro_e_o_comentario_so_na_acao_de_reprovar()
        {
            var r = Derivar(Acesso(PapelAplicacao.AprovadorTecnico), EmRevisao(),
                Acao(TipoAcaoBot.AbrirReview, EstadoAcaoBot.Confirmada, em: new DateTime(2026, 11, 23, 9, 0, 0, DateTimeKind.Utc)),
                Acao(TipoAcaoBot.Reprovar, EstadoAcaoBot.Falhou, comentario: "<b>refaça</b>"));

            r.Lista.Should().HaveCount(2);
            r.Lista[0].Should().Match<AcaoDaFatiaDto>(a => a.Tipo == "abrir-review" && a.Estado == "confirmada" && a.Repositorio == "api" && a.MrIid == 3
                && a.PedidaPor == "Ana" && a.CodigoErro == null && a.Comentario == null);
            r.Lista[1].Should().Match<AcaoDaFatiaDto>(a => a.Tipo == "reprovar" && a.Estado == "falhou" && a.CodigoErro == "gitlab.indisponivel"
                && a.Comentario == "<b>refaça</b>");
        }
    }
}
