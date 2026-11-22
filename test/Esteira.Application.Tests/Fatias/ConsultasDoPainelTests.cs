using AwesomeAssertions;
using Esteira.Application.Common.Paginacao;
using Esteira.Application.Fatias;
using Esteira.Application.Fatias.Consultas;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Application.Interfaces.Usuario;
using Esteira.Domain.Common.Resultados;
using NSubstitute;

namespace Esteira.Application.Tests.Fatias
{
    // P1.6, critérios 3, 10, 11, 12, 17 e 19: lista, atualizar e gaveta sobre o instantâneo, com a visibilidade por papel/MOR.
    [Trait("Fatia", "P1")]
    public class ConsultasDoPainelTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private readonly IPainelAplicacaoQuery _apps = Substitute.For<IPainelAplicacaoQuery>();
        private readonly IPainelDeFatias _painel = Substitute.For<IPainelDeFatias>();
        private readonly IUsuarioAtual _usuario = Substitute.For<IUsuarioAtual>();
        private readonly LinksDoGitLab _links = new("https://gitlab.exemplo.com");

        private static readonly AplicacaoNoPainel Alfa = Aplicacao("Alfa", "g/alfa", 10);
        private static readonly AplicacaoNoPainel Beta = Aplicacao("Beta", "g/beta", 20);
        private static readonly AplicacaoNoPainel SemWs = Aplicacao("Sem", "g/sem", null);

        private static AplicacaoNoPainel Aplicacao(string nome, string grupo, long? ws) => new(
            Guid.NewGuid(), nome, nome[..3].ToUpperInvariant(), grupo, ws, ws is null ? null : $"{grupo}/{nome.ToLowerInvariant()}-workspace",
            [new ProjetoNoPainel(RepositorioFatia.Api, 1, $"{grupo}/{nome.ToLowerInvariant()}-api"),
             new ProjetoNoPainel(RepositorioFatia.Frontend, 2, $"{grupo}/{nome.ToLowerInvariant()}-frontend")]);

        public ConsultasDoPainelTests()
        {
            _usuario.Id.Returns("sub-1");
            _usuario.EhMor.Returns(false);
            _apps.ListarVisiveisAsync("sub-1", false, Arg.Any<CancellationToken>()).Returns([Alfa, Beta, SemWs]);
            _painel.ObterAsync(Arg.Any<IReadOnlyList<AplicacaoDoPainel>>(), Arg.Any<CancellationToken>()).Returns(ci =>
                (IReadOnlyList<ResultadoDoPainel>)ci.Arg<IReadOnlyList<AplicacaoDoPainel>>().Select(Resultado).ToList());
        }

        private static readonly DateTimeOffset Agora = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

        private static FatiaLida Fatia(string codigo, EstadoDaFatia estado, RepositorioFatia[]? repos = null, string? titulo = null, DetalheDaFatia? detalhe = null,
            string? progresso = null) => new(
            codigo, titulo ?? $"Título {codigo}", "—", true, repos ?? [RepositorioFatia.Api], "baixo", true, $"specs/feature/x/{codigo}.md", 7,
            new ResultadoDeEstado(estado, progresso, false, false), false, detalhe);

        private static ResultadoDoPainel Resultado(AplicacaoDoPainel e)
        {
            if (e.WorkspaceId is null)
                return new ResultadoDoPainel(e.Chave, SituacaoDoPainel.SemWorkspace, [], null, false, null);
            if (e.WorkspaceId == 20)
                return new ResultadoDoPainel(e.Chave, SituacaoDoPainel.Falha, [], null, false,
                    new Erro("gitlab.indisponivel", "Fora do ar.", TipoErro.Failure));

            return new ResultadoDoPainel(e.Chave, SituacaoDoPainel.Ok,
            [
                Fatia("A1", EstadoDaFatia.ProntaParaEntrega),
                Fatia("A2", EstadoDaFatia.EmRevisao, [RepositorioFatia.Api, RepositorioFatia.Frontend], "Painel bonito"),
                Fatia("A3", EstadoDaFatia.Planejada, [RepositorioFatia.Frontend]),
                Fatia("A4", EstadoDaFatia.ComSpec, [RepositorioFatia.Frontend]),
                Fatia("A10", EstadoDaFatia.EmExecucao),
            ], Agora, false, null);
        }

