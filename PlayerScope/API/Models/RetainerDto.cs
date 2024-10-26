using Newtonsoft.Json;

namespace PlayerScope.API.Models
{
    public class RetainerDto
    {
        [JsonProperty("L")]
        public long LocalContentId { get; set; }
        [JsonProperty("N")]
        public string? Name { get; set; }
        [JsonProperty("W")]
        public short WorldId { get; set; }
        [JsonProperty("O")]
        public long OwnerLocalContentId { get; set; }
    }
}
