using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Domain.Common.Resultados;

namespace Esteira.Application.Fatias
{
    // Chaves de aplicação (não de infra): `Painel:ValidadeSegundos` (padrão 60). O resto é ajuste fino de espera.
    public sealed class PainelOptions
    {
        public const string Secao = "Painel";

        public int ValidadeSegundos { get; set; } = 60;

        // Quem chega com o guardado vencido e outra leitura em andamento consulta o guardado a cada intervalo.
        public int IntervaloDeEsperaMs { get; set; } = 100;

        public int EsperaMaximaSegundos { get; set; } = 30;
    }

    // Guardado do Painel (Redis em produção). Chaves lógicas; o adaptador põe o `Redis:KeyPrefix`. Só comandos simples.
    public interface IPainelCache
    {
        Task<string?> ObterAsync(string chave, CancellationToken cancellationToken = default);

        Task GuardarAsync(string chave, string valor, TimeSpan validade, CancellationToken cancellationToken = default);

        // `SET NX`: true se esta chamada guardou (a chave não existia).
        Task<bool> TentarGuardarSeAusenteAsync(string chave, string valor, TimeSpan validade, CancellationToken cancellationToken = default);

        Task RemoverAsync(string chave, CancellationToken cancellationToken = default);

        // Só apaga se o valor atual for `valor` (liberar trava sem apagar a de outro).
        Task RemoverSeValorAsync(string chave, string valor, CancellationToken cancellationToken = default);
    }

    // `Chave` é o identificador da Aplicação (PublicId) e liga entrada e resultado e compõe as chaves do guardado.
    // `Projetos`: Projeto de código da Aplicação (Api/Frontend) -> id no GitLab. `WorkspaceId` nulo = Aplicação sem workspace.
    public sealed record AplicacaoDoPainel(
        string Chave, string GrupoCaminho, long? WorkspaceId, IReadOnlyDictionary<RepositorioFatia, long> Projetos);

    public enum SituacaoDoPainel { Ok, SemWorkspace, Falha }

    // Ok: `Fatias` e `GeradoEm` (`Desatualizado` = cópia servida porque o GitLab está fora). SemWorkspace: aviso, nada lido.
    // Falha: `Erro` (`gitlab.indisponivel`, `gitlab.bot-nao-configurado`...).
    public sealed record ResultadoDoPainel(
        string Chave, SituacaoDoPainel Situacao, IReadOnlyList<FatiaLida> Fatias, DateTimeOffset? GeradoEm, bool Desatualizado, Erro? Erro);

    public interface IPainelDeFatias
    {
        Task<ResultadoDoPainel> ObterAsync(AplicacaoDoPainel aplicacao, CancellationToken cancellationToken = default);

        // Um resultado por Aplicação, na ordem; a falha de uma não derruba as outras.
        Task<IReadOnlyList<ResultadoDoPainel>> ObterAsync(IReadOnlyList<AplicacaoDoPainel> aplicacoes, CancellationToken cancellationToken = default);

        // Descarta o guardado. No máximo uma por Aplicação a cada 10 s: `painel.atualizacao-recente` (TipoErro.TooManyRequests, 429).
        Task<Result> AtualizarAsync(AplicacaoDoPainel aplicacao, CancellationToken cancellationToken = default);
    }

    public sealed class PainelDeFatias(IGitLab gitlab, IPainelCache cache, TimeProvider relogio, PainelOptions opcoes) : IPainelDeFatias
    {
        private const string BranchDoWorkspace = "main";
        private const string CaminhoDoMapa = "specs/README.md";
        private const string LabelEmExecucao = "em-execucao";
        private const int LeiturasDeSpecEmParalelo = 8;
        private const int CabecalhoMaximo = 4000;
        private static readonly TimeSpan CopiaDuracao = TimeSpan.FromHours(24);
        private static readonly TimeSpan SpecDuracao = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan TravaDuracao = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan LimiteDeAtualizacao = TimeSpan.FromSeconds(10);

        private static readonly Erro AtualizacaoRecente = new(
            "painel.atualizacao-recente", "A lista foi atualizada há instantes. Aguarde alguns segundos.", TipoErro.TooManyRequests);

        private static readonly Erro PainelIndisponivel = new(
            "painel.indisponivel", "Não foi possível montar a lista de Fatias agora. Tente novamente.", TipoErro.Failure);

        private sealed record Instantaneo(DateTimeOffset GeradoEm, List<FatiaLida> Fatias);

        private static string ChaveFresco(string app) => $"painel:{app}:fresco";
        private static string ChaveCopia(string app) => $"painel:{app}:copia";
        private static string ChaveTrava(string app) => $"painel:{app}:trava";
        private static string ChaveAtualizacao(string app) => $"painel:{app}:atualizacao";

