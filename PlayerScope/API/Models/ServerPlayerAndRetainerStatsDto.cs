using Lumina.Excel.Sheets;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlayerScope.API.Models
{
    public class ServerPlayerAndRetainerStatsDto
    {
        [JsonProperty("0")]
        public List<WorldCountStat> PlayerWorldStats { get; set; } = new List<WorldCountStat>();
        [JsonProperty("1")]
        public List<WorldCountStat> RetainerWorldStats { get; set; } = new List<WorldCountStat>();
        [JsonProperty("2")]
        public long LastUpdate { get; set; }
    }

    public class WorldCountStat
    {
        [JsonProperty("0")]
        public short WorldId { get; set; }
        [JsonProperty("1")]
        public int Count { get; set; }
    }

    public class WorldStructWithCountStat
    {
        public World World { get; set; }
        public int Count { get; set; }
    }
}
