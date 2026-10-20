using Esteira.Application.Interfaces.Cache;
using Esteira.Application.Interfaces.Transaction;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.Etapas.Commands.AlterarStatusEtapa
{
    public class AlterarStatusEtapaCommand: IRequest<Unit>, ICommand, IInvalidaCache
    {
        public IEnumerable<string> PrefixosCache => new[] { ChavesCache.Etapas };

        public string Sigla { get; }
        public string Status { get; }

        public AlterarStatusEtapaCommand(string sigla, string status)
        {
            Sigla = sigla;
            Status = status;
        }
    }

}