        private static string ChaveSpec(long workspace, string caminho)
            => $"painel:spec:{workspace}:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(caminho)))[..24]}";

        public async Task<IReadOnlyList<ResultadoDoPainel>> ObterAsync(
            IReadOnlyList<AplicacaoDoPainel> aplicacoes, CancellationToken cancellationToken = default)
        {
            var tarefas = aplicacoes.Select(async app =>
            {
                try
                {
                    return await ObterAsync(app, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    return Falha(app.Chave, PainelIndisponivel);
                }
            });

            return await Task.WhenAll(tarefas);
        }

        public async Task<ResultadoDoPainel> ObterAsync(AplicacaoDoPainel aplicacao, CancellationToken cancellationToken = default)
        {
            if (aplicacao.WorkspaceId is null)
                return new ResultadoDoPainel(aplicacao.Chave, SituacaoDoPainel.SemWorkspace, [], null, false, null);

            var guardado = await LerInstantaneoAsync(ChaveFresco(aplicacao.Chave), cancellationToken);
            if (guardado is not null)
                return Ok(aplicacao, guardado, false);

            var dono = Guid.NewGuid().ToString("N");
            var tentativas = Math.Max(1, opcoes.EsperaMaximaSegundos * 1000 / Math.Max(1, opcoes.IntervaloDeEsperaMs));

            for (var i = 0; i < tentativas; i++)
            {
                if (await cache.TentarGuardarSeAusenteAsync(ChaveTrava(aplicacao.Chave), dono, TravaDuracao, cancellationToken))
                {
                    try
                    {
                        // Quem segurava a trava pode ter acabado de guardar.
                        guardado = await LerInstantaneoAsync(ChaveFresco(aplicacao.Chave), cancellationToken);
                        return guardado is not null ? Ok(aplicacao, guardado, false) : await LerEGuardarAsync(aplicacao, cancellationToken);
                    }
                    finally
                    {
                        await cache.RemoverSeValorAsync(ChaveTrava(aplicacao.Chave), dono, CancellationToken.None);
                    }
                }

                await Task.Delay(opcoes.IntervaloDeEsperaMs, cancellationToken);
                guardado = await LerInstantaneoAsync(ChaveFresco(aplicacao.Chave), cancellationToken);
                if (guardado is not null)
                    return Ok(aplicacao, guardado, false);
            }

            return await CopiaOuFalhaAsync(aplicacao, PainelIndisponivel, cancellationToken);
        }

        public async Task<Result> AtualizarAsync(AplicacaoDoPainel aplicacao, CancellationToken cancellationToken = default)
        {
            if (!await cache.TentarGuardarSeAusenteAsync(ChaveAtualizacao(aplicacao.Chave), "1", LimiteDeAtualizacao, cancellationToken))
                return Result.Falha(AtualizacaoRecente);

            await cache.RemoverAsync(ChaveFresco(aplicacao.Chave), cancellationToken);
            return Result.Sucesso();
        }

        private static ResultadoDoPainel Falha(string chave, Erro erro)
            => new(chave, SituacaoDoPainel.Falha, [], null, false, erro);

        private static ResultadoDoPainel Ok(AplicacaoDoPainel app, Instantaneo i, bool desatualizado)
            => new(app.Chave, SituacaoDoPainel.Ok, i.Fatias, i.GeradoEm, desatualizado, null);

        private async Task<Instantaneo?> LerInstantaneoAsync(string chave, CancellationToken ct)
        {
            var json = await cache.ObterAsync(chave, ct);
            if (string.IsNullOrEmpty(json))
                return null;

            try
            {
                return JsonSerializer.Deserialize<Instantaneo>(json);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private async Task<ResultadoDoPainel> LerEGuardarAsync(AplicacaoDoPainel app, CancellationToken ct)
        {
            var lido = await LerDoGitLabAsync(app, ct);
            if (!lido.IsSuccess)
                return await CopiaOuFalhaAsync(app, lido.Errors[0], ct);

            var instantaneo = new Instantaneo(relogio.GetUtcNow(), lido.Value.ToList());
            var json = JsonSerializer.Serialize(instantaneo);
            await cache.GuardarAsync(ChaveCopia(app.Chave), json, CopiaDuracao, ct);
            await cache.GuardarAsync(ChaveFresco(app.Chave), json, TimeSpan.FromSeconds(Math.Max(1, opcoes.ValidadeSegundos)), ct);
            return Ok(app, instantaneo, false);
        }

        // Só `gitlab.indisponivel` serve a cópia; sem token para o grupo o aviso é o erro (configuração, não oscilação).
        private async Task<ResultadoDoPainel> CopiaOuFalhaAsync(AplicacaoDoPainel app, Erro erro, CancellationToken ct)
        {
            if (erro.Code is "gitlab.indisponivel" or "painel.indisponivel")
            {
                var copia = await LerInstantaneoAsync(ChaveCopia(app.Chave), ct);
                if (copia is not null)
                    return Ok(app, copia, true);
            }

            return Falha(app.Chave, erro);
        }

        private async Task<Result<IReadOnlyList<FatiaLida>>> LerDoGitLabAsync(AplicacaoDoPainel app, CancellationToken ct)
        {
            var grupo = app.GrupoCaminho;
            var workspace = app.WorkspaceId!.Value;

            var mapa = await gitlab.ObterArquivoAsync(grupo, workspace, CaminhoDoMapa, BranchDoWorkspace, ct);
            if (!mapa.IsSuccess)
                return Result<IReadOnlyList<FatiaLida>>.Falha(mapa.Errors[0]);

            var issuesTarefa = gitlab.ListarIssuesAsync(grupo, workspace, ct);
            var repositorios = app.Projetos.ToList();
            var mrsTarefas = repositorios.Select(p => gitlab.ListarMergeRequestsAsync(grupo, p.Value, ct)).ToList();
            var specsTarefa = LerSpecsAsync(app, mapa.Value, ct);

            await Task.WhenAll(new Task[] { issuesTarefa, specsTarefa }.Concat(mrsTarefas));

            var issues = await issuesTarefa;
            if (!issues.IsSuccess)
                return Result<IReadOnlyList<FatiaLida>>.Falha(issues.Errors[0]);

            var mrs = new Dictionary<RepositorioFatia, IReadOnlyList<MergeRequestGitLabDto>>();
            for (var i = 0; i < repositorios.Count; i++)
            {
                var r = await mrsTarefas[i];
                if (!r.IsSuccess)
                    return Result<IReadOnlyList<FatiaLida>>.Falha(r.Errors[0]);
                mrs[repositorios[i].Key] = r.Value;
            }

            var specs = await specsTarefa;
            if (!specs.IsSuccess)
                return Result<IReadOnlyList<FatiaLida>>.Falha(specs.Errors[0]);

            var maes = issues.Value
                .Where(i => i.Descricao is not null && i.Labels.Contains(LabelEmExecucao))
                .ToDictionary(i => i.Iid, i => i.Descricao!);

            return Result<IReadOnlyList<FatiaLida>>.Sucesso(MontadorDeFatias.Montar(mapa.Value, issues.Value, mrs, specs.Value, maes));
        }

        // Cabeçalho de cada spec linkada no mapa: até 8 leituras em paralelo, 10 min no guardado. Spec que não existe fica de fora.
        private async Task<Result<IReadOnlyDictionary<string, string>>> LerSpecsAsync(AplicacaoDoPainel app, string? mapa, CancellationToken ct)
        {
            var caminhos = LeitorDoMapa.Ler(mapa)
                .Select(l => l.CaminhoSpec).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).Distinct().ToList();
            using var porta = new SemaphoreSlim(LeiturasDeSpecEmParalelo);

            async Task<(string Caminho, Result<string?> Texto)> LerUma(string caminho)
            {
                await porta.WaitAsync(ct);
                try
                {
                    var chave = ChaveSpec(app.WorkspaceId!.Value, caminho);
                    var guardado = await cache.ObterAsync(chave, ct);
                    if (guardado is not null)
                        return (caminho, Result<string?>.Sucesso(guardado));

                    var lido = await gitlab.ObterArquivoAsync(app.GrupoCaminho, app.WorkspaceId!.Value, caminho, BranchDoWorkspace, ct);
                    if (lido.IsSuccess && lido.Value is not null)
                    {
                        var cabecalho = lido.Value.Length > CabecalhoMaximo ? lido.Value[..CabecalhoMaximo] : lido.Value;
                        await cache.GuardarAsync(chave, cabecalho, SpecDuracao, ct);
                        return (caminho, Result<string?>.Sucesso(cabecalho));
                    }

                    return (caminho, lido);
                }
                finally
                {
                    porta.Release();
                }
            }

            var lidos = await Task.WhenAll(caminhos.Select(LerUma));
            var falha = lidos.FirstOrDefault(l => !l.Texto.IsSuccess);
            if (falha.Caminho is not null)
                return Result<IReadOnlyDictionary<string, string>>.Falha(falha.Texto.Errors[0]);

            return Result<IReadOnlyDictionary<string, string>>.Sucesso(
                lidos.Where(l => l.Texto.Value is not null).ToDictionary(l => l.Caminho, l => l.Texto.Value!));
        }
    }
}
