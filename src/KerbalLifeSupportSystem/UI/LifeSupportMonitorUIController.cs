using KSP.Game;
using KSP.Messages;
using KSP.Modules;
using KSP.OAB;
using KSP.Sim.impl;
using KSP.Sim.ResourceSystem;
using KSP.UI.Binding;
using UitkForKsp2.API;
using UnityEngine;
using UnityEngine.UIElements;
using static KerbalLifeSupportSystem.UI.LifeSupportEntryControl;

namespace KerbalLifeSupportSystem.UI;

internal class LifeSupportMonitorUIController : KerbalMonoBehaviour
{
    private const double SecondsPerDay = 21600;
    private const double FoodPerSecond = 0.001 / SecondsPerDay;
    private const double WaterPerSecond = 3.0 / SecondsPerDay;
    private const double OxygenPerSecond = 0.001 / SecondsPerDay;
    private static VisualElement S_CONTAINER;
    private static bool S_INITIALIZED;
    private readonly Dictionary<IGGuid, LifeSupportEntryControl> _lifeSupportEntries = new();
    private Button _closeButton;
    private ResourceDefinitionID _foodResourceId;
    private bool _isLsEntriesDirty;
    private ScrollView _lifeSupportEntriesView;
    private ResourceDefinitionID _oxygenResourceId;

    private bool _uiEnabled;
    private ResourceDefinitionID _waterResourceId;

    private void Start()
    {
        SetupDocument();
        InitMessages();
    }

    private void Update()
    {
        if (!S_INITIALIZED) InitElements();

        if (!_uiEnabled || GameManager.Instance?.Game?.ViewController?.Universe is null) return;

        if (_isLsEntriesDirty)
        {
            _isLsEntriesDirty = false;
            PopulateLsEntries();
        }

        if (GameManager.Instance?.Game?.OAB?.Current?.Stats?.MainAssembly is not null)
        {
            var assembly = Game.OAB.Current.Stats.MainAssembly;

            if (!_lifeSupportEntries.ContainsKey(assembly.Anchor.UniqueId)) PopulateLsEntries();

            _lifeSupportEntries[assembly.Anchor.UniqueId].SetValues(GetObjectAssemblyData(assembly), true);
        }

        foreach (var entry in _lifeSupportEntries)
            if (GameManager.Instance?.Game?.OAB?.Current?.Stats?.MainAssembly is null ||
                entry.Key != Game.OAB.Current.Stats.MainAssembly.Anchor.UniqueId)
            {
                var vessel = Game.ViewController.Universe.FindVesselComponent(entry.Key);
                if (vessel != null)
                {
                    var isActiveVessel = Game.ViewController.IsActiveVessel(vessel);
                    entry.Value.SetValues(GetVesselData(vessel), isActiveVessel);
                }
                else
                {
                    _isLsEntriesDirty = true;
                }
            }
    }

    private void OnDestroy()
    {
        if (IsGameShuttingDown)
            return;

        Game.Messages.Unsubscribe<GameLoadFinishedMessage>(OnLSEntriesDirtyingEvent);
        Game.Messages.Unsubscribe<VesselCreatedMessage>(OnLSEntriesDirtyingEvent);
        Game.Messages.Unsubscribe<VesselLaunchedMessage>(OnLSEntriesDirtyingEvent);
        Game.Messages.Unsubscribe<VesselDestroyedMessage>(OnLSEntriesDirtyingEvent);
        Game.Messages.Unsubscribe<VesselRecoveredMessage>(OnLSEntriesDirtyingEvent);
        Game.Messages.Unsubscribe<VesselSplitMessage>(OnLSEntriesDirtyingEvent);
        Game.Messages.Unsubscribe<VesselChangedMessage>(OnLSEntriesDirtyingEvent);
        Game.Messages.Unsubscribe<OABNewAssemblyMessage>(OnLSEntriesDirtyingEvent);
        Game.Messages.Unsubscribe<SubassemblyLoadedMessage>(OnLSEntriesDirtyingEvent);
        Game.Messages.Unsubscribe<WorkspaceLoadedMessage>(OnLSEntriesDirtyingEvent);
        Game.Messages.Unsubscribe<AddVesselToMapMessage>(OnLSEntriesDirtyingEvent);
    }

