using Azure.Messaging.ServiceBus;
using Mdrx.PhrHubHub.PHR.PHRConnect.MessageFunction.DataModel;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Mdrx.PhrHubHub.PHR.PHRConnect.MessageFunction.Services
{
    public class ServiceBusOperation : IServiceBusOperation
    {

        Microsoft.Extensions.Logging.ILogger Log;
        TelemetryConfiguration appInsightConfiguration;
        TelemetryClient TelemetryClient;
        EventTelemetry TraceEvent;
        IConfiguration Configuration;

        public ServiceBusOperation(IConfiguration configuration)
        {
            
            this.Configuration = configuration;

        }

        public async Task<OutputDataEnvelope> FetchFromQueue(InputDataModel input, ILogger log, TelemetryClient telemetryClient, EventTelemetry traceEvent, Stopwatch stopwatch)
        {
            Log = log;
            TelemetryClient = telemetryClient;
            TraceEvent = traceEvent;
            List<EventTelemetry> eventList = new List<EventTelemetry>();

            log.LogInformation("Entered FetchFromQueue function.");
            var envelop = new OutputDataEnvelope();
            List<string> phrConnectIDsSent = new List<string>();
            List<string> executionContextIDs = new List<string>();
            List<string> accountIDs = new List<string>();
            List<string> deletedPHRConnectIDs = new List<string>();
            List<string> nounList = new List<string>();
            List<string> verbList = new List<string>();
            List<string> destinationIDList = new List<string>();
            envelop.MessageOperationID = input.MessageOperationID;

            //Initialize supporting variable
            var responseMessageList = new List<OutputDataModel>();
            int accumulatedBatchSize = 0;
            int messageCount = 0;

            //Add to telemetry
            //Elapsed time tracking
            traceEvent.Properties.Add("StartFetchingFromKeyVault", stopwatch.ElapsedMilliseconds.ToString());
            //Get Queue details
            var secret = Environment.GetEnvironmentVariable(await ValidateAndGenerateSecretName(input.TenantID));
            if (string.IsNullOrEmpty(secret))
            {
                var argException = new ArgumentException("Tenant configuration missing.");
                telemetryClient.TrackException(argException);
                telemetryClient.Flush();
                envelop.ErrorMessage = "Configuration Issue.";
                envelop.ErrorCode = 128;
                envelop.MessageCount = 0;
                envelop.Messages = responseMessageList;
                envelop.BatchSize = accumulatedBatchSize;
                return envelop;
            }
            var queueInfo = JsonConvert.DeserializeObject<QueueInfo>(Encoding.UTF8.GetString(Convert.FromBase64String(secret)));
            var serviceBusConnection = queueInfo.properties.ServiceBusConnection;
            var queueName = queueInfo.properties.QueueName;
            var enabled = queueInfo.properties.Enabled;
            //Elapsed time tracking
            traceEvent.Properties.Add("EndFetchingFromKeyVault", stopwatch.ElapsedMilliseconds.ToString());

            //if more verbose is required, use operation  mode as fastverbose or secureverbose
            if (input.OperationMode == Mode.fastverbose || input.OperationMode == Mode.secureverbose)
            {

                log.LogInformation(input.MessageOperationID);
                log.LogInformation(serviceBusConnection);
                log.LogInformation(queueName);
                log.LogInformation(enabled.ToString());
                TraceEvent.Properties.Add(new KeyValuePair<string, string>("ServiceBusConnection", serviceBusConnection));
                TraceEvent.Properties.Add(new KeyValuePair<string, string>("QueueName", queueName));
                TraceEvent.Properties.Add(new KeyValuePair<string, string>("Enabled", enabled.ToString()));
            }

            if (serviceBusConnection == null || queueName == null)
            {
                throw new ArgumentException("Service Bus configuration missing");

            }




            //Disabled tenant, hence skipping processing
            if (!enabled)
            {
                var argException = new ArgumentException("Tenant is not enabled.");

                telemetryClient.TrackException(argException);
                telemetryClient.Flush();
                envelop.ErrorMessage = "Tenant is disabled.";
                envelop.ErrorCode = 127;
                envelop.MessageCount = 0;
                envelop.Messages = responseMessageList;
                envelop.BatchSize = accumulatedBatchSize;
                return envelop;
            }
            ServiceBusClient AzureClient = null;
            ServiceBusReceiver queueReceiver = null;
            try
            {

                AzureClient = new ServiceBusClient(serviceBusConnection);

                //secure mode: Delete after confirmation in subsequent call 
                //fast mode: Delete as and when received
                queueReceiver = AzureClient.CreateReceiver(queueName, new ServiceBusReceiverOptions()
                {
                    ReceiveMode = input.OperationMode == Mode.fast || input.OperationMode == Mode.fastverbose ? ServiceBusReceiveMode.ReceiveAndDelete : ServiceBusReceiveMode.PeekLock

                });

                var pollingInterval = 0;
                if (String.IsNullOrEmpty(Environment.GetEnvironmentVariable("PollingInterval")) ||
                    !Int32.TryParse(Environment.GetEnvironmentVariable("PollingInterval"), out pollingInterval))
                {
                    var argException = new ArgumentException("Polling Interval is not an integer.");

                    telemetryClient.TrackException(argException);
                    telemetryClient.Flush();
                    envelop.ErrorMessage = "Polling Interval is not an integer.";
                    envelop.ErrorCode = 128;
                    envelop.MessageCount = 0;
                    envelop.Messages = responseMessageList;
                    envelop.BatchSize = accumulatedBatchSize;
                    return envelop;
                }
                traceEvent.Properties.Add("StartPollingServiceBus", stopwatch.ElapsedMilliseconds.ToString());
                //Batching message
                while (true)
                {

                    var message = await queueReceiver.ReceiveMessageAsync(TimeSpan.FromMilliseconds(pollingInterval));
                    if (message == null)
                    {

                        break;
                    }
                    if (input.OperationMode == Mode.secure && input.PHRConnectIDs.Contains(message.MessageId.ToLower()))
                    {
                        await queueReceiver.CompleteMessageAsync(message);
                        deletedPHRConnectIDs.Add(message.MessageId.ToLower());
                        continue;
                    }
                    if ((input.StartTime - DateTime.UtcNow).TotalSeconds > (input.PollingTimeoutInSeconds * 0.85))
                        break;


                    messageCount++;

                    var bytes = message.Body.ToArray();

                    string content = string.Empty;
                    content = Convert.ToBase64String(bytes);

                    var responseMessage = new OutputDataModel()
                    {
                        content = content
                    };
                    responseMessage.properties.Add(new KeyValuePair<string, string>("PHRConnectID", message.MessageId.ToLower()));
                    phrConnectIDsSent.Add(message.MessageId.ToLower());

                    var trackingID = string.Empty;
                    var noun = "ReportabilityResponse";
                    var verb = "Receive";
                    var destinationID = string.Empty;
                    foreach (var prop in message.ApplicationProperties)
                    {

                        responseMessage.properties.Add(new KeyValuePair<string, string>(prop.Key, prop.Value == null ? string.Empty : prop.Value.ToString()));
                        if (prop.Key.ToUpper().Equals("CORELOGGINGID"))
                            trackingID = prop.Value == null ? string.Empty : prop.Value.ToString();
                        else if (prop.Key.ToUpper().Equals("EXECUTIONCONTEXTACTIVITYID"))
                            trackingID = prop.Value == null ? string.Empty : prop.Value.ToString();
                        else if (prop.Key.ToUpper().Equals("ACCOUNTID"))
                            accountIDs.Add(prop.Value == null ? string.Empty : prop.Value.ToString());
                        else if (prop.Key.ToUpper().Equals("NOUN"))
                            noun = prop.Value == null ? string.Empty : prop.Value.ToString();
                        else if (prop.Key.ToUpper().Equals("VERB"))
                            verb = prop.Value == null ? string.Empty : prop.Value.ToString();
                        else if (prop.Key.ToUpper().Equals("DESTINATIONID"))
                            destinationID = prop.Value == null ? string.Empty : prop.Value.ToString();

                    }
                    //Adding Noun and Verb to response message
                    responseMessage.properties.Add(new KeyValuePair<string, string>("NOUN", noun));
                    responseMessage.properties.Add(new KeyValuePair<string, string>("VERB", verb));
                    nounList.Add(noun);
                    verbList.Add(verb);
                    destinationIDList.Add(destinationID);
                    executionContextIDs.Add(trackingID);
                    responseMessageList.Add(responseMessage);
                    accumulatedBatchSize += content.Length;
                    if (accumulatedBatchSize >= this.Configuration.BatchSizeLimit)
                    {
                        break;
                    }
                }
                await queueReceiver.CloseAsync();
                await queueReceiver.DisposeAsync();
            }

            catch (Exception ex)
            {


                telemetryClient.TrackException(ex);
                telemetryClient.Flush();
                log.LogError(ex.Message);
                log.LogError(ex.StackTrace);
                envelop.ErrorMessage = ex.Message;
                envelop.ErrorCode = 126;
                return envelop;
            }
            finally
            {
                if (queueReceiver != null && queueReceiver.IsClosed)
                {
                    await queueReceiver.CloseAsync();
                    await queueReceiver.DisposeAsync();
                }
            }


            traceEvent.Properties.Add("StopPollingServiceBus", stopwatch.ElapsedMilliseconds.ToString());
            var state = string.Empty;
            try
            {
                var countList = new List<int>();
                countList.Add(executionContextIDs.Count);
                countList.Add(phrConnectIDsSent.Count);
                countList.Add(accountIDs.Count);
                countList.Add(deletedPHRConnectIDs.Count);
                state = "list";
                if (!(input.PHRConnectIDs is null) && input.PHRConnectIDs.Count > 0)
                {
                    countList.Add(input.PHRConnectIDs.Count);
                    state = "hasPHRConnectIDs";
                }
                var maxCount = countList.Max();
                state = "max";

                for (int i = 0; i <= maxCount / this.Configuration.TraceLimit; i++)
                {
                    var EventName = String.Concat("IDTrack", i.ToString());
                    var tempEventTelemetry = new EventTelemetry(EventName);
                    telemetryClient.TrackEvent(tempEventTelemetry);
                    eventList.Add(tempEventTelemetry);
                }
                state = "EventList Added;";
                TraceEvent.Properties.Add("MessageCount", messageCount.ToString());
                TraceEvent.Properties.Add("BatchSize", accumulatedBatchSize.ToString());
                state = "properties Added";
                if (destinationIDList.Count > 0)
                    TrackInput("DESTINATIONIDs", destinationIDList, eventList);
                if (nounList.Count > 0)
                    TrackInput("NOUNs", nounList, eventList);
                if (verbList.Count > 0)
                    TrackInput("VERBs", verbList, eventList);
                if (executionContextIDs.Count > 0)
                    TrackInput("EXECUTIONCONTEXTACTIVITYIDs", executionContextIDs, eventList);
                state = "executionContextIDs Added";
                if (phrConnectIDsSent.Count > 0)
                    TrackInput("PHRConnectIDsReceived", phrConnectIDsSent, eventList);
                state = "phrConnectIDsSent Added";
                if (accountIDs.Count > 0)
                    TrackInput("ACCOUNTIDs", accountIDs, eventList);
                state = "accountIDs Added";
                if (deletedPHRConnectIDs.Count > 0)
                    TrackInput("DeletedPHRConnectIDs", deletedPHRConnectIDs, eventList);
                state = "deletedPHRConnectIDs Added";
                if (!(input.PHRConnectIDs is null) && input.PHRConnectIDs.Count > 0)
                {

                    TrackInput("DeliveredPHRConnectIDs", input.PHRConnectIDs, eventList);
                }
                state = "PHRConnectIDs Added";
            }
            catch (Exception ex)
            {
                telemetryClient.TrackException(ex);
                telemetryClient.Flush();
                log.LogError(state);
                log.LogError(ex.Message);
                log.LogError(ex.StackTrace);
            }
            envelop.MessageCount = messageCount;
            envelop.Messages = responseMessageList;
            envelop.BatchSize = accumulatedBatchSize;
            envelop.DeletedPHRConnectIDs = deletedPHRConnectIDs;

            return envelop;
        }

        private void TrackInput(string parameterName, List<string> IDs, List<EventTelemetry> eventList)
        {
            //Add value to new event when the limit is reached.

            for (int i = 0; i <= IDs.Count / this.Configuration.TraceLimit; i++)
            {
                var tempIDs = IDs.Skip(i * this.Configuration.TraceLimit).Take(this.Configuration.TraceLimit).ToArray();
                eventList[i].Properties.Add(new KeyValuePair<string, string>(parameterName, string.Join(", ", tempIDs)));
            }

        }

        public async Task<InputDataModel> FetchBodyJson(HttpRequest req, Microsoft.Extensions.Logging.ILogger log)
        {

            string requestBody = String.Empty;
            using (StreamReader streamReader = new StreamReader(req.Body))
            {

                requestBody = await streamReader.ReadToEndAsync();
            }
            log.LogInformation("Returning from FetchBodyJson");
            return JsonConvert.DeserializeObject<InputDataModel>(requestBody);
        }


        private async Task<string> ValidateAndGenerateSecretName(string tenantID)
        {


            var validSecretTenantID = Regex.Replace(tenantID, "[^a-zA-Z0-9\\-]", "");
            if (validSecretTenantID.Length > 127)
            {
                var overLength = validSecretTenantID.Length - 127;
                validSecretTenantID = validSecretTenantID.Substring(0, validSecretTenantID.Length - overLength);


            }
            return validSecretTenantID;
        }

    }

}
