using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlayerScope.API.Models
{
    public class RetainerSearchDto
    {
        [JsonProperty("L")]
        public long LocalContentId { get; set; }
        [JsonProperty("N")]
        public string? Name { get; set; }
        [JsonProperty("W")]
        public ushort WorldId { get; set; }
        [JsonProperty("O")]
        public long OwnerLocalContentId { get; set; }
        [JsonProperty("C")]
        public int CreatedAt { get; set; }
    }
}
