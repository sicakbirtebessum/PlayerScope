using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Style;
using FFXIVClientStructs.FFXIV.Common.Math;
using Newtonsoft.Json;
using PlayerScope.Database;
using PlayerScope.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PlayerScope.API.Models
{
    public class PlayerDetailed
    {
        [JsonProperty("F")]
        public int[] Flags { get; set; } = [];
        [JsonProperty("1")]
        public long LocalContentId { get; set; }
        [JsonProperty("2")]
        public int? AccountId { get; set; }
        [JsonProperty("3")]
        public List<PlayerCustomizationHistoryDto> PlayerCustomizationHistories { get; set; } = new List<PlayerCustomizationHistoryDto>();
        [JsonProperty("4")]
        public List<PlayerTerritoryHistoryDto> TerritoryHistory { get; set; } = new List<PlayerTerritoryHistoryDto>();
        [JsonProperty("5")]
        public PlayerLodestoneDto? PlayerLodestone { get; set; } = null;
        [JsonProperty("6")]
        public List<PlayerNameHistoryDto> PlayerNameHistories { get; set; } = new List<PlayerNameHistoryDto>();
        [JsonProperty("7")]
        public List<PlayerWorldHistoryDto> PlayerWorldHistories { get; set; } = new List<PlayerWorldHistoryDto>();
        [JsonProperty("8")]
        public List<RetainerDto> Retainers { get; set; } = new List<RetainerDto>();
        [JsonProperty("9")]
        public List<PlayerDetailedInfoAltCharDto> PlayerAltCharacters { get; set; } = new List<PlayerDetailedInfoAltCharDto>();
        [JsonProperty("0")]
        public PlayerProfileVisitInfoDto? ProfileVisitInfo { get; set; } = null;
        public class PlayerProfileVisitInfoDto
        {
            [JsonProperty("1")]
            public int? ProfileTotalVisitCount { get; set; } = 0;
            [JsonProperty("2")]
            public int? LastProfileVisitDate { get; set; } = 0;
            [JsonProperty("3")]
            public int? UniqueVisitorCount { get; set; } = 0;
        }
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

        public enum PlayerFlagKey
        {
            IsBot = 0,
            IsGM = 1,
            IsPrivate = 2,
            IsSelfPrivate = 3,
            Custom = 4,
        }
        public class FlagInfo
        {
            public FontAwesomeIcon Icon { get; set;}
            public string Message { get; set; }
            public Vector4 Color { get; set; }
        }

        public static void UpdateFlagMessages()
        {
            PlayerFlagsDict[PlayerFlagKey.IsBot].Message = Loc.DtPlayerFlagsBotMessage;
            PlayerFlagsDict[PlayerFlagKey.IsGM].Message = Loc.DtPlayerFlagsGMMessage;
            PlayerFlagsDict[PlayerFlagKey.IsPrivate].Message = Loc.DtPlayerFlagsPrivateMessage;
            PlayerFlagsDict[PlayerFlagKey.IsSelfPrivate].Message = Loc.DtPlayerFlagsSelfPrivateMessage;
        }

        public static Dictionary<PlayerFlagKey, FlagInfo> PlayerFlagsDict = new Dictionary<PlayerFlagKey, FlagInfo>
        {
            { PlayerFlagKey.IsBot, new FlagInfo { Icon = FontAwesomeIcon.ExclamationTriangle, Message = Loc.DtPlayerFlagsBotMessage, Color = KnownColor.IndianRed.Vector() } },
            { PlayerFlagKey.IsGM, new FlagInfo { Icon = FontAwesomeIcon.UserShield, Message = Loc.DtPlayerFlagsGMMessage, Color = KnownColor.Gold.Vector() } },
            { PlayerFlagKey.IsPrivate, new FlagInfo { Icon = FontAwesomeIcon.Lock, Message = Loc.DtPlayerFlagsPrivateMessage, Color = KnownColor.IndianRed.Vector() } },
            { PlayerFlagKey.IsSelfPrivate, new FlagInfo { Icon = FontAwesomeIcon.Lock, Message =  Loc.DtPlayerFlagsSelfPrivateMessage, Color = ImGuiColors.HealerGreen } },
        };

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

        public class PlayerLodestoneDto
        {
            [JsonProperty("1")]
            public int? LodestoneId { get; set; }
            [JsonProperty("2")]
            public int? CharacterCreationDate { get; set; }
            [JsonProperty("3")]
            public string? AvatarLink { get; set; }
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
            [JsonProperty("5")]
            public string? AvatarLink { get; set; }
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
