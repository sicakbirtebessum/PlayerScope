using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PlayerScope.Models
{
    public class PlayerCustomization
    {
        [JsonPropertyName("1")]
        public byte? BodyType { get; set; }
        [JsonPropertyName("2")]
        public byte? GenderRace { get; set; }
        [JsonPropertyName("3")]
        public byte? Height { get; set; }
        [JsonPropertyName("4")]
        public byte? Face { get; set; }
        [JsonPropertyName("5")]
        public byte? SkinColor { get; set; }
        [JsonPropertyName("6")]
        public byte? Nose { get; set; }
        [JsonPropertyName("7")]
        public byte? Jaw { get; set; }
        [JsonPropertyName("8")]
        public byte? MuscleMass { get; set; }
        [JsonPropertyName("9")]
        public byte? BustSize { get; set; }
        [JsonPropertyName("A")]
        public byte? TailShape { get; set; }
        [JsonPropertyName("B")]
        public byte? Mouth { get; set; }
        [JsonPropertyName("C")]
        public byte? EyeShape { get; set; }
        [JsonPropertyName("D")]
        public bool? SmallIris { get; set; }
    }
}
