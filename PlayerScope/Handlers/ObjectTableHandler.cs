﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Lumina.Excel.GeneratedSheets2;
using Lumina.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using PlayerScope.API.Models;
using PlayerScope.Database;
using PlayerScope.GUI;
using PlayerScope.Models;

namespace PlayerScope.Handlers;

internal sealed class ObjectTableHandler : IDisposable
{
    private readonly IObjectTable _objectTable;
    private readonly IFramework _framework;
    private readonly IClientState _clientState;
    private readonly ILogger<ObjectTableHandler> _logger;
    private readonly PersistenceContext _persistenceContext;

    private long _lastUpdate;

    public ObjectTableHandler(IObjectTable objectTable, IFramework framework, IClientState clientState, ILogger<ObjectTableHandler> logger, PersistenceContext persistenceContext)
    {
        _objectTable = objectTable;
        _framework = framework;
        _clientState = clientState;
        _logger = logger;
        _persistenceContext = persistenceContext;

        _framework.Update += FrameworkUpdate;
    }

    private unsafe void FrameworkUpdate(IFramework framework)
    {
        long now = Environment.TickCount64;
        if (!_clientState.IsLoggedIn || now - _lastUpdate < 10_000) //_clientState.IsPvPExcludingDen
            return;

        _lastUpdate = now;

        List<PlayerMapping> playerMappings = new();
        foreach (var obj in _objectTable)
        {
            if (obj.ObjectKind == ObjectKind.Player)
            {
                var bc = (Character*)obj.Address;
                if (bc->ContentId == 0 || bc->AccountId == 0)
                    continue;

                playerMappings.Add(new PlayerMapping
                {
                    ContentId = bc->ContentId,
                    AccountId = bc->AccountId,
                    PlayerName = bc->NameString,
                    WorldId = bc->HomeWorld,
                });

                var Customization = bc->DrawData.CustomizeData;

                if (Process.GetProcessesByName("Anamnesis").Length != 0)
                    PersistenceContext.AnamnesisFound = true;

                PersistenceContext.AddPlayerUploadData(bc->ContentId, new PostPlayerRequest
                {
                    LocalContentId = bc->ContentId,
                    Name = bc->NameString,
                    AccountId = (int?)bc->AccountId,
                    HomeWorldId = bc->HomeWorld,
                    CurrentWorldId = bc->CurrentWorld,
                    TerritoryId = (short)PersistenceContext._clientState.TerritoryType,
                    PlayerPos = Util.Vector3ToString(obj.GetMapCoordinates()),
                    Customization = PersistenceContext.AnamnesisFound ? null : new PlayerCustomization
                    {
                        BodyType = Customization.BodyType,
                        BustSize = Customization.BustSize,
                        EyeShape = Customization.EyeShape,
                        Face = Customization.Face,
                        Height = Customization.Height,
                        Jaw = Customization.Jaw,
                        Mouth = Customization.Mouth,
                        MuscleMass = Customization.MuscleMass,
                        Nose = Customization.Nose,
                        SkinColor = Customization.SkinColor,
                        SmallIris = Customization.SmallIris,
                        TailShape = Customization.TailShape,
                        GenderRace = ((byte)Models.RaceEnumExtensions.CombinedRace((Gender)bc->DrawData.CustomizeData.Sex, (SubRace)bc->DrawData.CustomizeData.Tribe))
                    },
                    CreatedAt = Tools.UnixTime,
                });

                _logger.LogError($"{PersistenceContext.AnamnesisFound} - {bc->NameString}-  Cord: " + obj.GetMapCoordinates() + "   -   Second: " + obj.GetMapCoordinates().ToString() + "   -   Third: " + obj.GetMapCoordinates().AsVector128().AsUInt64() + " 4th: X" + obj.GetMapCoordinates().X + " Y:" + obj.GetMapCoordinates().Y + " Z:" + obj.GetMapCoordinates().Z);
            }
        }

        if (playerMappings.Count > 0)
            Task.Run(() => _persistenceContext.HandleContentIdMappingAsync(playerMappings));

        _logger.LogTrace("ObjectTable handling for {Count} players took {TimeMs}", playerMappings.Count, TimeSpan.FromMilliseconds(Environment.TickCount64 - now));
    }
    public void Dispose()
    {
        _framework.Update -= FrameworkUpdate;
    }
}