    private void InitMessages()
    {
        Game.Messages.PersistentSubscribe<GameLoadFinishedMessage>(OnLSEntriesDirtyingEvent);
        Game.Messages.PersistentSubscribe<VesselCreatedMessage>(OnLSEntriesDirtyingEvent);
        Game.Messages.PersistentSubscribe<VesselLaunchedMessage>(OnLSEntriesDirtyingEvent);
        Game.Messages.PersistentSubscribe<VesselDestroyedMessage>(OnLSEntriesDirtyingEvent);
        Game.Messages.PersistentSubscribe<VesselRecoveredMessage>(OnLSEntriesDirtyingEvent);
        Game.Messages.PersistentSubscribe<VesselSplitMessage>(OnLSEntriesDirtyingEvent);
        Game.Messages.PersistentSubscribe<VesselChangedMessage>(OnLSEntriesDirtyingEvent);
        Game.Messages.PersistentSubscribe<OABNewAssemblyMessage>(OnLSEntriesDirtyingEvent);
        Game.Messages.PersistentSubscribe<SubassemblyLoadedMessage>(OnLSEntriesDirtyingEvent);
        Game.Messages.PersistentSubscribe<WorkspaceLoadedMessage>(OnLSEntriesDirtyingEvent);
        Game.Messages.PersistentSubscribe<AddVesselToMapMessage>(OnLSEntriesDirtyingEvent);
    }

    private LsEntryData GetVesselData(VesselComponent vessel)
    {
        LsEntryData data = new()
        {
            // Basic vessel information
            Id = vessel.GlobalId,
            VesselName = vessel.DisplayName,
            CurrentCrew = vessel.Game.SessionManager.KerbalRosterManager
                .GetAllKerbalsInVessel(vessel.SimulationObject.GlobalId).Count,
            MaximumCrew = vessel.SimulationObject.IsKerbal ? 1 : 0
        };

        foreach (var part in vessel.SimulationObject.PartOwner.Parts)
            data.MaximumCrew += !vessel.SimulationObject.IsKerbal ? part.PartData.crewCapacity : 0;

        // Consumption rate settings
        double foodRate = KerbalLifeSupportSystemPlugin.Instance.ConsumptionRates["Food"].Value;
        double waterRate = KerbalLifeSupportSystemPlugin.Instance.ConsumptionRates["Water"].Value;
        double oxygenRate = KerbalLifeSupportSystemPlugin.Instance.ConsumptionRates["Oxygen"].Value;

        // Base consumption rates
        var curFoodRate = FoodPerSecond * foodRate * data.CurrentCrew;
        var maxFoodRate = FoodPerSecond * foodRate * data.MaximumCrew;
        var curWaterRate = WaterPerSecond * waterRate * data.CurrentCrew;
        var maxWaterRate = WaterPerSecond * waterRate * data.MaximumCrew;
        var curOxygenRate = OxygenPerSecond * oxygenRate * data.CurrentCrew;
        var maxOxygenRate = OxygenPerSecond * oxygenRate * data.MaximumCrew;

        // Compute the current capacities of the recyclers on the vessel
        var foodRecyclerCapacity = 0.0;
        var waterRecyclerCapacity = 0.0;
        var oxygenRecyclerCapacity = 0.0;
        foreach (var part in vessel.SimulationObject.PartOwner.Parts)
            if (part.TryGetModuleData<PartComponentModule_ResourceConverter, Data_ResourceConverter>(out var converter))
                if (converter.ConverterIsActive)
                {
                    var def = converter.FormulaDefinitions[converter.SelectedFormula];
                    if (def.InternalName == "KLSS_Combined")
                    {
                        waterRecyclerCapacity += converter.conversionRate.GetValue() *
                                                 def.OutputResources.Find(res => res.ResourceName == "Water").Rate;
                        oxygenRecyclerCapacity += converter.conversionRate.GetValue() *
                                                  def.OutputResources.Find(res => res.ResourceName == "Oxygen").Rate;
                    }
                    else if (def.InternalName == "KLSS_CO2Scrubber")
                    {
                        oxygenRecyclerCapacity += converter.conversionRate.GetValue() *
                                                  def.OutputResources.Find(res => res.ResourceName == "Oxygen").Rate;
                    }
                    else if (def.InternalName == "KLSS_WaterRecycler")
                    {
                        waterRecyclerCapacity += converter.conversionRate.GetValue() *
                                                 def.OutputResources.Find(res => res.ResourceName == "Water").Rate;
                    }
                    else if (def.InternalName == "KLSS_Greenhouse" || def.InternalName == "KLSS_GreenhouseFertilized")
                    {
                        foodRecyclerCapacity += converter.conversionRate.GetValue() *
                                                def.OutputResources.Find(res => res.ResourceName == "Food").Rate;
                    }
                }

        curFoodRate -= Math.Min(foodRecyclerCapacity, curFoodRate);
        maxFoodRate -= Math.Min(foodRecyclerCapacity, maxFoodRate);
        curWaterRate -= Math.Min(waterRecyclerCapacity, curWaterRate);
        maxWaterRate -= Math.Min(waterRecyclerCapacity, maxWaterRate);
        curOxygenRate -= Math.Min(oxygenRecyclerCapacity, curOxygenRate);
        maxOxygenRate -= Math.Min(oxygenRecyclerCapacity, maxOxygenRate);

        // Get information about the resource containers
        _foodResourceId = Game.ResourceDefinitionDatabase.GetResourceIDFromName("Food");
        _waterResourceId = Game.ResourceDefinitionDatabase.GetResourceIDFromName("Water");
        _oxygenResourceId = Game.ResourceDefinitionDatabase.GetResourceIDFromName("Oxygen");
        var containerGroup = vessel.GetControlOwner().PartOwner.ContainerGroup;

        // Food
        var food = containerGroup.GetResourceStoredUnits(_foodResourceId);
        data.CurFood = food / curFoodRate;
        data.MaxFood = food / maxFoodRate;

        // Water
        var water = containerGroup.GetResourceStoredUnits(_waterResourceId);
        data.CurWater = water / curWaterRate;
        data.MaxWater = water / maxWaterRate;

        // Oxygen
        var oxygen = containerGroup.GetResourceStoredUnits(_oxygenResourceId);
        data.CurOxygen = oxygen / curOxygenRate;
        data.MaxOxygen = oxygen / maxOxygenRate;

        return data;
    }

