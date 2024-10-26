using Dalamud.Extensions.MicrosoftLogging;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlayerScope.API;
using PlayerScope.Database;
using PlayerScope.GUI;
using PlayerScope.Handlers;
using PlayerScope.Properties;
using System;
using System.Globalization;
using System.IO;
using System.Net.Http;

namespace PlayerScope;

public sealed class PlayerScopePlugin : IDalamudPlugin
{
    public const string DatabaseFileName = "PlayerScope.data.sqlite3";
    private readonly string _sqliteConnectionString;
    public static ServiceProvider? _serviceProvider;
    private readonly ICommandManager _commandManager;
    internal static IContextMenu _contextMenu { get; set; } = null!;
    internal static IDataManager _dataManager { get; set; } = null!;
    internal static IGameGui _gameGui { get; set; } = null!;
    internal static PlayerScopePlugin Instance { get; private set; } = null!;
    public Configuration Configuration { get; }
    public ApiClient ApiClient { get; set; }
    public GUI.SettingsWindow ConfigWindow;
    public GUI.MainWindow MainWindow;
    public GUI.DetailsWindow DetailsWindow;
    internal WindowSystem ws;
    internal IDalamudPluginInterface _pluginInterface {  get; }
    public PlayerScopePlugin(
        IDalamudPluginInterface pluginInterface,
        IFramework framework,
        IClientState clientState,
        IGameGui gameGui,
        IChatGui chatGui,
        IGameInteropProvider gameInteropProvider,
        IAddonLifecycle addonLifecycle,
        ICommandManager commandManager,
        IDataManager dataManager,
        ITargetManager targetManager,
        IObjectTable objectTable,
        IMarketBoard marketBoard,
        IPluginLog pluginLog,
        IContextMenu contextMenu)
    {
        Instance = this;
        ServiceCollection serviceCollection = new();
        serviceCollection.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace)
            .ClearProviders()
            .AddDalamudLogger(pluginLog)
            .AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning));
        serviceCollection.AddSingleton<IDalamudPlugin>(this);
        serviceCollection.AddSingleton(pluginInterface);
        serviceCollection.AddSingleton(framework);
        serviceCollection.AddSingleton(clientState);
        serviceCollection.AddSingleton(gameGui);
        serviceCollection.AddSingleton(chatGui);
        serviceCollection.AddSingleton(gameInteropProvider);
        serviceCollection.AddSingleton(addonLifecycle);
        serviceCollection.AddSingleton(commandManager);
        serviceCollection.AddSingleton(dataManager);
        serviceCollection.AddSingleton(targetManager);
        serviceCollection.AddSingleton(objectTable);
        serviceCollection.AddSingleton(marketBoard);

        serviceCollection.AddSingleton<PersistenceContext>();
        serviceCollection.AddSingleton<MarketBoardOfferingsHandler>();
        serviceCollection.AddSingleton<MarketBoardUiHandler>();
        serviceCollection.AddSingleton<ObjectTableHandler>();
        serviceCollection.AddSingleton<GameHooks>();

        Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        if (string.IsNullOrWhiteSpace(Configuration.Language.ToString()))
        {
            if (CultureInfo.InstalledUICulture.TwoLetterISOLanguageName == "tr")
            { Loc.Culture = new CultureInfo("tr"); Configuration.Language = Configuration.LanguageEnum.tr; }
            else
            { Loc.Culture = new CultureInfo("en"); Configuration.Language = Configuration.LanguageEnum.en; }
            pluginInterface.SavePluginConfig(Configuration);
        }
        else { Loc.Culture = new CultureInfo(Configuration.Language.ToString()); }

        if (Configuration.FreshInstall && string.IsNullOrWhiteSpace(Configuration.Key))
        {
            Configuration.FreshInstall = false; Configuration.Key = Util.GenerateRandomKey();
            pluginInterface.SavePluginConfig(Configuration);
        }

        ApiClient = new ApiClient();

        _pluginInterface = pluginInterface;
        _commandManager = commandManager;
        _contextMenu = contextMenu;
        Handlers.ContextMenu.Enable();
        _dataManager = dataManager;
        _gameGui = gameGui;

        ws = new();
        MainWindow = new();
        DetailsWindow = new();
        ConfigWindow = new();
        ws.AddWindow(MainWindow);
        ws.AddWindow(DetailsWindow);
        ws.AddWindow(ConfigWindow);

        pluginInterface.UiBuilder.Draw += ws.Draw;

        pluginInterface.UiBuilder.OpenMainUi += delegate { MainWindow.IsOpen = true; };
        pluginInterface.UiBuilder.OpenConfigUi += ConfigWindow.Toggle;

        _commandManager.AddHandler("/ps", new CommandInfo(ProcessCommand)
        {
            HelpMessage = Loc.CmOpenUI
        });

        _sqliteConnectionString = PrepareSqliteDb(serviceCollection, pluginInterface.GetPluginConfigDirectory());
        _serviceProvider = serviceCollection.BuildServiceProvider();

        RunMigrations(_serviceProvider);
        InitializeRequiredServices(_serviceProvider);
    }
    private void ProcessCommand(string command, string arguments)
    {
        if (command == "/ps")
        {
            MainWindow.IsOpen = true;
        }
    }

    private static string PrepareSqliteDb(IServiceCollection serviceCollection, string getPluginConfigDirectory)
    {
        string connectionString = $"Data Source={Path.Join(getPluginConfigDirectory, DatabaseFileName)}";
        serviceCollection.AddDbContext<RetainerTrackContext>(o => o
            .UseSqlite(connectionString));
            //.UseModel(RetainerTrackContextModel.Instance));
        return connectionString;
    }

    private static void RunMigrations(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<RetainerTrackContext>();
        dbContext.Database.Migrate();
    }

    private static void InitializeRequiredServices(ServiceProvider serviceProvider)
    {
        serviceProvider.GetRequiredService<MarketBoardOfferingsHandler>();
        serviceProvider.GetRequiredService<MarketBoardUiHandler>();
        serviceProvider.GetRequiredService<ObjectTableHandler>();
        serviceProvider.GetRequiredService<GameHooks>();
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
        Handlers.ContextMenu.Disable();
        // ensure we're not keeping the file open longer than the plugin is loaded
        using (SqliteConnection sqliteConnection = new(_sqliteConnectionString))
            SqliteConnection.ClearPool(sqliteConnection);
    }
}