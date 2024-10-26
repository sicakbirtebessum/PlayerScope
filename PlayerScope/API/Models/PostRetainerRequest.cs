using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PlayerScope.API.Models
{
    public class PostRetainerRequest
    {
        [Required, JsonPropertyName("L")]
        public ulong LocalContentId { get; set; }
        [MaxLength(24), Required, JsonPropertyName("N")]
        public string? Name { get; set; }
        [Required, JsonPropertyName("W")]
        public int WorldId { get; set; }
        [Required, JsonPropertyName("O")]
        public ulong OwnerLocalContentId { get; set; }
        [Required, JsonPropertyName("C")]
        public int CreatedAt { get; set; }
    }
}