    private LsEntryData GetObjectAssemblyData(IObjectAssembly assembly)
    {
        LsEntryData data = new()
        {
            // Basic vessel information
            Id = assembly.Anchor.UniqueId,
            VesselName = Game.OAB.Current.Stats.CurrentWorkspaceVehicleDisplayName.GetValue(),
            CurrentCrew = Game.SessionManager.KerbalRosterManager
                .GetAllKerbalsInAssembly(Game.SessionManager.KerbalRosterManager.KSCGuid, assembly).Count,
            MaximumCrew = 0
        };

        foreach (var part in assembly.Parts) data.MaximumCrew += part.AvailablePart.PartData.crewCapacity;

        // Consumption rate setting
        double foodRate = KerbalLifeSupportSystemPlugin.Instance.ConsumptionRates["Food"].Value;
        double waterRate = KerbalLifeSupportSystemPlugin.Instance.ConsumptionRates["Water"].Value;
        double oxygenRate = KerbalLifeSupportSystemPlugin.Instance.ConsumptionRates["Oxygen"].Value;

        // Base consumption rates
        var curFoodRate = FoodPerSecond * foodRate * data.CurrentCrew;
        var maxFoodRate = FoodPerSecond * foodRate * data.MaximumCrew;
        var curWaterRate = WaterPerSecond * waterRate * data.CurrentCrew;
        var maxWaterRate = WaterPerSecond * waterRate * data.MaximumCrew;
        var curOxygenRate = OxygenPerSecond * oxygenRate * data.CurrentCrew;
        var maxOxygenRate = OxygenPerSecond * oxygenRate * data.MaximumCrew;

        // Compute the current capacities of the recyclers on the vessel
        var foodRecyclerCapacity = 0.0;
        var waterRecyclerCapacity = 0.0;
        var oxygenRecyclerCapacity = 0.0;
        foreach (var part in assembly.Parts)
            if (part.TryGetModule(out Module_ResourceConverter module_converter))
            {
                var converter = module_converter._dataResourceConverter;
                if (converter.EnabledToggle.GetValue())
                {
                    var def = converter.FormulaDefinitions[converter.SelectedFormula];
                    if (def.InternalName == "KLSS_Combined")
                    {
                        waterRecyclerCapacity += converter.conversionRate.GetValue() *
                                                 def.OutputResources.Find(res => res.ResourceName == "Water").Rate;
                        oxygenRecyclerCapacity += converter.conversionRate.GetValue() *
                                                  def.OutputResources.Find(res => res.ResourceName == "Oxygen").Rate;
                    }
                    else if (def.InternalName == "KLSS_CO2Scrubber")
                    {
                        oxygenRecyclerCapacity += converter.conversionRate.GetValue() *
                                                  def.OutputResources.Find(res => res.ResourceName == "Oxygen").Rate;
                    }
                    else if (def.InternalName == "KLSS_WaterRecycler")
                    {
                        waterRecyclerCapacity += converter.conversionRate.GetValue() *
                                                 def.OutputResources.Find(res => res.ResourceName == "Water").Rate;
                    }
                    else if (def.InternalName == "KLSS_Greenhouse" || def.InternalName == "KLSS_GreenhouseFertilized")
                    {
                        foodRecyclerCapacity += converter.conversionRate.GetValue() *
                                                def.OutputResources.Find(res => res.ResourceName == "Food").Rate;
                    }
                }
            }

        curFoodRate -= Math.Min(foodRecyclerCapacity, curFoodRate);
        maxFoodRate -= Math.Min(foodRecyclerCapacity, maxFoodRate);
        curWaterRate -= Math.Min(waterRecyclerCapacity, curWaterRate);
        maxWaterRate -= Math.Min(waterRecyclerCapacity, maxWaterRate);
        curOxygenRate -= Math.Min(oxygenRecyclerCapacity, curOxygenRate);
        maxOxygenRate -= Math.Min(oxygenRecyclerCapacity, maxOxygenRate);

        // Food
        var food = 0.0;
        assembly.TryGetPartsWithResourceStored("Food", out var resourceParts);
        foreach (var part in resourceParts)
        foreach (var resource in part.Resources)
            if (resource.Name == "Food")
                food += resource.Count;
        data.CurFood = food / curFoodRate;
        data.MaxFood = food / maxFoodRate;

        // Water
        var water = 0.0;
        assembly.TryGetPartsWithResourceStored("Water", out resourceParts);
        foreach (var part in resourceParts)
        foreach (var resource in part.Resources)
            if (resource.Name == "Water")
                water += resource.Count;
        data.CurWater = water / curWaterRate;
        data.MaxWater = water / maxWaterRate;

        // Oxygen
        var oxygen = 0.0;
        assembly.TryGetPartsWithResourceStored("Oxygen", out resourceParts);
        foreach (var part in resourceParts)
        foreach (var resource in part.Resources)
            if (resource.Name == "Oxygen")
                oxygen += resource.Count;
        data.CurOxygen = oxygen / curOxygenRate;
        data.MaxOxygen = oxygen / maxOxygenRate;

        return data;
    }

