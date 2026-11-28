namespace Esteira.Domain.Analises
{
    // Persistidos como texto em kebab-case (artefato, deploy, ferramenta, aprovado, critica...). E2.
    public enum EscopoAnalise { Artefato, Deploy }

    public enum TipoAnalise { Ferramenta, Agente }

    public enum VereditoAnalise { Aprovado, Reprovado, Erro }

    public enum SeveridadeAchado { Critica, Alta, Media, Baixa, Info }

    public enum MomentoDoGate { Gate, Resultado }

    public enum DecisaoGate { Liberado, Avisou, Bloqueado }

    // Motivos gravados na DecisaoDoGate e em EtapaFluxo.CodigoFalha (codigos estaveis da spec E2).
    public static class MotivosDoGate
    {
        public const string Bloqueado = "gate.bloqueado";
        public const string AnaliseComErro = "gate.analise-com-erro";
        public const string SemAnalises = "gate.sem-analises";
        public const string ResultadoBloqueado = "resultado.bloqueado";
    }
}