        private Task<Result<PainelListaDto>> Listar(string? aba = null, string? fatia = null, string? projeto = null, string? apps = null, PaginacaoDto? pag = null)
            => new ListarFatiasHandler(_apps, _painel, _usuario).Handle(new ListarFatiasQuery(pag ?? new PaginacaoDto { TamanhoPagina = 100 }, aba, fatia, projeto, apps), Ct);

        private static IEnumerable<string> Codigos(Result<PainelListaDto> r, string aplicacao = "Alfa")
            => r.Value.Itens.Where(i => i.Aplicacao == aplicacao).Select(i => i.Codigo);

        [Fact]
        public async Task Aplicacao_sem_workspace_e_listada_com_aviso_e_nenhuma_fatia()
        {
            var r = await Listar(aba: "todas");

            r.IsSuccess.Should().BeTrue();
            r.Value.Itens.Should().NotContain(i => i.Aplicacao == "Sem");
            r.Value.Avisos.Should().ContainSingle(a => a.AplicacaoId == SemWs.Id).Which.Codigo.Should().Be("painel.sem-workspace");
            r.Value.Aplicacoes.Should().Contain(a => a.Id == SemWs.Id && a.GeradoEm == null);
        }

        [Fact]
        public async Task Aplicacao_com_gitlab_fora_vira_aviso_e_nao_derruba_as_outras()
        {
            var r = await Listar(aba: "todas");

            r.Value.Avisos.Should().ContainSingle(a => a.AplicacaoId == Beta.Id).Which.Codigo.Should().Be("gitlab.indisponivel");
            Codigos(r).Should().NotBeEmpty();
        }

        [Fact]
        public async Task Aba_andamento_e_o_padrao_e_traz_com_spec_em_execucao_e_em_revisao()
        {
            var r = await Listar();

            Codigos(r).Should().BeEquivalentTo(["A2", "A4", "A10"]);
        }

        [Fact]
        public async Task Aba_prontas_traz_so_prontas_e_todas_traz_tudo()
        {
            Codigos(await Listar(aba: "prontas")).Should().Equal("A1");
            Codigos(await Listar(aba: "todas")).Should().BeEquivalentTo(["A1", "A2", "A3", "A4", "A10"]);
        }

        [Fact]
        public async Task Aba_desconhecida_e_400_com_codigo_estavel()
        {
            var r = await Listar(aba: "lixo");

            r.IsSuccess.Should().BeFalse();
            r.Errors[0].Code.Should().Be("painel.aba-invalida");
            r.Errors[0].Type.Should().Be(TipoErro.Validation);
        }

        [Theory]
        [InlineData("a2", "A2")]
        [InlineData("bonito", "A2")]
        [InlineData("BONITO", "A2")]
        public async Task Filtro_fatia_casa_codigo_ou_texto_sem_diferenciar_caixa(string busca, string esperado)
            => Codigos(await Listar(aba: "todas", fatia: busca)).Should().Equal(esperado);

        [Fact]
        public async Task Filtro_projeto_filtra_pelos_repositorios_da_fatia()
        {
            Codigos(await Listar(aba: "todas", projeto: "frontend")).Should().BeEquivalentTo(["A2", "A3", "A4"]);
            Codigos(await Listar(aba: "todas", projeto: "api")).Should().BeEquivalentTo(["A1", "A2", "A10"]);
        }

        [Fact]
        public async Task Projeto_desconhecido_e_400()
            => (await Listar(projeto: "banco")).Errors[0].Code.Should().Be("painel.projeto-invalido");

