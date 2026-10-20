using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Domain.Common.Exceptions
{
    public class IntegrationException: Exception
    {
        public int? StatusCode { get; }
        public IntegrationException(string message, int? statusCode = 500) : base(message)
        {
            StatusCode = statusCode;
        }
    }
}
