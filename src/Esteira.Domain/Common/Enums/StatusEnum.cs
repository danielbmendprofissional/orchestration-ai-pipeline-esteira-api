using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Esteira.Domain.Common.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum StatusEnum
    {
        [Description("NÃO INICIADO")]
        NAO_INICIADO,

        [Description("ATIVO")]
        ATIVO,

        [Description("INATIVO")]
        INATIVO,

        [Description("EXECUTANDO")]
        EXECUTANDO,

        [Description("CANCELADO")]
        CANCELADO,

        [Description("CONCLUÍDO")]
        CONCLUIDO,

        [Description("ERRO")]
        ERRO
    }
}
