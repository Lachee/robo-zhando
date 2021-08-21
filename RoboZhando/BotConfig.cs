using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RoboZhando
{
    public class BotConfig
    {
        public string Prefix { get; set; } = "\\";

        public string Token { get; set; }
        public string AuzreKey { get; set; }
        public string AzureRegion { get; set; } = "eastus";
        public string AzureEndpoint { get; set; } = "https://eastus.api.cognitive.microsoft.com/sts/v1.0/issuetoken";

        public string AnouncerVoice { get; set; } = "";

        public RedisConfig Redis { get; set; } = new RedisConfig();
        public class RedisConfig
        {
            public string Address = "127.0.0.1";
            public int Database = 0;
            public string Prefix = "zhando";
        }
        
    }
}
