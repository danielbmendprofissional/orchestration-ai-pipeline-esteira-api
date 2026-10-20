using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.DTOs.Request
{
    public class TrivyDtoRequest: ArtefatoAbstractDtoRequest
    {   
        
        public string Tipo { get; private set; } = "trivy";
        public int SchemaVersion { get; set; }
        public string CreatedAt { get; set; }
        public string ArtifactName { get; set; }
        public string ArtifactType { get; set; }
        public MetadataTrivyDto Metadata { get; set; }
        public List<ResultTrivyDto> Results { get; set; }
    }


    public class BitnamiTrivyDto
    {
        public string V3Vector { get; set; }
        public double V3Score { get; set; }
    }

    public class CVSSTrivyDto
    {
        public BitnamiTrivyDto Bitnami { get; set; }
        public GhsaTrivyDto Ghsa { get; set; }
        public NvdTrivyDto Nvd { get; set; }
        public RedhatTrivyDto Redhat { get; set; }
    }

    public class DataSourceTrivyDto
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public string URL { get; set; }
    }

    public class GhsaTrivyDto
    {
        public string V3Vector { get; set; }
        public double V3Score { get; set; }
        public string V40Vector { get; set; }
        public double? V40Score { get; set; }
    }

    public class IdentifierTrivyDto
    {
        public string PURL { get; set; }
        public string UID { get; set; }
    }

    public class MetadataTrivyDto
    {
        public string RepoURL { get; set; }
        public string Commit { get; set; }
        public string CommitMsg { get; set; }
        public string Author { get; set; }
        public string Committer { get; set; }
    }

    public class NvdTrivyDto
    {
        public string V2Vector { get; set; }
        public string V3Vector { get; set; }
        public double V2Score { get; set; }
        public double V3Score { get; set; }
    }

    public class PackageTrivyDto
    {
        public string Name { get; set; }
        public IdentifierTrivyDto Identifier { get; set; }
        public string Version { get; set; }
    }

    public class PkgIdentifierTrivyDto
    {
        public string PURL { get; set; }
        public string UID { get; set; }
    }

    public class RedhatTrivyDto
    {
        public string V3Vector { get; set; }
        public double V3Score { get; set; }
        public string V2Vector { get; set; }
        public double? V2Score { get; set; }
    }

    public class ResultTrivyDto
    {
        public string Target { get; set; }
        public string Class { get; set; }
        public string Type { get; set; }
        public List<PackageTrivyDto> Packages { get; set; }
        public List<VulnerabilityTrivyDto> Vulnerabilities { get; set; }
    }

    public class VendorSeverityTrivyDto
    {
        public int Bitnami { get; set; }
        public int Ghsa { get; set; }
        public int Nvd { get; set; }
        [JsonProperty("oracle-oval")]
        public int OracleOval { get; set; }
        public int Photon { get; set; }
        public int Redhat { get; set; }
        public int? Alma { get; set; }
        public int? Rocky { get; set; }
        public int? Amazon { get; set; }
        public int? Azure { get; set; }
        [JsonProperty("ruby-advisory-db")]
        public int? RubyAdvisoryDb { get; set; }
        [JsonProperty("nodejs-security-wg")]
        public int? NodejsSecurityWg { get; set; }
        public int? Ubuntu { get; set; }
    }

    public class VulnerabilityTrivyDto
    {
        public string VulnerabilityID { get; set; }
        public string PkgName { get; set; }
        public PkgIdentifierTrivyDto PkgIdentifier { get; set; }
        public string InstalledVersion { get; set; }
        public string FixedVersion { get; set; }
        public string Status { get; set; }
        public string SeveritySource { get; set; }
        public string PrimaryURL { get; set; }
        public DataSourceTrivyDto DataSource { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Severity { get; set; }
        public VendorSeverityTrivyDto VendorSeverity { get; set; }
        public CVSSTrivyDto CVSS { get; set; }
        public List<string> References { get; set; }
        public DateTime PublishedDate { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public List<string> CweIDs { get; set; }
    }
}
