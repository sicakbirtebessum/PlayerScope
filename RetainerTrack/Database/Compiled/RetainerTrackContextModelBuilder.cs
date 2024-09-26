﻿// <auto-generated />
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

#pragma warning disable 219, 612, 618
#nullable disable

namespace RetainerTrackExpanded.Database.Compiled
{
    public partial class RetainerTrackContextModel
    {
        partial void Initialize()
        {
            var player = PlayerEntityType.Create(this);
            var retainer = RetainerEntityType.Create(this);

            PlayerEntityType.CreateAnnotations(player);
            RetainerEntityType.CreateAnnotations(retainer);

            AddAnnotation("ProductVersion", "8.0.5");
            AddRuntimeAnnotation("Relational:RelationalModel", CreateRelationalModel());
        }

        private IRelationalModel CreateRelationalModel()
        {
            var relationalModel = new RelationalModel(this);

            var player = FindEntityType("RetainerTrack.Database.Player")!;

            var defaultTableMappings = new List<TableMappingBase<ColumnMappingBase>>();
            player.SetRuntimeAnnotation("Relational:DefaultMappings", defaultTableMappings);
            var retainerTrackDatabasePlayerTableBase = new TableBase("RetainerTrack.Database.Player", null, relationalModel);
            var accountIdColumnBase = new ColumnBase<ColumnMappingBase>("AccountId", "INTEGER", retainerTrackDatabasePlayerTableBase)
            {
                IsNullable = true
            };
            retainerTrackDatabasePlayerTableBase.Columns.Add("AccountId", accountIdColumnBase);
            var localContentIdColumnBase = new ColumnBase<ColumnMappingBase>("LocalContentId", "INTEGER", retainerTrackDatabasePlayerTableBase);
            retainerTrackDatabasePlayerTableBase.Columns.Add("LocalContentId", localContentIdColumnBase);
            var nameColumnBase = new ColumnBase<ColumnMappingBase>("Name", "TEXT", retainerTrackDatabasePlayerTableBase);
            retainerTrackDatabasePlayerTableBase.Columns.Add("Name", nameColumnBase);
            relationalModel.DefaultTables.Add("RetainerTrack.Database.Player", retainerTrackDatabasePlayerTableBase);
            var retainerTrackDatabasePlayerMappingBase = new TableMappingBase<ColumnMappingBase>(player, retainerTrackDatabasePlayerTableBase, true);
            retainerTrackDatabasePlayerTableBase.AddTypeMapping(retainerTrackDatabasePlayerMappingBase, false);
            defaultTableMappings.Add(retainerTrackDatabasePlayerMappingBase);
            RelationalModel.CreateColumnMapping((ColumnBase<ColumnMappingBase>)localContentIdColumnBase, player.FindProperty("LocalContentId")!, retainerTrackDatabasePlayerMappingBase);
            RelationalModel.CreateColumnMapping((ColumnBase<ColumnMappingBase>)accountIdColumnBase, player.FindProperty("AccountId")!, retainerTrackDatabasePlayerMappingBase);
            RelationalModel.CreateColumnMapping((ColumnBase<ColumnMappingBase>)nameColumnBase, player.FindProperty("Name")!, retainerTrackDatabasePlayerMappingBase);

            var tableMappings = new List<TableMapping>();
            player.SetRuntimeAnnotation("Relational:TableMappings", tableMappings);
            var playersTable = new Table("Players", null, relationalModel);
            var localContentIdColumn = new Column("LocalContentId", "INTEGER", playersTable);
            playersTable.Columns.Add("LocalContentId", localContentIdColumn);
            var accountIdColumn = new Column("AccountId", "INTEGER", playersTable)
            {
                IsNullable = true
            };
            playersTable.Columns.Add("AccountId", accountIdColumn);
            var nameColumn = new Column("Name", "TEXT", playersTable);
            playersTable.Columns.Add("Name", nameColumn);
            var pK_Players = new UniqueConstraint("PK_Players", playersTable, new[] { localContentIdColumn });
            playersTable.PrimaryKey = pK_Players;
            var pK_PlayersUc = RelationalModel.GetKey(this,
                "RetainerTrack.Database.Player",
                new[] { "LocalContentId" });
            pK_Players.MappedKeys.Add(pK_PlayersUc);
            RelationalModel.GetOrCreateUniqueConstraints(pK_PlayersUc).Add(pK_Players);
            playersTable.UniqueConstraints.Add("PK_Players", pK_Players);
            relationalModel.Tables.Add(("Players", null), playersTable);
            var playersTableMapping = new TableMapping(player, playersTable, true);
            playersTable.AddTypeMapping(playersTableMapping, false);
            tableMappings.Add(playersTableMapping);
            RelationalModel.CreateColumnMapping(localContentIdColumn, player.FindProperty("LocalContentId")!, playersTableMapping);
            RelationalModel.CreateColumnMapping(accountIdColumn, player.FindProperty("AccountId")!, playersTableMapping);
            RelationalModel.CreateColumnMapping(nameColumn, player.FindProperty("Name")!, playersTableMapping);

            var retainer = FindEntityType("RetainerTrack.Database.Retainer")!;

            var defaultTableMappings0 = new List<TableMappingBase<ColumnMappingBase>>();
            retainer.SetRuntimeAnnotation("Relational:DefaultMappings", defaultTableMappings0);
            var retainerTrackDatabaseRetainerTableBase = new TableBase("RetainerTrack.Database.Retainer", null, relationalModel);
            var localContentIdColumnBase0 = new ColumnBase<ColumnMappingBase>("LocalContentId", "INTEGER", retainerTrackDatabaseRetainerTableBase);
            retainerTrackDatabaseRetainerTableBase.Columns.Add("LocalContentId", localContentIdColumnBase0);
            var nameColumnBase0 = new ColumnBase<ColumnMappingBase>("Name", "TEXT", retainerTrackDatabaseRetainerTableBase);
            retainerTrackDatabaseRetainerTableBase.Columns.Add("Name", nameColumnBase0);
            var ownerLocalContentIdColumnBase = new ColumnBase<ColumnMappingBase>("OwnerLocalContentId", "INTEGER", retainerTrackDatabaseRetainerTableBase);
            retainerTrackDatabaseRetainerTableBase.Columns.Add("OwnerLocalContentId", ownerLocalContentIdColumnBase);
            var worldIdColumnBase = new ColumnBase<ColumnMappingBase>("WorldId", "INTEGER", retainerTrackDatabaseRetainerTableBase);
            retainerTrackDatabaseRetainerTableBase.Columns.Add("WorldId", worldIdColumnBase);
            relationalModel.DefaultTables.Add("RetainerTrack.Database.Retainer", retainerTrackDatabaseRetainerTableBase);
            var retainerTrackDatabaseRetainerMappingBase = new TableMappingBase<ColumnMappingBase>(retainer, retainerTrackDatabaseRetainerTableBase, true);
            retainerTrackDatabaseRetainerTableBase.AddTypeMapping(retainerTrackDatabaseRetainerMappingBase, false);
            defaultTableMappings0.Add(retainerTrackDatabaseRetainerMappingBase);
            RelationalModel.CreateColumnMapping((ColumnBase<ColumnMappingBase>)localContentIdColumnBase0, retainer.FindProperty("LocalContentId")!, retainerTrackDatabaseRetainerMappingBase);
            RelationalModel.CreateColumnMapping((ColumnBase<ColumnMappingBase>)nameColumnBase0, retainer.FindProperty("Name")!, retainerTrackDatabaseRetainerMappingBase);
            RelationalModel.CreateColumnMapping((ColumnBase<ColumnMappingBase>)ownerLocalContentIdColumnBase, retainer.FindProperty("OwnerLocalContentId")!, retainerTrackDatabaseRetainerMappingBase);
            RelationalModel.CreateColumnMapping((ColumnBase<ColumnMappingBase>)worldIdColumnBase, retainer.FindProperty("WorldId")!, retainerTrackDatabaseRetainerMappingBase);

            var tableMappings0 = new List<TableMapping>();
            retainer.SetRuntimeAnnotation("Relational:TableMappings", tableMappings0);
            var retainersTable = new Table("Retainers", null, relationalModel);
            var localContentIdColumn0 = new Column("LocalContentId", "INTEGER", retainersTable);
            retainersTable.Columns.Add("LocalContentId", localContentIdColumn0);
            var nameColumn0 = new Column("Name", "TEXT", retainersTable);
            retainersTable.Columns.Add("Name", nameColumn0);
            var ownerLocalContentIdColumn = new Column("OwnerLocalContentId", "INTEGER", retainersTable);
            retainersTable.Columns.Add("OwnerLocalContentId", ownerLocalContentIdColumn);
            var worldIdColumn = new Column("WorldId", "INTEGER", retainersTable);
            retainersTable.Columns.Add("WorldId", worldIdColumn);
            var pK_Retainers = new UniqueConstraint("PK_Retainers", retainersTable, new[] { localContentIdColumn0 });
            retainersTable.PrimaryKey = pK_Retainers;
            var pK_RetainersUc = RelationalModel.GetKey(this,
                "RetainerTrack.Database.Retainer",
                new[] { "LocalContentId" });
            pK_Retainers.MappedKeys.Add(pK_RetainersUc);
            RelationalModel.GetOrCreateUniqueConstraints(pK_RetainersUc).Add(pK_Retainers);
            retainersTable.UniqueConstraints.Add("PK_Retainers", pK_Retainers);
            relationalModel.Tables.Add(("Retainers", null), retainersTable);
            var retainersTableMapping = new TableMapping(retainer, retainersTable, true);
            retainersTable.AddTypeMapping(retainersTableMapping, false);
            tableMappings0.Add(retainersTableMapping);
            RelationalModel.CreateColumnMapping(localContentIdColumn0, retainer.FindProperty("LocalContentId")!, retainersTableMapping);
            RelationalModel.CreateColumnMapping(nameColumn0, retainer.FindProperty("Name")!, retainersTableMapping);
            RelationalModel.CreateColumnMapping(ownerLocalContentIdColumn, retainer.FindProperty("OwnerLocalContentId")!, retainersTableMapping);
            RelationalModel.CreateColumnMapping(worldIdColumn, retainer.FindProperty("WorldId")!, retainersTableMapping);
            return relationalModel.MakeReadOnly();
        }
    }
}