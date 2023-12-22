using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
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
    public const string ToolbarOabButtonID = "BTN-KLSSPlanner";
    public const string ToolbarFlightButtonID = "BTN-KLSSMonitor";

    private const int SecondsPerDay = 21600;
    private const int SecondsPerHour = 3600;

    //internal ConfigEntry<bool> ConfigKerbalsDie;
    internal readonly Dictionary<string, ConfigEntry<float>> ConsumptionRates = new();

    // UI Controllers
    private LifeSupportMonitorUIController _klssMonitorController;

    public string[] LsInputResources { get; set; } = { "Food", "Water", "Oxygen" };

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
            { "Food", new string[2] { "KLSS_Greenhouse", "KLSS_GreenhouseFertilized" } },
            { "Water", new string[2] { "KLSS_WaterRecycler", "KLSS_Combined" } },
            { "Oxygen", new string[2] { "KLSS_CO2Scrubber", "KLSS_Combined" } }
        };


    // Singleton instance of the plugin class
    public static KerbalLifeSupportSystemPlugin Instance { get; private set; }

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

    private void SetupConfiguration()
    {
        foreach (var resource in LsInputResources)
            ConsumptionRates[resource] = Config.Bind("Life-Support", $"{resource} Consumption Multiplier", 1f,
                new ConfigDescription(
                    $"{resource} consumption rate multiplier.", new AcceptableValueRange<float>(0f, 5f)));
    }

    /// <summary>
    ///     Runs when the mod is first initialized.
    /// </summary>
    public override void OnInitialized()
    {
        base.OnInitialized();

        Logger = base.Logger;
        Instance = this;

        var klssPlannerUxml =
            AssetManager.GetAsset<VisualTreeAsset>($"{Info.Metadata.GUID}/klss_ui/ui/lifesupportmonitor.uxml");
        var klssPlannerWindow = Window.CreateFromUxml(klssPlannerUxml, "Life-Support Monitor", transform, true);
        _klssMonitorController = klssPlannerWindow.gameObject.AddComponent<LifeSupportMonitorUIController>();

        Appbar.RegisterOABAppButton(
            "Life-Support",
            ToolbarOabButtonID,
            AssetManager.GetAsset<Texture2D>($"{Info.Metadata.GUID}/images/icon.png"),
            _klssMonitorController.SetEnabled);

        Appbar.RegisterAppButton(
            "Life-Support",
            ToolbarFlightButtonID,
            AssetManager.GetAsset<Texture2D>($"{Info.Metadata.GUID}/images/icon.png"),
            _klssMonitorController.SetEnabled);
    }

    public override void OnPostInitialized()
    {
        SetupConfiguration();
    }
}