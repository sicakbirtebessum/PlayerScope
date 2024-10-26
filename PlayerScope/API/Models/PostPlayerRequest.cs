using Newtonsoft.Json;
using PlayerScope.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PlayerScope.API.Models
{
    public class PostPlayerRequest
    {
        [JsonPropertyName("1")]
        public ulong LocalContentId { get; set; }
        [JsonPropertyName("2")]
        public string Name { get; set; } = string.Empty;
        [JsonPropertyName("3")]
        public ushort? HomeWorldId { get; set; }
        [JsonPropertyName("4")]
        public int? AccountId { get; set; }
        [JsonPropertyName("5")]
        public short? TerritoryId { get; set; }
        [JsonPropertyName("6")]
        public string? PlayerPos { get; set; }
        [JsonPropertyName("7")]
        public ushort? CurrentWorldId { get; set; }
        [JsonPropertyName("8")]
        public PlayerCustomization? Customization { get; set; }
        [JsonPropertyName("9")]
        public int CreatedAt { get; set; }
    }
}
