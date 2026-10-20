using Esteira.Domain.Common.Enums;
using Esteira.Domain.Etapas.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Domain.Etapas.Interfaces
{
    public interface IEtapaService
    {   
        void ValidarStatusEtapa(EtapaEntity etapa, StatusEnum status);
        void ValidarEtapasExistentesEAtivas(IReadOnlyList<EtapaEntity> etapasEntity, List<string> etapas);
        void ValidarEtapasObrigatorias(IReadOnlyList<EtapaEntity> etapasEntity, List<string> etapas);        
    }
}
