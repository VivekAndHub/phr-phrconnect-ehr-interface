using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Text;

namespace Mdrx.PhrHubHub.PHR.PHRConnect.MessageFunction.DataModel
{
    public class OutputDataModel
    {
        public string content { get; set; }
        public List<KeyValuePair<string, string>> properties = new List<KeyValuePair<string, string>>();
    }

    public class OutputDataEnvelope
    {
        public List<OutputDataModel> Messages = new List<OutputDataModel>();
        public List<string> DeletedPHRConnectIDs;
        public string ErrorMessage = string.Empty;
        public Int32 MessageCount;
        public Int32 ErrorCode;
        public Int32 BatchSize;
        public string MessageOperationID;
    }

    public class InputDataModel
    {
        
        public List<String> PHRConnectIDs;
        public string ShieldToken;
        public string TenantID;
        [JsonConverter(typeof(StringEnumConverter))]
        public Mode OperationMode;
        public string AccountID;
        public string ShieldOperationID;
        public string MessageOperationID;
        public int PollingTimeoutInSeconds;
        public DateTime StartTime;
    }
    /// <summary>
    /// enum for modes of operation
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum Mode
    {
        fast,
        secure,
        fastverbose,
        secureverbose,
        test
    }

    // Data structure of queue info
    class QueueInfo
    {
        public string tenantid { get; set; }
        public Properties properties { get; set; }
    }

    class Properties
    {
        public string ServiceBusConnection { get; set; }
        public string QueueName { get; set; }
        public Boolean Enabled { get; set; }
        public string GroupName { get; set; }
        
    }
}
