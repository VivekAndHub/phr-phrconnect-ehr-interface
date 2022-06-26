using System.Security.Cryptography.X509Certificates;

namespace Mdrx.PhrHubHub.PHR.PHRConnect.MessageFunction.Services
{
    public interface IConfiguration
    {
        int BatchSizeLimit { get; }
        bool IsProdEnv { get; }
        int PollingInterval { get; }
        string[] ShieldAllowedURIs { get; }
        string[] ShieldThumbPrint { get; }
        X509Certificate2Collection ShieldCertificates { get; }
        int PollingTimeoutInSeconds { get; }
        int TraceLimit { get; }
        T Parse<T>(string value);
    }
}