        [Fact]
        public async Task Filtro_aplicacoes_le_so_as_pedidas_e_ignora_id_inacessivel_ou_malformado()
        {
            var r = await Listar(aba: "todas", apps: $"{Alfa.Id},{Guid.NewGuid()},nao-e-guid");

            r.IsSuccess.Should().BeTrue();
            r.Value.Itens.Select(i => i.Aplicacao).Distinct().Should().Equal("Alfa");
            await _painel.Received(1).ObterAsync(
                Arg.Is<IReadOnlyList<AplicacaoDoPainel>>(l => l.Count == 1 && l[0].Chave == Alfa.Id.ToString()), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Filtro_aplicacoes_so_com_ids_inacessiveis_nao_cai_em_todas()
        {
            var r = await Listar(aba: "todas", apps: Guid.NewGuid().ToString());

            r.Value.Itens.Should().BeEmpty();
            r.Value.Aplicacoes.Should().BeEmpty();
        }

        [Fact]
        public async Task Sem_filtro_lista_so_o_que_a_pessoa_pode_ver_e_repassa_sub_e_mor()
        {
            _usuario.EhMor.Returns(true);
            _apps.ListarVisiveisAsync("sub-1", true, Arg.Any<CancellationToken>()).Returns([Alfa]);

            var r = await Listar(aba: "todas");

            r.Value.Itens.Select(i => i.Aplicacao).Distinct().Should().Equal("Alfa");
        }

        [Fact]
        public async Task Pagina_em_memoria_com_total_e_ordem_estavel_do_mapa()
        {
            var r = await Listar(aba: "todas", apps: Alfa.Id.ToString(), pag: new PaginacaoDto { Pagina = 2, TamanhoPagina = 2 });

            r.Value.Total.Should().Be(5);
            r.Value.TotalPaginas.Should().Be(3);
            r.Value.Pagina.Should().Be(2);
            r.Value.Itens.Select(i => i.Codigo).Should().Equal("A3", "A4");
        }

        [Fact]
        public async Task Ordena_por_codigo_de_forma_natural_e_no_sentido_pedido()
        {
            var r = await Listar(aba: "todas", apps: Alfa.Id.ToString(), pag: new PaginacaoDto { Ordenacao = "codigo", Sentido = "desc", TamanhoPagina = 100 });

            r.Value.Itens.Select(i => i.Codigo).Should().Equal("A10", "A4", "A3", "A2", "A1");
        }

        [Fact]
        public async Task Ordenacao_desconhecida_e_400()
            => (await Listar(pag: new PaginacaoDto { Ordenacao = "senha" })).Errors[0].Code.Should().Be("paginacao.ordenacao-invalida");

        [Fact]
        public async Task Linha_traz_estado_em_texto_estavel_progresso_e_repositorios()
        {
            var r = await Listar(aba: "todas", apps: Alfa.Id.ToString());

            var a2 = r.Value.Itens.Single(i => i.Codigo == "A2");
            a2.Estado.Should().Be("em-revisao");
            a2.Repositorios.Should().Equal("api", "frontend");
            (a2.AplicacaoId, a2.Sigla).Should().Be((Alfa.Id, "ALF"));
            r.Value.Itens.Single(i => i.Codigo == "A1").Estado.Should().Be("pronta-para-entrega");
            r.Value.Itens.Single(i => i.Codigo == "A3").Estado.Should().Be("planejada");
        }

        [Fact]
        public async Task GeradoEm_e_o_mais_antigo_e_desatualizado_vale_se_alguma_copia_e_velha()
        {
            var velho = Agora.AddMinutes(-30);
            _painel.ObterAsync(Arg.Any<IReadOnlyList<AplicacaoDoPainel>>(), Arg.Any<CancellationToken>()).Returns(ci =>
                (IReadOnlyList<ResultadoDoPainel>)ci.Arg<IReadOnlyList<AplicacaoDoPainel>>().Select(e =>
                    e.Chave == Alfa.Id.ToString()
                        ? new ResultadoDoPainel(e.Chave, SituacaoDoPainel.Ok, [], Agora, false, null)
                        : new ResultadoDoPainel(e.Chave, SituacaoDoPainel.Ok, [], velho, true, null)).ToList());

            var r = await Listar(aba: "todas", apps: $"{Alfa.Id},{Beta.Id}");

            r.Value.GeradoEm.Should().Be(velho);
            r.Value.Desatualizado.Should().BeTrue();
            r.Value.Aplicacoes.Single(a => a.Id == Beta.Id).Desatualizado.Should().BeTrue();
        }

        // ---- Atualizar ----

        [Fact]
        public async Task Atualizar_sem_papel_e_404_e_nao_toca_o_guardado()
        {
            var r = await new AtualizarFatiasHandler(_apps, _painel, _usuario).Handle(new AtualizarFatiasCommand(Guid.NewGuid()), Ct);

            r.Errors[0].Code.Should().Be("aplicacao.nao-encontrada");
            r.Errors[0].Type.Should().Be(TipoErro.NotFound);
            await _painel.DidNotReceiveWithAnyArgs().AtualizarAsync(default!, default);
        }

        [Fact]
        public async Task Atualizar_com_papel_descarta_o_guardado_da_aplicacao()
        {
            _painel.AtualizarAsync(Arg.Any<AplicacaoDoPainel>(), Arg.Any<CancellationToken>()).Returns(Result.Sucesso());

            var r = await new AtualizarFatiasHandler(_apps, _painel, _usuario).Handle(new AtualizarFatiasCommand(Alfa.Id), Ct);

            r.IsSuccess.Should().BeTrue();
            await _painel.Received(1).AtualizarAsync(Arg.Is<AplicacaoDoPainel>(a => a.Chave == Alfa.Id.ToString() && a.WorkspaceId == 10), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Atualizar_repassa_o_429_do_limite()
        {
            _painel.AtualizarAsync(Arg.Any<AplicacaoDoPainel>(), Arg.Any<CancellationToken>())
                .Returns(Result.Falha(new Erro("painel.atualizacao-recente", "Aguarde.", TipoErro.TooManyRequests)));

            var r = await new AtualizarFatiasHandler(_apps, _painel, _usuario).Handle(new AtualizarFatiasCommand(Alfa.Id), Ct);

            r.Errors[0].Code.Should().Be("painel.atualizacao-recente");
        }

        // ---- Gaveta ----

        private static DetalheDaFatia Detalhe() => new(
            "A2 — Painel bonito", "opened",
            [new FilhaDaFatia(11, 1, "migration", "closed", RepositorioFatia.Api), new FilhaDaFatia(12, 2, "tela", "opened", RepositorioFatia.Frontend)],
            [new TaskDaGaveta("A2", 1, 11, true, false, "0b4bd79", "backend (Claude)", true, RepositorioFatia.Api),
             new TaskDaGaveta("A2", 2, 12, false, true, null, null, false, RepositorioFatia.Frontend),
             new TaskDaGaveta("A2", 3, 13, false, true, null, null, false, null)],
            [new MrDaGaveta(RepositorioFatia.Api, 5, "A2 — Painel bonito", "opened", "feature/a2-painel", "success"),
             new MrDaGaveta(RepositorioFatia.Frontend, 6, "A2 — Painel bonito", "opened", "feature/a2-painel", null)]);

        private void ComDetalhe(FatiaLida fatia) => _painel.ObterAsync(Arg.Any<AplicacaoDoPainel>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoDoPainel(Alfa.Id.ToString(), SituacaoDoPainel.Ok, [fatia], Agora, false, null));

        private Task<Result<GavetaDto>> Gaveta(Guid app, string codigo)
            => new ObterGavetaHandler(_apps, _painel, _usuario, _links).Handle(new ObterGavetaQuery(app, codigo), Ct);

        [Fact]
        public async Task Gaveta_monta_links_de_spec_mae_filhas_branch_mr_e_commit_so_de_ids_e_caminhos_conhecidos()
        {
            ComDetalhe(Fatia("A2", EstadoDaFatia.EmRevisao, [RepositorioFatia.Api, RepositorioFatia.Frontend], detalhe: Detalhe()));

            var r = await Gaveta(Alfa.Id, "a2");

            r.IsSuccess.Should().BeTrue();
            var g = r.Value;
            g.Spec.Link.Should().Be("https://gitlab.exemplo.com/g/alfa/alfa-workspace/-/blob/main/specs/feature/x/A2.md");
            g.Mae!.Link.Should().Be("https://gitlab.exemplo.com/g/alfa/alfa-workspace/-/issues/7");
            g.Filhas.Select(f => f.Link).Should().Equal(
                "https://gitlab.exemplo.com/g/alfa/alfa-workspace/-/issues/11", "https://gitlab.exemplo.com/g/alfa/alfa-workspace/-/issues/12");
            g.Repositorios.Should().HaveCount(2);
            var api = g.Repositorios.Single(x => x.Repositorio == "api");
            api.Branch.Should().Be("feature/a2-painel");
            api.LinkBranch.Should().Be("https://gitlab.exemplo.com/g/alfa/alfa-api/-/tree/feature/a2-painel");
            api.Mrs.Single().Link.Should().Be("https://gitlab.exemplo.com/g/alfa/alfa-api/-/merge_requests/5");
            g.Repositorios.Single(x => x.Repositorio == "frontend").Mrs.Single().Link
                .Should().Be("https://gitlab.exemplo.com/g/alfa/alfa-frontend/-/merge_requests/6");
            g.Tasks[0].LinkCommit.Should().Be("https://gitlab.exemplo.com/g/alfa/alfa-api/-/commit/0b4bd79");
            g.Tasks[0].Executor.Should().Be("backend (Claude)");
        }

        [Fact]
        public async Task Gaveta_task_sem_sha_aponta_para_a_branch_do_repositorio_da_task()
        {
            ComDetalhe(Fatia("A2", EstadoDaFatia.EmRevisao, [RepositorioFatia.Api, RepositorioFatia.Frontend], detalhe: Detalhe()));

            var g = (await Gaveta(Alfa.Id, "A2")).Value;

            g.Tasks[1].Sha.Should().BeNull();
            g.Tasks[1].LinkCommit.Should().Be("https://gitlab.exemplo.com/g/alfa/alfa-frontend/-/tree/feature/a2-painel");
            // Sem repositório conhecido para a task, não se chuta o repositório: sem link.
            g.Tasks[2].LinkCommit.Should().BeNull();
        }

        [Fact]
        public async Task Gaveta_com_branch_estranha_nao_gera_link_e_o_texto_do_gitlab_segue_como_dado_cru()
        {
            var d = Detalhe() with
            {
                Mrs = [new MrDaGaveta(RepositorioFatia.Api, 5, "<img src=x onerror=alert(1)> A2", "opened", "feature/<script>", null)],
                Tasks = [new TaskDaGaveta("A2", 1, 11, true, false, "0b4bd79", null, false, RepositorioFatia.Api)],
            };
            ComDetalhe(Fatia("A2", EstadoDaFatia.EmRevisao, detalhe: d));

            var g = (await Gaveta(Alfa.Id, "A2")).Value;

            g.Repositorios.Single().LinkBranch.Should().BeNull();
            g.Repositorios.Single().Mrs.Single().Titulo.Should().Be("<img src=x onerror=alert(1)> A2");
        }

        [Fact]
        public async Task Gaveta_de_fatia_sem_detalhe_ou_sem_spec_nao_quebra()
        {
            ComDetalhe(Fatia("A2", EstadoDaFatia.Planejada) with { Detalhe = null, CaminhoSpec = null, SpecDisponivel = false, IssueMae = null });

            var g = (await Gaveta(Alfa.Id, "A2")).Value;

            g.Spec.Link.Should().BeNull();
            g.Mae.Should().BeNull();
            g.Filhas.Should().BeEmpty();
            g.Tasks.Should().BeEmpty();
        }

        [Fact]
        public async Task Gaveta_sem_papel_e_404()
        {
            var r = await Gaveta(Guid.NewGuid(), "A2");

            r.Errors[0].Code.Should().Be("aplicacao.nao-encontrada");
            await _painel.DidNotReceiveWithAnyArgs().ObterAsync(default(AplicacaoDoPainel)!, default);
        }

        [Fact]
        public async Task Gaveta_de_codigo_inexistente_ou_aplicacao_sem_workspace_e_404()
        {
            ComDetalhe(Fatia("A2", EstadoDaFatia.EmRevisao));
            (await Gaveta(Alfa.Id, "Z9")).Errors[0].Code.Should().Be("painel.fatia-nao-encontrada");

            _painel.ObterAsync(Arg.Any<AplicacaoDoPainel>(), Arg.Any<CancellationToken>())
                .Returns(new ResultadoDoPainel(SemWs.Id.ToString(), SituacaoDoPainel.SemWorkspace, [], null, false, null));
            var r = await Gaveta(SemWs.Id, "A2");
            r.Errors[0].Code.Should().Be("painel.fatia-nao-encontrada");
            r.Errors[0].Type.Should().Be(TipoErro.NotFound);
        }

        [Fact]
        public async Task Gaveta_repassa_a_falha_do_gitlab()
        {
            _painel.ObterAsync(Arg.Any<AplicacaoDoPainel>(), Arg.Any<CancellationToken>())
                .Returns(new ResultadoDoPainel(Alfa.Id.ToString(), SituacaoDoPainel.Falha, [], null, false, new Erro("gitlab.indisponivel", "Fora.", TipoErro.Failure)));

            (await Gaveta(Alfa.Id, "A2")).Errors[0].Code.Should().Be("gitlab.indisponivel");
        }
    }
}
