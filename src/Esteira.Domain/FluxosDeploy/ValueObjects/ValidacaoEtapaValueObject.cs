using Esteira.Domain.ConfiguracoesSistema.Entities;
using Esteira.Domain.Deploys.Entities;
using Esteira.Domain.FluxosDeploy.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Domain.FluxosDeploy.ValueObjects
{
    #nullable enable
    public record ValidacaoEtapaValueObject(
        FluxoDeployEntity FluxoDeploy,
        IEnumerable<TarefaValueObject>? Tarefas = null,
        IEnumerable<AnaliseEntity>? Analises = null,
        ConfiguracaoSistemaEntity? Config = null
        

    );
}
