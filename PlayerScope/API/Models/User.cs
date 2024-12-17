using Dalamud;
using Newtonsoft.Json;
using PlayerScope.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PlayerScope.API.Models
{
    public class User
    {
        [JsonProperty("0")]
        public string BaseUrl { get; set; }
        [JsonProperty("1")]
        public int GameAccountId { get; set; }
        [JsonProperty("2")]
        public long LocalContentId { get; set; }
        [JsonProperty("3")]
        public string Name { get; set; } = string.Empty;
        [JsonProperty("4")]
        public int AppRoleId { get; set; }
        [JsonProperty("5")]
        public List<UserCharacterDto> Characters { get; set; } = new List<UserCharacterDto>();
        [JsonProperty("6")]
        public UserNetworkStatsDto NetworkStats { get; set; } = new UserNetworkStatsDto();
        [JsonProperty("7")]
        public List<UserLodestoneCharacterDto> LodestoneCharacters { get; set; } = new List<UserLodestoneCharacterDto>();
        public class UserCharacterDto
        {
            [JsonProperty("1"), JsonPropertyName("1")]
            public string? Name { get; set; }
            [JsonProperty("2"), JsonPropertyName("2")]
            public long? LocalContentId { get; set; }
            [JsonProperty("3"), JsonPropertyName("3")]
            public CharacterPrivacySettingsDto? Privacy { get; set; }
            [JsonProperty("4"), JsonPropertyName("4")]
            public CharacterProfileVisitInfoDto? ProfileVisitInfo { get; set; }
            public class CharacterProfileVisitInfoDto
            {
                [JsonProperty("1"), JsonPropertyName("1")]
                public int? ProfileTotalVisitCount { get; set; }
                [JsonProperty("2"), JsonPropertyName("2")]
                public int? LastProfileVisitDate { get; set; }
            }
            [JsonProperty("5")]
            public string? AvatarLink { get; set; }
        }
        public class CharacterPrivacySettingsDto
        {
            [JsonProperty("1"), JsonPropertyName("1")]
            public bool HideFullProfile { get; set; }
            [JsonProperty("2"), JsonPropertyName("2")]
            public bool HideTerritoryInfo { get; set; }
            [JsonProperty("3"), JsonPropertyName("3")]
            public bool HideCustomizations { get; set; }
            [JsonProperty("4"), JsonPropertyName("4")]
            public bool HideInSearchResults { get; set; }
            [JsonProperty("5"), JsonPropertyName("5")]
            public bool HideRetainersInfo { get; set; }
            [JsonProperty("6"), JsonPropertyName("6")]
            public bool HideAltCharacters { get; set; }
        }

        public class UserNetworkStatsDto
        {
            [JsonProperty("1")]
            public int UploadedPlayersCount { get; set; } = 0;
            [JsonProperty("2")]
            public int UploadedPlayerInfoCount { get; set; } = 0;
            [JsonProperty("3")]
            public int UploadedRetainersCount { get; set; } = 0;
            [JsonProperty("4")]
            public int UploadedRetainerInfoCount { get; set; } = 0;
            [JsonProperty("5")]
            public int FetchedPlayerInfoCount { get; set; } = 0;
            [JsonProperty("6")]
            public int SearchedNamesCount { get; set; } = 0;
            [JsonProperty("7")]
            public int LastSyncedTime { get; set; } = 0;
        }

        public class UserLodestoneCharacterDto()
        {
            [JsonProperty("1")]
            public int LodestoneId { get; set; }
            [JsonProperty("2")]
            public string NameAndWorld { get; set; } = string.Empty;
            [JsonProperty("3")]
            public string AvatarLink { get; set; } = string.Empty;
            [JsonProperty("4")]
            public int VerifiedAt { get; set; } = 0;
        }

        public enum Roles
        {
            Banned = -1,
            Guest = 0,
            Member = 1,
            Verified_Member = 2,
            Vip = 5,
            Moderator = 8,
            Admin = 9,
            Owner = 10
        }
    }
}
