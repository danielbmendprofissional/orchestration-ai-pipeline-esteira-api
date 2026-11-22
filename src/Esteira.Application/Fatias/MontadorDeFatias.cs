using Esteira.Application.Interfaces.Integracoes;

namespace Esteira.Application.Fatias
{
    // Junta o que já foi lido (mapa, issues, MRs por repositório, texto das specs por caminho, descrição das mães por
    // `Iid`) numa Fatia por linha do mapa, na ordem do mapa. Sem I/O; uma linha com problema não derruba a lista.
    public static class MontadorDeFatias
    {
        public static IReadOnlyList<FatiaLida> Montar(
            string? mapa,
            IReadOnlyList<IssueGitLabDto> issues,
            IReadOnlyDictionary<RepositorioFatia, IReadOnlyList<MergeRequestGitLabDto>> mrsPorRepositorio,
            IReadOnlyDictionary<string, string> specsPorCaminho,
            IReadOnlyDictionary<long, string> descricoesDasMaes)
        {
            var fatias = new List<FatiaLida>();
            foreach (var linha in LeitorDoMapa.Ler(mapa))
            {
                try
                {
                    fatias.Add(MontarUma(linha, issues ?? [], mrsPorRepositorio, specsPorCaminho, descricoesDasMaes));
                }
                catch (Exception)
                {
                    // Nunca derruba a lista: a linha sai só com o que o mapa disse.
                    fatias.Add(new FatiaLida(linha.Codigo, linha.Titulo, linha.Status, linha.StatusReconhecido, linha.Repositorios,
                        null, false, linha.CaminhoSpec, null,
                        new ResultadoDeEstado(linha.Feito ? EstadoDaFatia.ProntaParaEntrega : EstadoDaFatia.Planejada, null, false, true), true));
                }
            }
            return fatias;
        }

        private static FatiaLida MontarUma(
            LinhaDoMapa linha,
            IReadOnlyList<IssueGitLabDto> issues,
            IReadOnlyDictionary<RepositorioFatia, IReadOnlyList<MergeRequestGitLabDto>> mrsPorRepositorio,
            IReadOnlyDictionary<string, string> specsPorCaminho,
            IReadOnlyDictionary<long, string> descricoesDasMaes)
        {
            var mae = DerivadorDeEstado.ResolverMae(linha, issues);

            BlocoDeExecucao? bloco = null;
            if (mae is not null && descricoesDasMaes is not null && descricoesDasMaes.TryGetValue(mae.Iid, out var descricao))
                bloco = LeitorDoBlocoDeExecucao.Ler(descricao);

            var mrs = new List<MrDaFatia>();
            foreach (var repo in linha.Repositorios)
            {
                if (mrsPorRepositorio is not null && mrsPorRepositorio.TryGetValue(repo, out var lista) && lista is not null)
                    mrs.AddRange(lista.Select(m => new MrDaFatia(repo, m)));
            }

            var estado = DerivadorDeEstado.Derivar(linha, mae, bloco, mrs);

            CabecalhoDaSpec? spec = null;
            if (linha.CaminhoSpec is not null && specsPorCaminho is not null && specsPorCaminho.TryGetValue(linha.CaminhoSpec, out var texto))
                spec = LeitorDeSpec.Ler(texto);

            return new FatiaLida(linha.Codigo, linha.Titulo, linha.Status, linha.StatusReconhecido, linha.Repositorios,
                spec?.Risco, spec is not null, linha.CaminhoSpec, mae?.Iid, estado,
                estado.FormatoAnterior || spec?.FormatoAnterior == true);
        }
    }
}
