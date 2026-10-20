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
    public enum EtapaEnum
    {
        [Description("ABER")]
        ABER,

        [Description("VERSAO")]
        VERSAO,

        [Description("GMUD")]
        GMUD,

        [Description("BD")]
        BD,

        [Description("AET")]
        AET,

        [Description("DEV")]
        DEV,

        [Description("HML")]
        HML,

        [Description("QA")]
        QA,

        [Description("AAN")]
        AAN,

        [Description("PRD")]
        PRD


    }
}
