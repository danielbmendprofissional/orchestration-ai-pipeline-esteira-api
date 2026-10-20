using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Domain.FluxosDeploy.ValueObjects
{
    public record TarefaValueObject
    (
        int Id,
        string Status
    )
    {
        public const string StatusAprovado = "Aprovado";

        public bool EstaAprovada => Status == StatusAprovado;
    }

}
