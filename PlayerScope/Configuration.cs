using Dalamud.Configuration;
using Newtonsoft.Json;
using System;
using Dalamud.Interface;
using Dalamud.Plugin;
using PlayerScope.GUI;
using System.ComponentModel.DataAnnotations.Schema;
using PlayerScope.API.Models;
using System.Collections.Generic;
using System.Collections.Concurrent;
using static PlayerScope.Handlers.PersistenceContext;

namespace PlayerScope
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 1;
        public string BaseUrl { get; set; } = "https://localhost:5001/v1/";
        public string Username { get; set; } = string.Empty;
        public long ContentId { get; set; }
        public int AccountId { get; set; }
        public string Key { get; set; } = string.Empty;
        public bool LoggedIn { get; set; } = false;
        public bool FreshInstall { get; set; } = true;
        public int AppRoleId { get; set; } = 0;
        public int? UploadedPlayersCount { get; set; } = 0;
        public int? UploadedPlayerInfoCount { get; set; } = 0;
        public int? UploadedRetainersCount { get; set; } = 0;
        public int? UploadedRetainerInfoCount { get; set; } = 0;
        public int? FetchedPlayerInfoCount { get; set; } = 0;
        public int? SearchedNamesCount { get; set; } = 0;
        public int? LastSyncedTime { get; set; }
        public ConcurrentDictionary<long, CachedFavoritedPlayer> FavoritedPlayer = new();
        public bool bShowDetailedDate { get; set; } = false;
        public bool bHideCharacterAvatars { get; set; } = false;
        public LanguageEnum Language { get; set; }
        public bool AgreementAccepted { get; set; } = false;
        public int ObjectTableRefreshInterval { get; set; } = 5_000;

        public class CachedFavoritedPlayer
        {
            public required ulong? AccountId { get; init; }
            public required string Name { get; init; }
            public string? Note { get; set; }
        }
        public enum LanguageEnum
        {
            en,
            tr
        }

        public string Token()
        {
            return $"{Key}-{AccountId}";
        }
        public void Save()
        {
            Plugin.Instance._pluginInterface.SavePluginConfig(this);
        }
    }

}
