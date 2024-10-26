using System;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Microsoft.Extensions.Logging;

namespace PlayerScope.Handlers;

internal sealed unsafe class MarketBoardUiHandler : IDisposable
{
    private const string AddonName = "ItemSearchResult";

    private readonly ILogger<MarketBoardUiHandler> _logger;
    private readonly PersistenceContext _persistenceContext;
    private readonly IAddonLifecycle _addonLifecycle;

    public MarketBoardUiHandler(
        ILogger<MarketBoardUiHandler> logger,
        PersistenceContext persistenceContext,
        IAddonLifecycle addonLifecycle)
    {
        _logger = logger;
        _persistenceContext = persistenceContext;
        _addonLifecycle = addonLifecycle;

        _addonLifecycle.RegisterListener(AddonEvent.PreDraw, AddonName, PreDraw);
    }

    private void PreDraw(AddonEvent type, AddonArgs args)
    {
        UpdateRetainerNames((AddonItemSearchResult*)args.Addon);
    }

    private void UpdateRetainerNames(AddonItemSearchResult* addon)
    {
        try
        {
            if (addon == null || !addon->AtkUnitBase.IsVisible)
                return;

            var results = addon->Results;
            if (results == null)
                return;

            int length = results->ListLength;
            if (length == 0)
                return;

            for (int i = 0; i < length; ++i)
            {
                var listItem = results->ItemRendererList[i].AtkComponentListItemRenderer;
                if (listItem == null)
                    return;

                var uldManager = listItem->AtkComponentButton.AtkComponentBase.UldManager;
                if (uldManager.NodeListCount < 14)
                    continue;

                var retainerNameNode = (AtkTextNode*)uldManager.NodeList[5];
                if (retainerNameNode == null)
                    return;

                string retainerName = retainerNameNode->NodeText.ToString();
                if (!retainerName.Contains('*', StringComparison.Ordinal))
                {
                    string playerName = PersistenceContext.GetCharacterNameOnCurrentWorld(retainerName);
                    if (!string.IsNullOrEmpty(playerName))
                        retainerNameNode->SetText($"{playerName} * {retainerName}");
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogInformation(e, "Market board draw failed");
        }
    }

    public void Dispose()
    {
        _addonLifecycle.UnregisterListener(AddonEvent.PreDraw, AddonName, PreDraw);
    }
}
