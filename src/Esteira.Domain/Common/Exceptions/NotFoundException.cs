using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Domain.Common.Exceptions
{
    public class NotFoundException : Exception
    {
        public int? StatusCode { get; }
        public NotFoundException(string message, int? statusCode = 404) : base(message)
        {
            StatusCode = statusCode;
        }
    }
}
