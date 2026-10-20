using Esteira.Domain.Common.Enums;

namespace Esteira.Data.AprovadoresTecnicos.Persistence.QueryModels
{
    public sealed class AprovadorTecnicoQueryModel
    {
        public int Id { get; private set; }
        public StatusEnum Status { get; private set; }
        public string Usuario { get; private set; }
        public string Equipe { get; private set; }
    }
}
