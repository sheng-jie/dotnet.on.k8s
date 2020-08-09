using Newtonsoft.Json;

namespace K8S.NET.Apollo.Controllers
{
    public class LoggingOptions
    {
        [JsonProperty("LogLevel")]
        public LogLevelOptions LogLevel { get; set; }

        public class LogLevelOptions
        {
            [JsonProperty("Default")]
            public string Default { get; set; }

            [JsonProperty("Microsoft")]
            public string Microsoft { get; set; }

            [JsonProperty("Microsoft.Hosting.Lifetime")]
            public string MicrosoftHostingLifetime { get; set; }
        }
    }




}