    public void SetEnabled(bool newState)
    {
        _uiEnabled = newState;
        S_CONTAINER.style.display = newState ? DisplayStyle.Flex : DisplayStyle.None;
        GameObject.Find(KerbalLifeSupportSystemPlugin.ToolbarOabButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()
            ?.SetValue(newState);
        GameObject.Find(KerbalLifeSupportSystemPlugin.ToolbarFlightButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()
            ?.SetValue(newState);
    }

    private void SetupDocument()
    {
        var document = GetComponent<UIDocument>();

        // Set up localization
        if (document.TryGetComponent<DocumentLocalization>(out var localization))
            localization.Localize();
        else
            document.EnableLocalization();

        // root Visual Element
        S_CONTAINER = document.rootVisualElement;

        // Move the GUI to its starting position
        S_CONTAINER[0].transform.position = new Vector2(500, 50);
        S_CONTAINER[0].CenterByDefault();

        // Hide the GUI by default
        S_CONTAINER.style.display = DisplayStyle.None;
    }

    private void InitElements()
    {
        _closeButton = S_CONTAINER.Q<Button>("close-button");
        _closeButton.RegisterCallback<ClickEvent>(OnCloseButton);

        _lifeSupportEntriesView = S_CONTAINER.Q<ScrollView>("ls-entries-body");

        S_INITIALIZED = true;
    }

    private void OnCloseButton(ClickEvent evt)
    {
        SetEnabled(false);
    }

    private void PopulateLsEntries()
    {
        // Reset life-support UI entries
        _lifeSupportEntries.Clear();
        _lifeSupportEntriesView.Clear();

        // If in a OAB & we have a main assembly, add it to the entries
        if (GameManager.Instance?.Game?.OAB?.Current?.Stats?.MainAssembly != null)
        {
            var assembly = Game.OAB.Current.Stats.MainAssembly;
            _lifeSupportEntries[assembly.Anchor.UniqueId] =
                new LifeSupportEntryControl(GetObjectAssemblyData(assembly), true, true);
            _lifeSupportEntriesView.Add(_lifeSupportEntries[assembly.Anchor.UniqueId]);

            KerbalLifeSupportSystemPlugin.Logger.LogInfo("Added <" +
                                                         Game.OAB.Current.Stats.CurrentWorkspaceVehicleDisplayName
                                                             .GetValue() + "> to the Life-Support UI list.");
        }

        // Add an entry for each vessel owned by the player
        List<VesselComponent> vessels = new();
        Game.ViewController.Universe.GetAllOwnedVessels(Game.LocalPlayer.PlayerId, ref vessels);
        foreach (var vessel in vessels)
        {
            var simulationObject = vessel.SimulationObject;
            if (simulationObject is { IsKerbal: true } ||
                (simulationObject.IsVessel && vessel.TotalCommandCrewCapacity > 0))
            {
                var isActiveVessel = Game.ViewController.IsActiveVessel(simulationObject.Vessel);
                _lifeSupportEntries[simulationObject.GlobalId] =
                    new LifeSupportEntryControl(GetVesselData(simulationObject.Vessel), isActiveVessel, isActiveVessel);
                _lifeSupportEntriesView.Add(_lifeSupportEntries[simulationObject.GlobalId]);

                KerbalLifeSupportSystemPlugin.Logger.LogInfo("Added <" + simulationObject.Vessel.DisplayName +
                                                             "> to the Life-Support UI list.");
            }
        }

        _lifeSupportEntriesView.Sort(CompareLsEntries);
    }

    private void OnLSEntriesDirtyingEvent(MessageCenterMessage msg)
    {
        _isLsEntriesDirty = true;
    }

    private int CompareLsEntries(VisualElement e1, VisualElement e2)
    {
        if (e1 is not LifeSupportEntryControl ls1 || e2 is not LifeSupportEntryControl ls2)
            return 0;

        if (GameManager.Instance?.Game?.OAB?.Current?.Stats?.MainAssembly != null)
        {
            var id = Game.OAB.Current.Stats.MainAssembly.Anchor.UniqueId;
            if (ls1.Data.Id == id)
                return -1;
            if (ls2.Data.Id == id)
                return 1;
        }

        var vessel1 = Game.ViewController.Universe.FindVesselComponent(ls1.Data.Id);
        if (Game.ViewController.IsActiveVessel(vessel1))
            return -1;
        var vessel2 = Game.ViewController.Universe.FindVesselComponent(ls2.Data.Id);
        if (Game.ViewController.IsActiveVessel(vessel2))
            return 1;

        return string.CompareOrdinal(vessel1.DisplayName, vessel2.DisplayName);
    }
}