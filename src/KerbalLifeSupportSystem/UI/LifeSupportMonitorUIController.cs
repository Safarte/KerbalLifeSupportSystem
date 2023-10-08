using KSP.Game;
using KSP.Modules;
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

    private static double SECONDS_PER_DAY = 86400;
    private static double FOOD_PER_SECOND = 0.001 / SECONDS_PER_DAY;
    private static double WATER_PER_SECOND = 3.0 / SECONDS_PER_DAY;
    private static double OXYGEN_PER_SECOND = 0.001 / SECONDS_PER_DAY;

    private bool uiEnabled = false;
    private Button closeButton;
    private Dictionary<VesselComponent, LSEntryData> vesselData = new();
    private ScrollView lsEntries;
    private ResourceDefinitionID _foodResourceId;
    private ResourceDefinitionID _waterResourceId;
    private ResourceDefinitionID _oxygenResourceId;

    private void Start()
    {
        SetupDocument();
    }

    private void Update()
    {
        if (!s_initialized)
        {
            InitElements();
        }

        if (!uiEnabled || GameManager.Instance?.Game?.ViewController?._universe is null)
        {
            return;
        }
        List<VesselComponent> vessels = new List<VesselComponent>();
        Game.ViewController._universe.GetAllOwnedVessels(Game.LocalPlayer.PlayerId, ref vessels);
        foreach (ObjectComponent objectComponent in vessels)
        {
            SimulationObjectModel simulationObject = objectComponent.SimulationObject;
            if (simulationObject != null && simulationObject.IsVessel && (simulationObject.IsKerbal || simulationObject.Vessel.HasCommandModule))
            {
                if (!vesselData.ContainsKey(simulationObject.Vessel))
                {
                    vesselData[simulationObject.Vessel] = GetVesselData(simulationObject.Vessel);
                    LifeSupportEntryControl entry = new LifeSupportEntryControl(vesselData[simulationObject.Vessel], true);
                    lsEntries.Add(entry);
                }
                else
                {
                    vesselData[simulationObject.Vessel] = GetVesselData(simulationObject.Vessel);
                    LifeSupportEntryControl entry = lsEntries.Q<LifeSupportEntryControl>("ls-entry__" + simulationObject.Vessel.DisplayName);
                    entry.SetValues(vesselData[simulationObject.Vessel]);
                }
            }
        }
    }

    private LSEntryData GetVesselData(VesselComponent vessel)
    {
        LSEntryData data = new();

        data.VesselName = vessel.DisplayName;
        data.CurrentCrew = GetKerbalCount(vessel);
        data.MaximumCrew = vessel.TotalCommandCrewCapacity;

        _foodResourceId = Game.ResourceDefinitionDatabase.GetResourceIDFromName("Food");
        _waterResourceId = Game.ResourceDefinitionDatabase.GetResourceIDFromName("Water");
        _oxygenResourceId = Game.ResourceDefinitionDatabase.GetResourceIDFromName("Oxygen");
        double current;
        // Food
        GetResourceValues(vessel, _foodResourceId, out current);
        data.CurFood = current / (FOOD_PER_SECOND * KerbalLifeSupportSystemPlugin.Instance.ConfigResourceConsumptionRate.Value * data.CurrentCrew);
        data.MaxFood = current / (FOOD_PER_SECOND * KerbalLifeSupportSystemPlugin.Instance.ConfigResourceConsumptionRate.Value * data.MaximumCrew);

        // Water
        GetResourceValues(vessel, _waterResourceId, out current);
        data.CurWater = current / (WATER_PER_SECOND * KerbalLifeSupportSystemPlugin.Instance.ConfigResourceConsumptionRate.Value * data.CurrentCrew);
        data.MaxWater = current / (WATER_PER_SECOND * KerbalLifeSupportSystemPlugin.Instance.ConfigResourceConsumptionRate.Value * data.MaximumCrew);

        // Oxygen
        GetResourceValues(vessel, _oxygenResourceId, out current);
        data.CurOxygen = current / (OXYGEN_PER_SECOND * KerbalLifeSupportSystemPlugin.Instance.ConfigResourceConsumptionRate.Value * data.CurrentCrew);
        data.MaxOxygen = current / (OXYGEN_PER_SECOND * KerbalLifeSupportSystemPlugin.Instance.ConfigResourceConsumptionRate.Value * data.MaximumCrew);

        return data;
    }

    private void GetResourceValues(VesselComponent vessel, ResourceDefinitionID resourceId, out double current)
    {
        current = 0;
        foreach (PartComponent part in vessel.SimulationObject.PartOwner.Parts)
        {
            if (part != null)
            {
                ContainedResourceData containedResourceData = part.PartResourceContainer.GetResourceContainedData(resourceId);
                current += containedResourceData.StoredUnits;
            }
        }
    }

    private int GetKerbalCount(VesselComponent vessel)
    {
        int count = 0;

        foreach (PartComponent part in vessel.SimulationObject.PartOwner.Parts)
        {
            if (part.TryGetModuleData<PartComponentModule_Command, Data_Command>(out Data_Command data))
                count += vessel.Game.SessionManager.KerbalRosterManager.GetAllKerbalsInSimObject(part.SimulationObject.GlobalId).Count;
        }

        return count;
    }

    public void SetEnabled(bool newState)
    {
        uiEnabled = newState;
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

        lsEntries = s_container.Q<ScrollView>("ls-entries-body");

        s_initialized = true;
    }

    private void OnCloseButton(ClickEvent evt)
    {
        SetEnabled(false);
    }
}
