using System;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Game.Network.Structures;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Logging;
using PlayerScope.API.Models;
using PlayerScope.Database;

namespace PlayerScope.Handlers;

internal sealed class MarketBoardOfferingsHandler : IDisposable
{
    private readonly IMarketBoard _marketBoard;
    private readonly ILogger<MarketBoardOfferingsHandler> _logger;
    private readonly IClientState _clientState;
    private readonly PersistenceContext _persistenceContext;

    public MarketBoardOfferingsHandler(
        IMarketBoard marketBoard,
        ILogger<MarketBoardOfferingsHandler> logger,
        IClientState clientState,
        PersistenceContext persistenceContext)
    {
        _marketBoard = marketBoard;
        _logger = logger;
        _clientState = clientState;
        _persistenceContext = persistenceContext;

        _marketBoard.OfferingsReceived += HandleOfferings;
    }

    public void Dispose()
    {
        _marketBoard.OfferingsReceived += HandleOfferings;
    }

    private void HandleOfferings(IMarketBoardCurrentOfferings currentOfferings)
    {
        ushort worldId = (ushort?)_clientState.LocalPlayer?.CurrentWorld.RowId ?? 0;
        if (worldId == 0)
        {
            _logger.LogInformation("Skipping market board handler, current world unknown");
            return;
        }
        var updates =
               currentOfferings.ItemListings
                   .Cast<MarketBoardCurrentOfferings.MarketBoardItemListing>()
                   .DistinctBy(o => o.RetainerId)
                   .Where(l => l.RetainerId != 0)
                   .Where(l => l.RetainerOwnerId != 0)
                   .Select(l =>
                       new Retainer
                       {
                           LocalContentId = l.RetainerId,
                           Name = l.RetainerName,
                           WorldId = worldId,
                           OwnerLocalContentId = l.RetainerOwnerId,
                       }).ToList();

        var toRetainerRequests = updates.Select(a => new PostRetainerRequest
        {
            LocalContentId = a.LocalContentId,
            Name = a.Name,
            WorldId = a.WorldId,
            OwnerLocalContentId = a.OwnerLocalContentId,
            CreatedAt = Tools.UnixTime,
        }).ToList();

        Task.Run(() => _persistenceContext.HandleMarketBoardPage(updates));

        if (toRetainerRequests.Count > 0)
            PersistenceContext.AddRetainerUploadData(toRetainerRequests);
    }
}
