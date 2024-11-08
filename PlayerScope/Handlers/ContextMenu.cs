using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Gui.PartyFinder.Types;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlayerScope.API.Models;
using PlayerScope.API.Query;
using PlayerScope.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static FFXIVClientStructs.FFXIV.Client.UI.AddonAetherCurrent.Delegates;
using static FFXIVClientStructs.FFXIV.Client.UI.AddonRelicNoteBook;

namespace PlayerScope.Handlers;

public class ContextMenu
{
    public static void Enable()
    {
        Plugin._contextMenu.OnMenuOpened -= OnOpenContextMenu;
        Plugin._contextMenu.OnMenuOpened += OnOpenContextMenu;
    }

    public static void Disable()
    {
        Plugin._contextMenu.OnMenuOpened -= OnOpenContextMenu;
    }

    private static bool IsMenuValid(IMenuArgs menuOpenedArgs)
    {
        if (menuOpenedArgs.Target is not MenuTargetDefault menuTargetDefault)
        {
            return false;
        }
        switch (menuOpenedArgs.AddonName)
        {
            case null: // Nameplate/Model menu
            case "LookingForGroup":
            case "PartyMemberList":
            case "FriendList":
            case "FreeCompany":
            case "SocialList":
            case "ContactList":
            case "ChatLog":
            case "_PartyList":
            case "LinkShell":
            case "CrossWorldLinkshell":
            case "ContentMemberList": // Eureka/Bozja/...
            case "BeginnerChatList":
                return menuTargetDefault.TargetName != string.Empty && Util.IsWorldValid(menuTargetDefault.TargetHomeWorld.Id);
            case "BlackList":
            case "MuteList":
                return menuTargetDefault.TargetName != string.Empty;
        }

        return false;
    }

    private static void OnOpenContextMenu(IMenuOpenedArgs menuOpenedArgs)
    {
        if (menuOpenedArgs.Target is not MenuTargetDefault menuTargetDefault)
        {
            return;
        }

        if (!IsMenuValid(menuOpenedArgs))
            return;

        if (menuTargetDefault.TargetHomeWorld.Id < 10000)
        {
            if (menuTargetDefault.TargetContentId != 0)
            {
                menuOpenedArgs.AddMenuItem(new MenuItem
                {
                    PrefixColor = 15,
                    PrefixChar = 'R',
                    Name = "See Detailed Info",
                    OnClicked = SearchDetailedPlayerInfoById
                });
            }
            else if (!string.IsNullOrEmpty(menuTargetDefault.TargetName))
            {
                menuOpenedArgs.AddMenuItem(new MenuItem
                {
                    PrefixColor = 17,
                    PrefixChar = 'R',
                    Name = "Search Player By Name",
                    OnClicked = SearchPlayerName
                });
            }
        }
    }

    private static void SearchDetailedPlayerInfoById(IMenuItemClickedArgs menuArgs)
    {
        if (menuArgs.Target is not MenuTargetDefault menuTargetDefault)
        {
            return;
        }
        ulong? targetCId = menuTargetDefault.TargetContentId;

        DetailsWindow.Instance.IsOpen = true;
        DetailsWindow.Instance.OpenDetailedPlayerWindow((ulong)targetCId, true);
    }

    private static void SearchPlayerName(IMenuItemClickedArgs menuArgs)
    {
        if (menuArgs.Target is not MenuTargetDefault menuTargetDefault)
        {
            return;
        }

        var targetName = string.Empty;

        if (menuArgs.AddonName == "BlackList")
        {
            targetName = GetBlacklistSelectPlayerName();
        }
        else if (menuArgs.AddonName == "MuteList")
        {
            targetName = GetMuteListSelectFullName();
        }
        else
        {
            targetName = menuTargetDefault.TargetName;
        }

        MainWindow.Instance.IsOpen = true;
        MainWindow.Instance._searchContent = targetName;
        var query = new PlayerQueryObject() { Name = targetName };
        _ = System.Threading.Tasks.Task.Run(() =>
        {
            MainWindow.Instance.bIsNetworkProcessing = true;
            var request = MainWindow.Instance._client.GetPlayers<PlayerSearchDto>(query).ConfigureAwait(false).GetAwaiter().GetResult();
            if (request.Page == null)
            {
                MainWindow.Instance.SetPlayerResult((MainWindow.Instance._LastPlayerSearchResult.Players, request.Message));
                MainWindow.Instance.bIsNetworkProcessing = false;
                return;
            }

            MainWindow.Instance.SetPlayerResult((request.Page.Data.ToDictionary(t => t.LocalContentId, t => t), request.Message));
            MainWindow.Instance.bIsNetworkProcessing = false;
        });
    }

    private static unsafe string GetBlacklistSelectPlayerName()
    {
        var agentBlackList = (AgentBlacklist*)Framework.Instance()->GetUIModule()->GetAgentModule()->GetAgentByInternalId(AgentId.Blacklist);
        if (agentBlackList != null)
        {
            return MemoryHelper.ReadSeString(&agentBlackList->SelectedPlayerName).TextValue;
        }

        return string.Empty;
    }

    private static unsafe string GetMuteListSelectFullName()
    {
        var agentMuteList = Framework.Instance()->GetUIModule()->GetAgentModule()->GetAgentByInternalId(AgentId.Mutelist);
        if (agentMuteList != null)
        {
            return MemoryHelper.ReadSeStringNullTerminated(*(nint*)((nint)agentMuteList + 0x58)).TextValue; // should create the agent in CS later
        }

        return string.Empty;
    }
}