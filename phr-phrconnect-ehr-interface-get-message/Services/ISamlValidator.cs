using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Mdrx.PhrHubHub.PHR.PHRConnect.MessageFunction.Services
{
    public interface ISamlValidator
    {
        SamlValidationResult ValidateSamlToken(string base64Saml, X509Certificate2 x509Certificate, bool isServiceAccountToken, TimeSpan clockSkew);

    }

    public class SamlValidationResult
    {
        public bool IsDateRangeValid { get; set; }
        public bool IsAudienceValid { get; set; }
        public bool IsParsingSuccessful { get; set; }
        public bool IsSignatureValid { get; set; }
        public bool IsTypeOfUserValid { get; set; }
        public string Message { get; set; }
        public bool IsAllGood => IsParsingSuccessful && IsSignatureValid && IsAudienceValid && IsDateRangeValid && IsTypeOfUserValid;
        public string TenantID { get; set; }
    }
}
