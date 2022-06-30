using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.ApplicationInsights;
using Mdrx.PhrHubHub.PHR.PHRConnect.MessageFunction.Services;
using System.Diagnostics;
using Mdrx.PhrHubHub.PHR.PHRConnect.MessageFunction.DataModel;
using Microsoft.ApplicationInsights.DataContracts;

namespace Mdrx.Hub.PHR.PHRConnect.MessageFunction
{
    public class FetchFromServiceBus
    {
        private readonly TelemetryClient _telemetryClientMain;
        private readonly ISamlValidator _samlValidator;
        private readonly IConfiguration _configuration;
        private readonly IServiceBusOperation _serviceBusOperation;


        public FetchFromServiceBus(TelemetryClient telemetry, ISamlValidator samlValidator, IConfiguration configuration,IServiceBusOperation serviceBusOperation)
        {
            _telemetryClientMain = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
            _samlValidator = samlValidator ?? throw new ArgumentNullException(nameof(samlValidator));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(Configuration));
            _serviceBusOperation = serviceBusOperation ?? throw new ArgumentNullException(nameof(ServiceBusOperation)); ;
        }

        [FunctionName("FetchFromServiceBus")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "FetchFromServiceBus")] HttpRequest req,
            ILogger log)
        {
            var stopWatch = Stopwatch.StartNew();
            
            InputDataModel requestBody = await this._serviceBusOperation.FetchBodyJson(req, log);

            if(this._configuration.IsProdEnv)
            requestBody.TenantID = String.Empty;

            

            if (_configuration.ShieldCertificates != null && _configuration.ShieldCertificates.Count > 0)
            {
                var shieldValidationPassed = false;
                SamlValidationResult validationResult = null;
                foreach (var certificate in _configuration.ShieldCertificates)
                {
                    validationResult = _samlValidator.ValidateSamlToken(requestBody.ShieldToken, certificate, true, new TimeSpan(10, 0, 0));
                    if (validationResult.IsAllGood)
                    {
                        if(this._configuration.IsProdEnv)
                            requestBody.TenantID = validationResult.TenantID;
                        shieldValidationPassed = true;
                        break;
                    }
                }
                        
                //In case of Shiel Validation failure, revalidate the final certificate and return result;
                if(!shieldValidationPassed)
                {
                    
                //TODO: Error has to be added to error metrics.
                var ErrorCode = 0;
                    if (!validationResult.IsAudienceValid)
                    {
                        log.LogError("Audience is invalid.");
                        ErrorCode = 160;
                    }
                    if (!validationResult.IsDateRangeValid)
                    {
                        log.LogError("Token has expired.");
                        ErrorCode = 150;
                    }
                    if (!validationResult.IsParsingSuccessful)
                    {
                        log.LogError("Parsing is unsuccessfull.");
                        ErrorCode = 152;
                    }
                    if (!validationResult.IsSignatureValid)
                    {
                        log.LogError("Singature is invalid.");
                        ErrorCode = 161;
                    }
                    if (!validationResult.IsTypeOfUserValid)
                    {
                        log.LogError("Type of User is invalid");
                        ErrorCode = 162;
                    }
                    return new JsonResult(new OutputDataEnvelope()
                    {
                        ErrorCode = ErrorCode,
                        ErrorMessage = "Token validation failure.",
                    });
                }

            }
            

            //If verbose is set log all information

            if (requestBody.OperationMode == Mode.fastverbose || requestBody.OperationMode == Mode.secureverbose)
            {
                log.LogInformation("Entering AuthenticateAndFetch.");

                log.LogInformation(requestBody.TenantID);



                log.LogInformation(Environment.GetEnvironmentVariable(requestBody.TenantID));
                this._telemetryClientMain.TrackTrace($"TenantID: {requestBody.TenantID}");
            }

            var traceEvent = new EventTelemetry("Overall Trace");
            traceEvent.Context.User.Id = requestBody.TenantID;
            traceEvent.Properties.Add("Mode", requestBody.OperationMode.ToString());
            _telemetryClientMain.TrackEvent(traceEvent);

            try
            {
                if (requestBody.OperationMode == Mode.test)
                {
                    return new JsonResult(new OutputDataEnvelope()
                    {
                        MessageOperationID = traceEvent.Context.Operation.Id.ToString()
                    });
                }
                requestBody.MessageOperationID = traceEvent.Context.Operation.Id.ToString();
                var batchMessages = await this._serviceBusOperation.FetchFromQueue(requestBody, log, _telemetryClientMain, traceEvent, stopWatch);
                stopWatch.Stop();
                traceEvent.Properties.Add("EndOfFunction", stopWatch.ElapsedMilliseconds.ToString());
                return new JsonResult(batchMessages);
            }
            catch (ArgumentException ex)
            {

                return new JsonResult(new OutputDataEnvelope()
                {
                    ErrorCode = 125,
                    ErrorMessage = "Tenant is not configured to receive message.",
                });
            }
        }
    }
}
