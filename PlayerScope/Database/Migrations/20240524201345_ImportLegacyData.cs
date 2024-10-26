using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Dalamud.Plugin;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable 0612

namespace PlayerScope.Database.Migrations
{
    /// <inheritdoc />
    //public partial class ImportLegacyData : Migration
    //{
    //    public static IDalamudPluginInterface PluginInterface { get; set; }

    //    private static readonly string[] PlayerColumns = new[] { "LocalContentId", "Name" };
    //    private static readonly string[] RetainerColumns = new[] { "LocalContentId", "Name", "WorldId", "OwnerLocalContentId" };

        /// <inheritdoc />
        //protected override void Up(MigrationBuilder migrationBuilder)
        //{
        //    if (PluginInterface == null)
        //        return;

        //    string legacyDatabaseFileName = Path.Join(PluginInterface.GetPluginConfigDirectory(), "retainer-data_Dev.litedb");
        //    if (!File.Exists(legacyDatabaseFileName))
        //        return;

        //    using var liteDatabase = new LiteDatabase(new ConnectionString
        //    {
        //        Filename = Path.Join(PluginInterface.GetPluginConfigDirectory(), "retainer-data_Dev.litedb"),
        //        Connection = ConnectionType.Direct,
        //        Upgrade = true,
        //    },
        //    new BsonMapper
        //    {
        //        ResolveCollectionName = (type) =>
        //        {
        //            if (type == typeof(LegacyPlayer))
        //                return LegacyPlayer.CollectionName;

        //            if (type == typeof(LegacyRetainer))
        //                return LegacyRetainer.CollectionName;
        //            throw new ArgumentOutOfRangeException(nameof(type));
        //        }
        //    });
        //    liteDatabase.GetCollection<LegacyRetainer>()
        //        .EnsureIndex(x => x.Id);
        //    liteDatabase.GetCollection<LegacyPlayer>()
        //        .EnsureIndex(x => x.Id);

        //    List<LegacyPlayer> allPlayers = liteDatabase.GetCollection<LegacyPlayer>().FindAll().ToList();
        //    object[,] playersToInsert = To2DArray(
        //        allPlayers.Select(player => new object[] { player.Id, player.Name }).ToList(),
        //        PlayerColumns.Length);

        //    migrationBuilder.InsertData(
        //        table: "Players",
        //        columns: PlayerColumns,
        //        values: playersToInsert);

        //    List<LegacyRetainer> allRetainers = liteDatabase.GetCollection<LegacyRetainer>().FindAll().ToList();
        //    object[,] retainersToInsert = To2DArray(
        //        allRetainers.Select(retainer => new object[] { retainer.Id, retainer.Name, retainer.WorldId, retainer.OwnerContentId }).ToList(),
        //        RetainerColumns.Length);

        //    migrationBuilder.InsertData(
        //        table: "Retainers",
        //        columns: RetainerColumns, values: retainersToInsert);
        //}

        //[SuppressMessage("Performance", "CA1814")]
        //private static object[,] To2DArray(IReadOnlyList<object[]> data, int columnCount)
        //{
        //    object[,] result = new object[data.Count, columnCount];
        //    for (int i = 0; i < data.Count; i++)
        //    {
        //        for (int j = 0; j < columnCount; j++)
        //        {
        //            result[i, j] = data[i][j];
        //        }
        //    }
        //    return result;
        //}

        ///// <inheritdoc />
        //protected override void Down(MigrationBuilder migrationBuilder)
        //{
        //    migrationBuilder.Sql("DELETE FROM Players");
        //    migrationBuilder.Sql("DELETE FROM Retainers");
        //}
    //}
}

#pragma warning restore 0612