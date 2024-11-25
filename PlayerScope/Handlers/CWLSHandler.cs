using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using Lumina.Excel.Sheets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using PlayerScope.API.Models;
using PlayerScope.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlayerScope.Handlers;

internal sealed unsafe class CWLSHandler : IDisposable
{
    private const string AddonName = "CrossWorldLinkshell";

    private readonly ILogger<CWLSHandler> _logger;
    private readonly PersistenceContext _persistenceContext;
    private readonly IAddonLifecycle _addonLifecycle;

    public CWLSHandler(
       ILogger<CWLSHandler> logger,
       PersistenceContext persistenceContext,
       IAddonLifecycle addonLifecycle)
    {
        _logger = logger;
        _persistenceContext = persistenceContext;
        _addonLifecycle = addonLifecycle;

        _addonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, AddonName, PostRequestedUpdate);
    }

    private void PostRequestedUpdate(AddonEvent type, AddonArgs args)
    {
        CWLSOnUpdate((AgentCrossWorldLinkshell*)args.Addon);
    }

    private void CWLSOnUpdate(AgentCrossWorldLinkshell* addon)
    {
        try
        {
            if (addon == null)
                return;

            List<PostPlayerRequest> playerRequests = new();

            unsafe
            {
                if (InfoProxyCrossWorldLinkshellMember.Instance() != null && AgentCrossWorldLinkshell.Instance() != null && InfoProxyCrossWorldLinkshell.Instance() != null)
                {
                    foreach (var characterData in InfoProxyCrossWorldLinkshellMember.Instance()->CharDataSpan)
                    {
                        playerRequests.Add(new PostPlayerRequest
                        {
                            LocalContentId = characterData.ContentId,
                            Name = characterData.NameString,
                            HomeWorldId = characterData.HomeWorld,
                            CreatedAt = Tools.UnixTime,
                        });
                    }
                }
            }

            if (playerRequests.Count > 0)
                PersistenceContext.AddPlayerUploadData(playerRequests);
        }
        catch (Exception e)
        {
            _logger.LogInformation(e, "CWLS");
        }
    }

    public void Dispose()
    {
        _addonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, AddonName, PostRequestedUpdate);
    }
}