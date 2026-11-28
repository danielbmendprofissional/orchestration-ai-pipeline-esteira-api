using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Validations;
using Esteira.Domain.Common.ValueObjects;

namespace Esteira.Domain.Analises
{
    // Contagem por severidade (info nunca entra), depois de descontar as Excecoes.
    public sealed record ContagemDeSeveridade(int Critica, int Alta, int Media, int Baixa);

    public sealed record AvaliacaoDoGate(DecisaoGate Decisao, ContagemDeSeveridade Contagem, string? Motivo);

    // Funcao pura (sem I/O): decide o gate pela Politica gravada do Ambiente (spec 03-analises-e-gate.md, "Dominio").
    public static class AvaliadorDoGate
    {
        // `excecoes` = conjunto de ChaveDoTipo cobertas por Excecao vigente (vazio na E2; a E2b o preenche).
        public static AvaliacaoDoGate Avaliar(
            IReadOnlyCollection<AnaliseEntity> analises, PoliticaSeveridadeEntity politica, IReadOnlySet<string> excecoes, MomentoDoGate momento)
        {
            Guard.AgainstNull(analises, nameof(analises));
            Guard.AgainstNull(politica, nameof(politica));
            Guard.AgainstNull(excecoes, nameof(excecoes));
            Guard.AgainstInvalidEnum<MomentoDoGate>(momento, nameof(momento));

            if (analises.Count == 0)
                return new AvaliacaoDoGate(DecisaoGate.Bloqueado, new ContagemDeSeveridade(0, 0, 0, 0), MotivosDoGate.SemAnalises);

            int critica = 0, alta = 0, media = 0, baixa = 0;
            foreach (var achado in analises.SelectMany(a => a.Achados))
            {
                if (excecoes.Contains(achado.ChaveDoTipo))
                    continue;

                switch (achado.Severidade)
                {
                    case SeveridadeAchado.Critica: critica++; break;
                    case SeveridadeAchado.Alta: alta++; break;
                    case SeveridadeAchado.Media: media++; break;
                    case SeveridadeAchado.Baixa: baixa++; break;
                }
            }
            var contagem = new ContagemDeSeveridade(critica, alta, media, baixa);

            // Fail-closed, so no gate: ferramenta que nao produziu relatorio bloqueia. Erro de agente so e registrado.
            if (momento == MomentoDoGate.Gate
                && analises.Any(a => a.Tipo == TipoAnalise.Ferramenta && a.Veredito == VereditoAnalise.Erro))
                return new AvaliacaoDoGate(DecisaoGate.Bloqueado, contagem, MotivosDoGate.AnaliseComErro);

            var limites = LimitesSeveridadeValueObject.Criar(politica.Critica, politica.Alta, politica.Media, politica.Baixa).Value;
            var resumo = ResumoSeveridadeValueObject.Criar(critica, alta, media, baixa).Value;

            if (limites.ObterExcessos(resumo).Count == 0)
                return new AvaliacaoDoGate(DecisaoGate.Liberado, contagem, null);

            if (politica.Modo == ModoPolitica.Avisa)
                return new AvaliacaoDoGate(DecisaoGate.Avisou, contagem, null);

            return new AvaliacaoDoGate(DecisaoGate.Bloqueado, contagem,
                momento == MomentoDoGate.Gate ? MotivosDoGate.Bloqueado : MotivosDoGate.ResultadoBloqueado);
        }
    }
}
