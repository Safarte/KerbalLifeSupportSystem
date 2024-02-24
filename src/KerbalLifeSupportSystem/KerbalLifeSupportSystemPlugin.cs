using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using I2.Loc;
using JetBrains.Annotations;
using KerbalLifeSupportSystem.Modules;
using KerbalLifeSupportSystem.UI;
using KSP.Sim.impl;
using SpaceWarp;
using SpaceWarp.API.Assets;
using SpaceWarp.API.Mods;
using SpaceWarp.API.Parts;
using SpaceWarp.API.UI.Appbar;
using UitkForKsp2;
using UitkForKsp2.API;
using UnityEngine;
using UnityEngine.UIElements;

namespace KerbalLifeSupportSystem;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(SpaceWarpPlugin.ModGuid, SpaceWarpPlugin.ModVer)]
[BepInDependency(UitkForKsp2Plugin.ModGuid, UitkForKsp2Plugin.ModVer)]
public class KerbalLifeSupportSystemPlugin : BaseSpaceWarpPlugin
{
    // These are useful in case some other mod wants to add a dependency to this one
    [PublicAPI] public const string ModGuid = MyPluginInfo.PLUGIN_GUID;
    [PublicAPI] public const string ModName = MyPluginInfo.PLUGIN_NAME;
    [PublicAPI] public const string ModVer = MyPluginInfo.PLUGIN_VERSION;

    /// Singleton instance of the plugin class
    [PublicAPI]
    public static KerbalLifeSupportSystemPlugin Instance { get; set; }

    // AppBar button IDs
    public const string ToolbarFlightButtonID = "BTN-KLSSMonitor";
    public const string ToolbarOabButtonID = "BTN-KLSSPlanner";

    private const int SecondsPerDay = 21600;
    private const int SecondsPerHour = 3600;

    internal ConfigEntry<bool> ConfigKerbalsDie;
    internal readonly Dictionary<string, ConfigEntry<float>> ConsumptionRates = new();

    public string[] LsInputResources { get; set; } = ["Food", "Water", "Oxygen"];

    public Dictionary<string, double> LsConsumptionRates { get; set; } = new()
    {
        { "Food", 0.001 / SecondsPerDay },
        { "Water", 3.0 / SecondsPerDay },
        { "Oxygen", 0.001 / SecondsPerDay }
    };

    public Dictionary<string, double> LsGracePeriods { get; set; } = new()
    {
        { "Food", 30 * SecondsPerDay },
        { "Water", 3 * SecondsPerDay },
        { "Oxygen", 2 * SecondsPerHour }
    };

    public Dictionary<string, string> LsOutputInputNames { get; set; } = new()
    {
        { "Waste", "Food" },
        { "WasteWater", "Water" },
        { "CarbonDioxide", "Oxygen" }
    };

    public Dictionary<string, string[]> LsRecyclerNames { get; set; } =
        new()
        {
            { "Food", ["KLSS_Greenhouse", "KLSS_GreenhouseFertilized"] },
            { "Water", ["KLSS_WaterRecycler", "KLSS_Combined"] },
            { "Oxygen", ["KLSS_CO2Scrubber", "KLSS_Combined"] }
        };


    // Logger
    public new static ManualLogSource Logger { get; private set; }

    private void Awake()
    {
        // Make sure the life-support consumer and the recyclers are working in the background
        PartComponentModuleOverride
            .RegisterModuleForBackgroundResourceProcessing<PartComponentModule_LifeSupportConsumer>();
        PartComponentModuleOverride
            .RegisterModuleForBackgroundResourceProcessing<PartComponentModule_ResourceConverter>();
    }

    /// <summary>
    ///     Runs when the mod is first initialized.
    /// </summary>
    public override void OnInitialized()
    {
        base.OnInitialized();

        Logger = base.Logger;
        Instance = this;

        LoadAssemblies();

        // Load the UI from the asset bundle
        var lsMonitorUxml = AssetManager.GetAsset<VisualTreeAsset>($"{ModGuid}/klss_ui/ui/lifesupportmonitor.uxml");

        // Create the window options object
        var windowOptions = new WindowOptions
        {
            // The ID of the window. It should be unique to your mod.
            WindowId = "KerbalLifeSupportSystem_LSMonitor",
            // The transform of parent game object of the window.
            // If null, it will be created under the main canvas.
            Parent = null,
            // Whether or not the window can be hidden with F2.
            IsHidingEnabled = true,
            // Whether to disable game input when typing into text fields.
            DisableGameInputForTextFields = true,
            MoveOptions = new MoveOptions
            {
                // Whether or not the window can be moved by dragging.
                IsMovingEnabled = true,
                // Whether or not the window can only be moved within the screen bounds.
                CheckScreenBounds = true
            }
        };

        // Create the window
        var lsMonitorWindow = Window.Create(windowOptions, lsMonitorUxml);
        // Add a controller for the UI to the window's game object
        var lsMonitorController = lsMonitorWindow.gameObject.AddComponent<LifeSupportMonitorUIController>();
        lsMonitorController.IsWindowOpen = false;

        Appbar.RegisterOABAppButton(
            new LocalizedString("KLSS/UI/AppBar/Title"),
            ToolbarOabButtonID,
            AssetManager.GetAsset<Texture2D>($"{ModGuid}/images/icon.png"),
            isOpen => lsMonitorController.IsWindowOpen = isOpen);

        Appbar.RegisterAppButton(
            new LocalizedString("KLSS/UI/AppBar/Title"),
            ToolbarFlightButtonID,
            AssetManager.GetAsset<Texture2D>($"{ModGuid}/images/icon.png"),
            isOpen => lsMonitorController.IsWindowOpen = isOpen);
    }

    public override void OnPostInitialized()
    {
        SetupConfiguration();
    }

    private void SetupConfiguration()
    {
        ConfigKerbalsDie = Config.Bind("Life-Support", "Kerbals Die", true,
            new ConfigDescription(
                "Kerbals die when running out of life-support supplies. They go on strike and stop working otherwise."));

        foreach (var resource in LsInputResources)
            ConsumptionRates[resource] = Config.Bind("Life-Support", $"{resource} Consumption Multiplier", 1f,
                new ConfigDescription(
                    $"{resource} consumption rate multiplier.", new AcceptableValueRange<float>(0f, 5f)));
    }

    /// <summary>
    /// Loads all the assemblies for the mod.
    /// </summary>
    private static void LoadAssemblies()
    {
        // Load the Unity project assembly
        var currentFolder = new FileInfo(Assembly.GetExecutingAssembly().Location).Directory!.FullName;
        var unityAssembly = Assembly.LoadFrom(Path.Combine(currentFolder, "KerbalLifeSupportSystem.Unity.dll"));
        // Register any custom UI controls from the loaded assembly
        CustomControls.RegisterFromAssembly(unityAssembly);
    }
}