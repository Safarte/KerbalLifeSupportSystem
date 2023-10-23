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
    private static VisualElement s_container;
    private static bool s_initialized = false;

    private static double SECONDS_PER_DAY = 21600;
    private static double FOOD_PER_SECOND = 0.001 / SECONDS_PER_DAY;
    private static double WATER_PER_SECOND = 3.0 / SECONDS_PER_DAY;
    private static double OXYGEN_PER_SECOND = 0.001 / SECONDS_PER_DAY;

    private bool _uiEnabled = false;
    private Button closeButton;
    private bool _isLSEntriesDirty = false;
    private Dictionary<IGGuid, LifeSupportEntryControl> lifeSupportEntries = new();
    private ScrollView lifeSupportEntriesView;
    private ResourceDefinitionID _foodResourceId;
    private ResourceDefinitionID _waterResourceId;
    private ResourceDefinitionID _oxygenResourceId;

    private void InitMessages()
    {
        Game.Messages.Subscribe<GameLoadFinishedMessage>(OnLSEntriesDirtyingEvent);
        Game.Messages.Subscribe<VesselCreatedMessage>(OnLSEntriesDirtyingEvent);
        Game.Messages.Subscribe<VesselLaunchedMessage>(OnLSEntriesDirtyingEvent);
        Game.Messages.Subscribe<VesselDestroyedMessage>(OnLSEntriesDirtyingEvent);
        Game.Messages.Subscribe<VesselRecoveredMessage>(OnLSEntriesDirtyingEvent);
        Game.Messages.Subscribe<VesselSplitMessage>(OnLSEntriesDirtyingEvent);
        Game.Messages.Subscribe<VesselChangedMessage>(OnLSEntriesDirtyingEvent);
        Game.Messages.Subscribe<OABNewAssemblyMessage>(OnLSEntriesDirtyingEvent);
        Game.Messages.Subscribe<SubassemblyLoadedMessage>(OnLSEntriesDirtyingEvent);
        Game.Messages.Subscribe<WorkspaceLoadedMessage>(OnLSEntriesDirtyingEvent);
        Game.Messages.Subscribe<AddVesselToMapMessage>(OnLSEntriesDirtyingEvent);
    }

    private void Start()
    {
        SetupDocument();
        InitMessages();
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

    private void Update()
    {
        if (!s_initialized)
        {
            InitElements();
        }

        if (!_uiEnabled || GameManager.Instance?.Game?.ViewController?.Universe is null)
        {
            return;
        }

        if (_isLSEntriesDirty)
        {
            _isLSEntriesDirty = false;
            PopulateLSEntries();
        }

        if (GameManager.Instance?.Game?.OAB?.Current?.Stats?.MainAssembly is not null)
        {
            IObjectAssembly assembly = Game.OAB.Current.Stats.MainAssembly;
            lifeSupportEntries[assembly.Anchor.UniqueId].SetValues(GetObjectAssemblyData(assembly), true);
        }

        foreach (var entry in lifeSupportEntries)
        {
            if (GameManager.Instance?.Game?.OAB?.Current?.Stats?.MainAssembly is null || entry.Key != Game.OAB.Current.Stats.MainAssembly.Anchor.UniqueId)
            {
                VesselComponent vessel = Game.ViewController.Universe.FindVesselComponent(entry.Key);
                if (vessel != null)
                {
                    bool isActiveVessel = Game.ViewController.IsActiveVessel(vessel);
                    entry.Value.SetValues(GetVesselData(vessel), isActiveVessel);
                }
            }
        }

        if (_uiEnabled && Input.GetKey(KeyCode.Escape))
        {
            SetEnabled(false);
        }
    }

    private LSEntryData GetVesselData(VesselComponent vessel)
    {
        LSEntryData data = new();

        // Basic vessel information
        data.Id = vessel.GlobalId;
        data.VesselName = vessel.DisplayName;
        data.CurrentCrew = vessel.Game.SessionManager.KerbalRosterManager.GetAllKerbalsInVessel(vessel.SimulationObject.GlobalId).Count;
        data.MaximumCrew = vessel.SimulationObject.IsKerbal ? 1 : vessel.TotalCommandCrewCapacity;

        // Consumption rate setting
        double consRate = KerbalLifeSupportSystemPlugin.Instance.ConfigResourceConsumptionRate.Value;

        // Base consumption rates
        double curFoodRate = FOOD_PER_SECOND * consRate * data.CurrentCrew;
        double maxFoodRate = FOOD_PER_SECOND * consRate * data.MaximumCrew;
        double curWaterRate = WATER_PER_SECOND * consRate * data.CurrentCrew;
        double maxWaterRate = WATER_PER_SECOND * consRate * data.MaximumCrew;
        double curOxygenRate = OXYGEN_PER_SECOND * consRate * data.CurrentCrew;
        double maxOxygenRate = OXYGEN_PER_SECOND * consRate * data.MaximumCrew;

        // Compute the current capacities of the recyclers on the vessel
        double foodRecyclerCapacity = 0.0;
        double waterRecyclerCapacity = 0.0;
        double oxygenRecyclerCapacity = 0.0;
        foreach (PartComponent part in vessel.SimulationObject.PartOwner.Parts)
        {
            if (part.TryGetModuleData<PartComponentModule_ResourceConverter, Data_ResourceConverter>(out Data_ResourceConverter converter))
            {
                if (converter.ConverterIsActive)
                {
                    ResourceConverterFormulaDefinition def = converter.FormulaDefinitions[converter.SelectedFormula];
                    if (def.InternalName == "KLSS_Combined")
                    {
                        waterRecyclerCapacity += converter.conversionRate.GetValue() * def.OutputResources.Find(res => res.ResourceName == "Water").Rate;
                        oxygenRecyclerCapacity += converter.conversionRate.GetValue() * def.OutputResources.Find(res => res.ResourceName == "Oxygen").Rate;
                    }
                    else if (def.InternalName == "KLSS_CO2Scrubber")
                    {
                        oxygenRecyclerCapacity += converter.conversionRate.GetValue() * def.OutputResources.Find(res => res.ResourceName == "Oxygen").Rate;
                    }
                    else if (def.InternalName == "KLSS_WaterRecycler")
                    {
                        waterRecyclerCapacity += converter.conversionRate.GetValue() * def.OutputResources.Find(res => res.ResourceName == "Water").Rate;
                    }
                    else if (def.InternalName == "KLSS_Greenhouse" || def.InternalName == "KLSS_FertilizedGreenhouse")
                    {
                        foodRecyclerCapacity += converter.conversionRate.GetValue() * def.OutputResources.Find(res => res.ResourceName == "Food").Rate;
                    }
                }
            }
        }
        curFoodRate -= Math.Min(foodRecyclerCapacity, curFoodRate);
        maxFoodRate -= Math.Min(foodRecyclerCapacity, maxFoodRate);
        curWaterRate -= Math.Min(waterRecyclerCapacity, curWaterRate);
        maxWaterRate -= Math.Min(waterRecyclerCapacity, maxWaterRate);
        curOxygenRate -= Math.Min(oxygenRecyclerCapacity, curOxygenRate);
        maxOxygenRate -= Math.Min(oxygenRecyclerCapacity, maxOxygenRate);

        // Get informations about the resource containers
        _foodResourceId = Game.ResourceDefinitionDatabase.GetResourceIDFromName("Food");
        _waterResourceId = Game.ResourceDefinitionDatabase.GetResourceIDFromName("Water");
        _oxygenResourceId = Game.ResourceDefinitionDatabase.GetResourceIDFromName("Oxygen");
        ResourceContainerGroup containerGroup = vessel.GetControlOwner().PartOwner.ContainerGroup;

        // Food
        double food = containerGroup.GetResourceStoredUnits(_foodResourceId);
        data.CurFood = food / curFoodRate;
        data.MaxFood = food / maxFoodRate;

        // Water
        double water = containerGroup.GetResourceStoredUnits(_waterResourceId);
        data.CurWater = water / curWaterRate;
        data.MaxWater = water / maxWaterRate;

        // Oxygen
        double oxygen = containerGroup.GetResourceStoredUnits(_oxygenResourceId);
        data.CurOxygen = oxygen / curOxygenRate;
        data.MaxOxygen = oxygen / maxOxygenRate;

        return data;
    }

    private LSEntryData GetObjectAssemblyData(IObjectAssembly assembly)
    {
        LSEntryData data = new();

        // Basic vessel information
        data.Id = assembly.Anchor.UniqueId;
        data.VesselName = Game.OAB.Current.Stats.CurrentWorkspaceVehicleDisplayName.GetValue();
        data.CurrentCrew = Game.SessionManager.KerbalRosterManager.GetAllKerbalsInAssembly(Game.SessionManager.KerbalRosterManager.KSCGuid, assembly).Count;
        data.MaximumCrew = 0;
        foreach (IObjectAssemblyPart part in assembly.Parts)
        {
            data.MaximumCrew += part.AvailablePart.PartData.crewCapacity;
        }

        // Consumption rate setting
        double consRate = KerbalLifeSupportSystemPlugin.Instance.ConfigResourceConsumptionRate.Value;

        // Base consumption rates
        double curFoodRate = FOOD_PER_SECOND * consRate * data.CurrentCrew;
        double maxFoodRate = FOOD_PER_SECOND * consRate * data.MaximumCrew;
        double curWaterRate = WATER_PER_SECOND * consRate * data.CurrentCrew;
        double maxWaterRate = WATER_PER_SECOND * consRate * data.MaximumCrew;
        double curOxygenRate = OXYGEN_PER_SECOND * consRate * data.CurrentCrew;
        double maxOxygenRate = OXYGEN_PER_SECOND * consRate * data.MaximumCrew;

        // Compute the current capacities of the recyclers on the vessel
        double foodRecyclerCapacity = 0.0;
        double waterRecyclerCapacity = 0.0;
        double oxygenRecyclerCapacity = 0.0;
        foreach (IObjectAssemblyPart part in assembly.Parts)
        {
            if (part.TryGetModule(out Module_ResourceConverter module_converter))
            {
                Data_ResourceConverter converter = module_converter._dataResourceConverter;
                if (converter.EnabledToggle.GetValue())
                {
                    ResourceConverterFormulaDefinition def = converter.FormulaDefinitions[converter.SelectedFormula];
                    if (def.InternalName == "KLSS_Combined")
                    {
                        waterRecyclerCapacity += converter.conversionRate.GetValue() * def.OutputResources.Find(res => res.ResourceName == "Water").Rate;
                        oxygenRecyclerCapacity += converter.conversionRate.GetValue() * def.OutputResources.Find(res => res.ResourceName == "Oxygen").Rate;
                    }
                    else if (def.InternalName == "KLSS_CO2Scrubber")
                    {
                        oxygenRecyclerCapacity += converter.conversionRate.GetValue() * def.OutputResources.Find(res => res.ResourceName == "Oxygen").Rate;
                    }
                    else if (def.InternalName == "KLSS_WaterRecycler")
                    {
                        waterRecyclerCapacity += converter.conversionRate.GetValue() * def.OutputResources.Find(res => res.ResourceName == "Water").Rate;
                    }
                    else if (def.InternalName == "KLSS_Greenhouse" || def.InternalName == "KLSS_FertilizedGreenhouse")
                    {
                        foodRecyclerCapacity += converter.conversionRate.GetValue() * def.OutputResources.Find(res => res.ResourceName == "Food").Rate;
                    }
                }
            }
        }
        curFoodRate -= Math.Min(foodRecyclerCapacity, curFoodRate);
        maxFoodRate -= Math.Min(foodRecyclerCapacity, maxFoodRate);
        curWaterRate -= Math.Min(waterRecyclerCapacity, curWaterRate);
        maxWaterRate -= Math.Min(waterRecyclerCapacity, maxWaterRate);
        curOxygenRate -= Math.Min(oxygenRecyclerCapacity, curOxygenRate);
        maxOxygenRate -= Math.Min(oxygenRecyclerCapacity, maxOxygenRate);

        // Get informations about the resource containers
        List<IObjectAssemblyPart> resourceParts;

        // Food
        double food = 0.0;
        assembly.TryGetPartsWithResourceStored("Food", out resourceParts);
        foreach (IObjectAssemblyPart part in resourceParts)
        {
            foreach (IObjectAssemblyResource resource in part.Resources)
            {
                if (resource.Name == "Food")
                    food += resource.Count;
            }
        }
        data.CurFood = food / curFoodRate;
        data.MaxFood = food / maxFoodRate;

        // Water
        double water = 0.0;
        assembly.TryGetPartsWithResourceStored("Water", out resourceParts);
        foreach (IObjectAssemblyPart part in resourceParts)
        {
            foreach (IObjectAssemblyResource resource in part.Resources)
            {
                if (resource.Name == "Water")
                    water += resource.Count;
            }
        }
        data.CurWater = water / curWaterRate;
        data.MaxWater = water / maxWaterRate;

        // Oxygen
        double oxygen = 0.0;
        assembly.TryGetPartsWithResourceStored("Oxygen", out resourceParts);
        foreach (IObjectAssemblyPart part in resourceParts)
        {
            foreach (IObjectAssemblyResource resource in part.Resources)
            {
                if (resource.Name == "Oxygen")
                    oxygen += resource.Count;
            }
        }
        data.CurOxygen = oxygen / curOxygenRate;
        data.MaxOxygen = oxygen / maxOxygenRate;

        return data;
    }

    public void SetEnabled(bool newState)
    {
        _uiEnabled = newState;
        s_container.style.display = newState ? DisplayStyle.Flex : DisplayStyle.None;
        GameObject.Find(KerbalLifeSupportSystemPlugin.ToolbarOABButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(newState);
    }

    private void SetupDocument()
    {
        var document = GetComponent<UIDocument>();

        // Set up localization
        if (document.TryGetComponent<DocumentLocalization>(out var localization))
        {
            localization.Localize();
        }
        else
        {
            document.EnableLocalization();
        }

        // root Visual Element
        s_container = document.rootVisualElement;

        // Move the GUI to its starting position
        s_container[0].transform.position = new Vector2(500, 50);
        s_container[0].CenterByDefault();

        // Hide the GUI by default
        s_container.style.display = DisplayStyle.None;
    }

    private void InitElements()
    {
        closeButton = s_container.Q<Button>("close-button");
        closeButton.RegisterCallback<ClickEvent>(OnCloseButton);

        lifeSupportEntriesView = s_container.Q<ScrollView>("ls-entries-body");

        s_initialized = true;
    }

    private void OnCloseButton(ClickEvent evt)
    {
        SetEnabled(false);
    }

    private void PopulateLSEntries()
    {
        // Reset life-support UI entries
        lifeSupportEntries.Clear();
        lifeSupportEntriesView.Clear();

        // If in a OAB & we have a main assembly, add it to the entries
        if (GameManager.Instance?.Game?.OAB?.Current?.Stats?.MainAssembly != null)
        {
            IObjectAssembly assembly = Game.OAB.Current.Stats.MainAssembly;
            lifeSupportEntries[assembly.Anchor.UniqueId] = new LifeSupportEntryControl(GetObjectAssemblyData(assembly), true, true);
            lifeSupportEntriesView.Add(lifeSupportEntries[assembly.Anchor.UniqueId]);

            KerbalLifeSupportSystemPlugin.Logger.LogInfo("Added <" + Game.OAB.Current.Stats.CurrentWorkspaceVehicleDisplayName.GetValue() + "> to the Life-Support UI list.");
        }

        // Add an entry for each vessel owned by the player
        List<VesselComponent> vessels = new();
        Game.ViewController.Universe.GetAllOwnedVessels(Game.LocalPlayer.PlayerId, ref vessels);
        foreach (VesselComponent vessel in vessels)
        {
            SimulationObjectModel simulationObject = vessel.SimulationObject;
            if (simulationObject != null && simulationObject.IsKerbal || (simulationObject.IsVessel && vessel.TotalCommandCrewCapacity > 0))
            {
                bool isActiveVessel = Game.ViewController.IsActiveVessel(simulationObject.Vessel);
                lifeSupportEntries[simulationObject.GlobalId] = new LifeSupportEntryControl(GetVesselData(simulationObject.Vessel), isActiveVessel, isActiveVessel);
                lifeSupportEntriesView.Add(lifeSupportEntries[simulationObject.GlobalId]);

                KerbalLifeSupportSystemPlugin.Logger.LogInfo("Added <" + simulationObject.Vessel.DisplayName + "> to the Life-Support UI list.");
            }
        }

        lifeSupportEntriesView.Sort(CompareLSEntries);
    }

    private void OnLSEntriesDirtyingEvent(MessageCenterMessage msg) => _isLSEntriesDirty = true;

    private int CompareLSEntries(VisualElement e1, VisualElement e2)
    {
        if (e1 is not LifeSupportEntryControl ls1 || e2 is not LifeSupportEntryControl ls2)
            return 0;

        if (GameManager.Instance?.Game?.OAB?.Current?.Stats?.MainAssembly != null)
        {
            IGGuid id = Game.OAB.Current.Stats.MainAssembly.Anchor.UniqueId;
            if (ls1.Data.Id == id)
                return -1;
            if (ls2.Data.Id == id)
                return 1;
        }

        VesselComponent vessel1 = Game.ViewController.Universe.FindVesselComponent(ls1.Data.Id);
        if (Game.ViewController.IsActiveVessel(vessel1))
            return -1;
        VesselComponent vessel2 = Game.ViewController.Universe.FindVesselComponent(ls2.Data.Id);
        if (Game.ViewController.IsActiveVessel(vessel2))
            return 1;

        return string.Compare(vessel1.DisplayName, vessel2.DisplayName);
    }
}
