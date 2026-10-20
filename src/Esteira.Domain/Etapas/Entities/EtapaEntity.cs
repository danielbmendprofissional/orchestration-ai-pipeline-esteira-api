using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Validations;
namespace Esteira.Domain.Etapas.Entities
{
    public class EtapaEntity : IAggregateRoot
    {
        private EtapaEntity() { }

        internal EtapaEntity(string sigla, string descricao, int? ordem, bool obrigatorio)
        {
            Guard.AgainstNullOrWhiteSpace(sigla, nameof(sigla));

            Sigla = sigla;
            Descricao = descricao;
            Ordem = ordem;
            Obrigatorio = obrigatorio;
            Status = StatusEnum.ATIVO;
        }

        public string Sigla { get; private set; }
        public string Descricao { get; private set; }
        public StatusEnum Status { get; private set; }
        public int? Ordem { get; private set; }
        public bool Obrigatorio { get; private set; }


        public static EtapaEntity Criar(string sigla, string descricao, int? ordem, bool obrigatorio)
            => new(sigla, descricao, ordem, obrigatorio);

        public void AtualizarStatusEtapa(StatusEnum status)
        {
            Status = status;
        }
    }
}