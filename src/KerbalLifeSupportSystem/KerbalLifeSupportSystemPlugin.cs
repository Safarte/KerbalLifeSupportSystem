using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using JetBrains.Annotations;
using KerbalLifeSupportSystem.UI;
using SpaceWarp;
using SpaceWarp.API.Assets;
using SpaceWarp.API.Mods;
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

    //internal ConfigEntry<bool> ConfigKerbalsDie;
    internal readonly Dictionary<string, ConfigEntry<float>> ConsumptionRates = new();

    // UI Controllers
    private LifeSupportMonitorUIController _klssMonitorController;

    // Singleton instance of the plugin class
    public static KerbalLifeSupportSystemPlugin Instance { get; private set; }

    // Logger
    public new static ManualLogSource Logger { get; private set; }

    public void Awake()
    {
        SetupConfiguration();
    }

    private void SetupConfiguration()
    {
        //ConfigKerbalsDie = Config.Bind("Life Support", "Kerbals Die", false, "Do Kerbals die when out of food/water/oxygen, go on strike otherwise");
        ConsumptionRates["Food"] = Config.Bind("Life-Support", "Food Consumption Multiplier (Base: 1kg/day)", 1f,
            new ConfigDescription("Food consumption rate multiplier.", new AcceptableValueRange<float>(0f, 5f)));
        ConsumptionRates["Water"] = Config.Bind("Life-Support", "Water Consumption Multiplier (Base: 3l/day)", 1f,
            new ConfigDescription("Water consumption rate multiplier.", new AcceptableValueRange<float>(0f, 5f)));
        ConsumptionRates["Oxygen"] = Config.Bind("Life-Support", "Oxygen Consumption Multiplier (Base: 1kg/day)", 1f,
            new ConfigDescription("Oxygen consumption rate multiplier.", new AcceptableValueRange<float>(0f, 5f)));
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
}