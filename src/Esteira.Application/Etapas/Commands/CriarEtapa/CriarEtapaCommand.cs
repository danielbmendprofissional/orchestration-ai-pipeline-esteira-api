using Esteira.Application.Interfaces.Transaction;
using Esteira.Application.DTOs.Response.Common;
using Esteira.Application.Interfaces.Cache;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.Etapas.Commands.CriarEtapa
{
    public class CriarEtapaCommand : IRequest<BaseMessageResponseDto>, ICommand, IInvalidaCache
    {
        public IEnumerable<string> PrefixosCache => new[] { ChavesCache.Etapas };

        public string Sigla { get; set; }
        public string Descricao { get; set; } 
        public char Obrigatorio { get; set; }
    }
}
