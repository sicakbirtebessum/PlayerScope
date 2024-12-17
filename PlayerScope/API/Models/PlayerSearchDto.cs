using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlayerScope.API.Models
{
    public class PlayerSearchDto
    {
        [JsonProperty("L")]
        public long LocalContentId { get; set; }
        [JsonProperty("N")]
        public string Name { get; set; } = string.Empty;
        [JsonProperty("W")]
        public short? WorldId { get; set; }
        [JsonProperty("A")]
        public int? AccountId { get; set; }
        [JsonProperty("B")]
        public string? AvatarLink { get; set; }
    }
}
