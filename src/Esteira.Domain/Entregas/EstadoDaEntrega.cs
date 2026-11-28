namespace Esteira.Domain.Entregas
{
    // Exposto em kebab-case. Os demais estados da ADR-0023 entram com E3, E4, E6 e E7.
    public enum EstadoEntrega { BloqueadaOuFalha, AguardandoAprovacao, EmAndamento }

    public enum VezDe { AprovadorTecnico }

    // Estado da Entrega (ADR-0023): o primeiro que valer, na ordem da ADR. Calculado, nunca gravado.
    public sealed record EstadoDaEntrega(EstadoEntrega Estado, VezDe? VezDe)
    {
        public static EstadoDaEntrega Calcular(IEnumerable<EtapaFluxoEntity> etapas)
        {
            var lista = etapas.ToList();

            if (lista.Any(e => e.Estado is EstadoEtapa.Falhou or EstadoEtapa.Bloqueada))
                return new EstadoDaEntrega(EstadoEntrega.BloqueadaOuFalha, null);

            // qa e hml esperam o aprovador técnico.
            if (lista.Any(e => e.Estado == EstadoEtapa.AguardandoAprovacao))
                return new EstadoDaEntrega(EstadoEntrega.AguardandoAprovacao, Entregas.VezDe.AprovadorTecnico);

            return new EstadoDaEntrega(EstadoEntrega.EmAndamento, null);
        }
    }
}
