using Newtonsoft.Json;
using PlayerScope.Database;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PlayerScope.API.Models
{
    public class PlayerDetailed
    {
        [JsonProperty("0")]
        public PrivacyStatus ProfilePrivacy { get; set; }
        [JsonProperty("1")]
        public long LocalContentId { get; set; }
        [JsonProperty("2")]
        public int? AccountId { get; set; }
        [JsonProperty("3")]
        public List<PlayerCustomizationHistoryDto> PlayerCustomizationHistories { get; set; } = new List<PlayerCustomizationHistoryDto>();
        [JsonProperty("4")]
        public PlayerLastSeenDto? PlayerLastSeenInfo { get; set; }
        [JsonProperty("5")]
        public PlayerLodestoneDto? PlayerLodestone { get; set; }
        [JsonProperty("6")]
        public List<PlayerNameHistoryDto> PlayerNameHistories { get; set; } = new List<PlayerNameHistoryDto>();
        [JsonProperty("7")]
        public ICollection<PlayerWorldHistoryDto> PlayerWorldHistories { get; set; } = new List<PlayerWorldHistoryDto>();
        [JsonProperty("8")]
        public ICollection<RetainerDto> Retainers { get; set; } = new List<RetainerDto>();
        [JsonProperty("9")]
        public ICollection<PlayerDetailedInfoAltCharDto> PlayerAltCharacters { get; set; } = new List<PlayerDetailedInfoAltCharDto>();

        public class PlayerCustomizationHistoryDto
        {
            [JsonProperty("1")]
            public byte? BodyType { get; set; }
            [JsonProperty("2")]
            public byte? GenderRace { get; set; }
            [JsonProperty("3")]
            public byte? Height { get; set; }
            [JsonProperty("4")]
            public byte? Face { get; set; }
            [JsonProperty("5")]
            public byte? SkinColor { get; set; }
            [JsonProperty("6")]
            public byte? Nose { get; set; }
            [JsonProperty("7")]
            public byte? Jaw { get; set; }
            [JsonProperty("8")]
            public byte? MuscleMass { get; set; }
            [JsonProperty("9")]
            public byte? BustSize { get; set; }
            [JsonProperty("0")]
            public byte? TailShape { get; set; }
            [JsonProperty("A")]
            public byte? Mouth { get; set; }
            [JsonProperty("B")]
            public byte? EyeShape { get; set; }
            [JsonProperty("C")]
            public bool? SmallIris { get; set; }
            [JsonProperty("D")]
            public int? CreatedAt { get; set; }
        }
        public enum PrivacyStatus
        {
            Public = 0,
            NotFound = 1,
            Private = 2,
            Self = 3,
            SelfPrivate = 4,
        }

        public class PlayerLastSeenDto
        {
            [JsonProperty("1")]
            public short? WorldId { get; set; }
            [JsonProperty("2")]
            public int? CreatedAt { get; set; }
            [JsonProperty("3")]
            public List<PlayerTerritoryHistoryDto> TerritoryHistory { get; set; } = new List<PlayerTerritoryHistoryDto>();
            public class PlayerTerritoryHistoryDto
            {
                [JsonProperty("1")]
                public short? TerritoryId { get; set; }
                [JsonProperty("2")]
                public string? PlayerPos { get; set; }
                [JsonProperty("3")]
                public short? WorldId { get; set; }
                [JsonProperty("4")]
                public int? FirstSeenAt { get; set; }
                [JsonProperty("5")]
                public int? LastSeenAt { get; set; }
            }
        }

        public class PlayerLodestoneDto
        {
            [JsonProperty("1")]
            public int? LodestoneId { get; set; }
            [JsonProperty("2")]
            public int? CharacterCreationDate { get; set; }
        }

        public class PlayerDetailedInfoAltCharDto
        {
            [Key, JsonProperty("1")]
            public long LocalContentId { get; set; }
            [JsonProperty("2")]
            public string? Name { get; set; }
            [JsonProperty("3")]
            public short? WorldId { get; set; }
            [JsonProperty("4")]
            public List<RetainerDto> Retainers { get; set; } = new List<RetainerDto>();
        }

        public class PlayerNameHistoryDto
        {
            [JsonProperty("V")]
            public string Name { get; set; } = null!;
            [JsonProperty("A")]
            public int CreatedAt { get; set; }
        }

        public class PlayerWorldHistoryDto
        {
            [JsonProperty("V")]
            public int WorldId { get; set; }
            [JsonProperty("A")]
            public int CreatedAt { get; set; }
        }
        public class RetainerDto
        {
            [JsonProperty("1")]
            public long LocalContentId { get; set; }
            [JsonProperty("2")]
            public long OwnerLocalContentId { get; set; }
            [JsonProperty("3")]
            public int LastSeen { get; set; }
            [JsonProperty("4")]
            public List<RetainerNameHistoryDto> Names { get; set; } = new List<RetainerNameHistoryDto>();
            [JsonProperty("5")]
            public List<RetainerWorldHistoryDto> Worlds { get; set; } = new List<RetainerWorldHistoryDto>();

            public partial class RetainerNameHistoryDto
            {
                [JsonProperty("V")]
                public string Name { get; set; } = null!;
                [JsonProperty("A")]
                public int CreatedAt { get; set; }
            }
            public partial class RetainerWorldHistoryDto
            {
                [JsonProperty("V")]
                public int WorldId { get; set; }
                [JsonProperty("A")]
                public int CreatedAt { get; set; }
            }
        }
    }
}
