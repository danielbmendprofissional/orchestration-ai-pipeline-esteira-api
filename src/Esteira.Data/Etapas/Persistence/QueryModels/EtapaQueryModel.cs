using Esteira.Domain.Common.Enums;

namespace Esteira.Data.Etapas.Persistence.QueryModels
{
    public sealed class EtapaQueryModel
    {
        public string Sigla { get; private set; }
        public string Descricao { get; private set; }
        public StatusEnum Status { get; private set; }
        public int? Ordem { get; private set; }
        public bool Obrigatorio { get; private set; }
    }
}
