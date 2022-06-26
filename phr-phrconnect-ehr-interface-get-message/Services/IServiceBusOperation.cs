using Mdrx.PhrHubHub.PHR.PHRConnect.MessageFunction.DataModel;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Mdrx.PhrHubHub.PHR.PHRConnect.MessageFunction.Services
{
    public interface IServiceBusOperation
    {
        Task<InputDataModel> FetchBodyJson(HttpRequest req, ILogger log);
        Task<OutputDataEnvelope> FetchFromQueue(InputDataModel input, ILogger log, TelemetryClient telemetryClient, EventTelemetry traceEvent, Stopwatch stopwatch);
    }
}