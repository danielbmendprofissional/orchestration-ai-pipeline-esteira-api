using Esteira.Application.Common.Json;
using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Esteira.Application.DTOs.Request
{


    public class SemgrepDtoRequest: ArtefatoAbstractDtoRequest
    {
        public string Tipo { get; private set; } = "semgrep";
        public string Version { get; set; }
        public List<ResultSemgrepDto> Results { get; set; }
        public List<ErrorSemgrepDto> Errors { get; set; }
        public PathsSemgrepDto Paths { get; set; }
        public TimeSemgrepDto Time { get; set; }
        public string EngineRequested { get; set; }
        public List<object> SkippedRules { get; set; }
    }

    public class EndSemgrepDto
    {        
        public int Line { get; set; }
        public int Col { get; set; }
        public int Offset { get; set; }
    }

    public class ErrorSemgrepDto
    {   
        public int Code { get; set; }
        public string Level { get; set; }
        public object Type { get; set; }
        public string RuleId { get; set; }
        public string Message { get; set; }
        public string Path { get; set; }
        public List<SpanSemgrepDto> Spans { get; set; }
    }

    public class ExtraSemgrepDto
    {        
        public string Message { get; set; }
        public MetadataSemgrepDto Metadata { get; set; }
        public string Severity { get; set; }
        public string Fingerprint { get; set; }
        public string Lines { get; set; }
        public string ValidationState { get; set; }
        public string EngineKind { get; set; }
    }

    public class FixpointTimeoutSemgrepDto
    {        
        public string ErrorType { get; set; }
        public string Severity { get; set; }
        public string Message { get; set; }
        public LocationSemgrepDto Location { get; set; }
    }

    public class LocationSemgrepDto
    {
        public string Path { get; set; }
        public StartSemgrepDto Start { get; set; }
        public EndSemgrepDto End { get; set; }
    }

    public class MatchingTimeSemgrepDto
    {     
        public double TotalTime { get; set; }
        public PerFileAndRuleTimeSemgrepDto PerFileAndRuleTime { get; set; }
        public VerySlowStatsDto VerySlowStats { get; set; }
        public List<object> VerySlowRulesOnFiles { get; set; }
    }

    public class MetadataSemgrepDto
    {  
        public List<string> Owasp { get; set; }        
        [Newtonsoft.Json.JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string> Cwe { get; set; }
        public List<string> References { get; set; }
        public string SourceRuleUrl { get; set; }
        public string Category { get; set; }
        public List<string> Technology { get; set; }
        public List<string> Subcategory { get; set; }
        public string Likelihood { get; set; }
        public string Impact { get; set; }
        public string Confidence { get; set; }
        public string License { get; set; }
        [JsonProperty("vulnerability_class")]
        public List<string> VulnerabilityClass { get; set; }
        public string Source { get; set; }
        public string Shortlink { get; set; }
        public bool? Cwe2022Top25 { get; set; }
        public bool? Cwe2021Top25 { get; set; }
    }

    public class ParsingTimeSemgrepDto
    {        
        public double TotalTime { get; set; }
        public PerFileTimeSemgrepDto PerFileTime { get; set; }
        public VerySlowStatsDto VerySlowStats { get; set; }
        public List<object> VerySlowFiles { get; set; }
    }

    public class PathsSemgrepDto
    {   
        public List<string> Scanned { get; set; }
    }

    public class PerDefAndRuleTimeSemgrepDto
    {   
        public double Mean { get; set; }
        public double StdDev { get; set; }
    }

    public class PerFileAndRuleTimeSemgrepDto
    {        
        public double Mean { get; set; }
        public double StdDev { get; set; }
    }

    public class PerFileTimeSemgrepDto
    {        
        public double Mean { get; set; }
        public double StdDev { get; set; }
    }

    public class PrefilteringSemgrepDto
    {   
        public double ProjectLevelTime { get; set; }
        public double FileLevelTime { get; set; }
        public double RulesWithProjectPrefiltersRatio { get; set; }
        public double RulesWithFilePrefiltersRatio { get; set; }
        public double RulesSelectedRatio { get; set; }
        public double RulesMatchedRatio { get; set; }
    }

    public class ProfilingTimesSemgrepDto
    {        
        public double ConfigTime { get; set; }
        public double CoreTime { get; set; }
        public double IgnoresTime { get; set; }
        public double TotalTime { get; set; }
    }

    public class ResultSemgrepDto
    {        
        public string CheckId { get; set; }
        public string Path { get; set; }
        public StartSemgrepDto Start { get; set; }
        public EndSemgrepDto End { get; set; }
        public ExtraSemgrepDto Extra { get; set; }
    }

    public class ScanningTimeSemgrepDto
    {        
        public double TotalTime { get; set; }
        public PerFileTimeSemgrepDto PerFileTime { get; set; }
        public VerySlowStatsDto VerySlowStats { get; set; }
        public List<VerySlowFileSemgrepDto> VerySlowFiles { get; set; }
    }

    public class SpanSemgrepDto
    {        
        public string File { get; set; }
        public StartSemgrepDto Start { get; set; }
        public EndSemgrepDto End { get; set; }
    }

    public class StartSemgrepDto
    {        
        public int Line { get; set; }
        public int Col { get; set; }
        public int Offset { get; set; }
    }

    public class TaintingTimeSemgrepDto
    {     
        public double TotalTime { get; set; }
        public PerDefAndRuleTimeSemgrepDto PerDefAndRuleTime { get; set; }
        public VerySlowStatsDto VerySlowStats { get; set; }
        public List<object> VerySlowRulesOnDefs { get; set; }
    }

    public class TimeSemgrepDto
    {        
        public List<object> Rules { get; set; }
        public double RulesParseTime { get; set; }
        public ProfilingTimesSemgrepDto ProfilingTimes { get; set; }
        public ParsingTimeSemgrepDto ParsingTime { get; set; }
        public ScanningTimeSemgrepDto ScanningTime { get; set; }
        public MatchingTimeSemgrepDto MatchingTime { get; set; }
        public TaintingTimeSemgrepDto TaintingTime { get; set; }
        public List<FixpointTimeoutSemgrepDto> FixpointTimeouts { get; set; }
        public PrefilteringSemgrepDto Prefiltering { get; set; }
        public List<object> Targets { get; set; }
        public int TotalBytes { get; set; }
        public int MaxMemoryBytes { get; set; }
    }

    public class VerySlowFileSemgrepDto
    {     
        public string Fpath { get; set; }
        public double Ftime { get; set; }
    }

    public class VerySlowStatsDto
    {        
        public double TimeRatio { get; set; }
        public double CountRatio { get; set; }
    }
   
}
