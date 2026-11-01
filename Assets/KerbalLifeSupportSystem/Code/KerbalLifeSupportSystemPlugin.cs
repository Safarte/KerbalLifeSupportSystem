using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using I2.Loc;
using JetBrains.Annotations;
using KerbalLifeSupportSystem.UI;
using Redux.ExtraModTypes;
using SpaceWarp2.API.Configuration;
using SpaceWarp2.API.Configuration.Attributes;
using SpaceWarp2.UI.API.Appbar;
using UitkForKsp2.API;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace KerbalLifeSupportSystem
{
    /* Extend KerbalMod instead if you need the MonoBehaviour update loop/references to game stuff like SW 1.x mods */
    public class KerbalLifeSupportSystemPlugin : KerbalMod
    {
        [PublicAPI] public const string ModGuid = "KerbalLifeSupportSystem";
        [PublicAPI] public const string ModName = "Kerbal Life-Support System";

        [PublicAPI] public static KerbalLifeSupportSystemPlugin Instance { get; set; }

        // Life-Support constants (TODO: refactor into something more modular, maybe C# PM stuff?)
        private const int SecondsPerDay = 21600;
        private const int SecondsPerHour = 3600;

        public string[] LsInputResources { get; } = { "Food", "Water", "Oxygen" };

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
                { "Food", new[] { "KLSS_Greenhouse", "KLSS_GreenhouseFertilized" } },
                { "Water", new[] { "KLSS_WaterRecycler", "KLSS_Combined" } },
                { "Oxygen", new[] { "KLSS_CO2Scrubber", "KLSS_Combined" } }
            };

        // Mod settings
        [ConfigSection("Life-Support")]
        [ConfigValue("Kerbals Die",
            "Kerbals die when running out of life-support supplies. They go on strike and stop working otherwise.")]
        public readonly bool KerbalsDie = false;

        [ConfigValue("Consumption Multiplier", "Consumption rate multiplier for Food, Water, and Oxygen.")]
        public readonly ConfigValue<float> Multiplier = new ConfigDescription<float>(1f, new RangeConstraint<float>(0f, 5f));
        
        // UI assets
        private const string LsMonitorUxmlAddress = "Assets/KerbalLifeSupportSystem/UI/LifeSupportMonitor.uxml";
        private const string IconAddress = "Assets/KerbalLifeSupportSystem/UI/icon.png";
        
        private Texture2D _appbarIcon;
        private LifeSupportMonitorUIController _lsMonitorUIController;

        // AppBar button IDs
        public const string ToolbarFlightButtonID = "BTN-KLSSMonitor";
        public const string ToolbarOabButtonID = "BTN-KLSSPlanner";

        public override void OnPreInitialized()
        {
            SWConfiguration.Bind(this);
            CreateHarmonyAndPatchAll();
        }

        public override async void OnInitialized()
        {
            Instance = this;

            var lsMonitorUxmlHandle = Assets.LoadAssetAsync<VisualTreeAsset>(LsMonitorUxmlAddress);
            var appbarIconHandle = Assets.LoadAssetAsync<Texture2D>(IconAddress);

            try
            {
                await Task.WhenAll(lsMonitorUxmlHandle.Task, appbarIconHandle.Task);
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"{ModName}: An exception occurred while loading UI assets."
                );
                Debug.LogException(e);
                return;
            }

            if (lsMonitorUxmlHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError(
                    $"{ModName}: Failed to load the window UXML asset. Status: {lsMonitorUxmlHandle.Status}. Exception: {lsMonitorUxmlHandle.OperationException}"
                );
                return;
            }
            var lsMonitorUxml = lsMonitorUxmlHandle.Result;
            var windowOptions = new WindowOptions
            {
                // The ID of the window. It should be unique to your mod.
                WindowId = "KerbalLifeSupportSystem_LSMonitor",
                // The transform of parent game object of the window.
                // If null, it will be created under the main canvas.
                Parent = null,
                // Whether the window can be hidden with F2.
                IsHidingEnabled = true,
                // Whether the UI will use the "Scale" option in the settings.
                UseStockScale = true,
                // Whether to disable game input when typing into text fields.
                DisableGameInputForTextFields = true,
                MoveOptions = new MoveOptions
                {
                    // Whether the window can be moved by dragging.
                    IsMovingEnabled = true,
                    // Whether the window can only be moved within the screen bounds.
                    CheckScreenBounds = true
                }
            };
            var lsMonitor = Window.Create(windowOptions, lsMonitorUxml);
            _lsMonitorUIController = lsMonitor.gameObject.AddComponent<LifeSupportMonitorUIController>();

            if (appbarIconHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError(
                    $"{ModName}: Failed to load the AppBar icon. Status: {appbarIconHandle.Status}. Exception: {appbarIconHandle.OperationException}"
                );
                return;
            }
            _appbarIcon = appbarIconHandle.Result;
        }

        public override void OnCampaignLoaded()
        {
            base.OnCampaignLoaded();
            
            Appbar.RegisterOABAppButton(
                new LocalizedString("KLSS/UI/AppBar/Title"),
                ToolbarOabButtonID,
                _appbarIcon,
                isOpen => _lsMonitorUIController.IsWindowOpen = isOpen
            );
            Appbar.RegisterAppButton(
                new LocalizedString("KLSS/UI/AppBar/Title"),
                ToolbarFlightButtonID,
                _appbarIcon,
                isOpen => _lsMonitorUIController.IsWindowOpen = isOpen
            );
        }

        public override void OnCampaignUnloading()
        {
            base.OnCampaignUnloading();
            
            Appbar.Unregister(ToolbarOabButtonID);
            Appbar.Unregister(ToolbarFlightButtonID);
        }
    }
}