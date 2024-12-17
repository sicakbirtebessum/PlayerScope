using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlayerScope.API.Models
{
    public class ClaimLodestoneCharacterDto()
    {
        [JsonProperty("1")]
        public int LodestoneId { get; set; }
        [JsonProperty("2")]
        public string NameAndWorld { get; set; } = string.Empty;
        [JsonProperty("3")]
        public string AvatarLink { get; set; } = string.Empty;
        [JsonProperty("4")]
        public string VerifyCode { get; set; } = string.Empty;
        [JsonProperty("5")]
        public string Message { get; set; } = string.Empty;
    }
}
