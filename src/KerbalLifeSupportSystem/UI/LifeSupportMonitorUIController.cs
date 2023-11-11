using KSP.Game;
using KSP.Messages;
using KSP.Modules;
using KSP.OAB;
using KSP.Sim.impl;
using KSP.UI.Binding;
using UitkForKsp2.API;
using UnityEngine;
using UnityEngine.UIElements;
using static KerbalLifeSupportSystem.UI.LifeSupportEntryControl;

namespace KerbalLifeSupportSystem.UI;

internal class LifeSupportMonitorUIController : KerbalMonoBehaviour
{
    private static VisualElement _container;
    private static bool _initialized;
    private readonly Dictionary<IGGuid, LifeSupportEntryControl> _lifeSupportEntries = new();
    private Button _closeButton;
    private bool _isLsEntriesDirty;
    private ScrollView _lifeSupportEntriesView;

    private bool _uiEnabled;

    private void Start()
    {
        SetupDocument();
        InitMessages();
    }

    private void Update()
    {
        if (!_initialized) InitElements();

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
        var containerGroup = vessel.GetControlOwner().PartOwner.ContainerGroup;

        LsEntryData data = new()
        {
            // Basic vessel information
            Id = vessel.GlobalId,
            VesselName = vessel.DisplayName,
            CurrentCrew = vessel.Game.SessionManager.KerbalRosterManager
                .GetAllKerbalsInVessel(vessel.SimulationObject.GlobalId).Count,
            MaximumCrew = vessel.SimulationObject.IsKerbal ? 1 : 0,
            CurrentResourcesCountdowns = new Dictionary<string, double>(),
            MaxResourcesCountdowns = new Dictionary<string, double>()
        };

        foreach (var part in vessel.SimulationObject.PartOwner.Parts)
            data.MaximumCrew += !vessel.SimulationObject.IsKerbal ? part.PartData.crewCapacity : 0;

        Dictionary<string, double> recyclerCapacities = new();
        foreach (var resource in KerbalLifeSupportSystemPlugin.Instance.LsInputResources)
            recyclerCapacities[resource] = 0.0;

        foreach (var part in vessel.SimulationObject.PartOwner.Parts)
            if (part.TryGetModuleData<PartComponentModule_ResourceConverter, Data_ResourceConverter>(out var converter))
                if (converter.ConverterIsActive)
                {
                    var def = converter.FormulaDefinitions[converter.SelectedFormula];
                    foreach (var resource in KerbalLifeSupportSystemPlugin.Instance.LsInputResources)
                        if (KerbalLifeSupportSystemPlugin.Instance.LsRecyclerNames[resource].Contains(def.InternalName))
                            recyclerCapacities[resource] += converter.conversionRate.GetValue() *
                                                            def.OutputResources
                                                                .Find(res => res.ResourceName == resource).Rate;
                }

        foreach (var resource in KerbalLifeSupportSystemPlugin.Instance.LsInputResources)
        {
            double rate = KerbalLifeSupportSystemPlugin.Instance.ConsumptionRates[resource].Value;

            var curRate = KerbalLifeSupportSystemPlugin.Instance.LsConsumptionRates[resource] * rate * data.CurrentCrew;
            var maxRate = KerbalLifeSupportSystemPlugin.Instance.LsConsumptionRates[resource] * rate * data.MaximumCrew;

            curRate -= Math.Min(recyclerCapacities[resource], curRate);
            maxRate -= Math.Min(recyclerCapacities[resource], maxRate);

            var resourceId = Game.ResourceDefinitionDatabase.GetResourceIDFromName(resource);
            var stored = containerGroup.GetResourceStoredUnits(resourceId);

            data.CurrentResourcesCountdowns[resource] = stored / curRate;
            data.MaxResourcesCountdowns[resource] = stored / maxRate;
        }

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
            MaximumCrew = 0,
            CurrentResourcesCountdowns = new Dictionary<string, double>(),
            MaxResourcesCountdowns = new Dictionary<string, double>()
        };

        foreach (var part in assembly.Parts) data.MaximumCrew += part.AvailablePart.PartData.crewCapacity;

        Dictionary<string, double> recyclerCapacities = new();
        foreach (var resource in KerbalLifeSupportSystemPlugin.Instance.LsInputResources)
            recyclerCapacities[resource] = 0.0;

        foreach (var part in assembly.Parts)
            if (part.TryGetModule(out Module_ResourceConverter moduleConverter))
            {
                var converter = moduleConverter._dataResourceConverter;
                if (!converter.EnabledToggle.GetValue()) continue;
                var def = converter.FormulaDefinitions[converter.SelectedFormula];
                foreach (var resource in KerbalLifeSupportSystemPlugin.Instance.LsInputResources)
                    if (KerbalLifeSupportSystemPlugin.Instance.LsRecyclerNames[resource].Contains(def.InternalName))
                        recyclerCapacities[resource] += converter.conversionRate.GetValue() *
                                                        def.OutputResources
                                                            .Find(res => res.ResourceName == resource).Rate;
            }

        foreach (var resource in KerbalLifeSupportSystemPlugin.Instance.LsInputResources)
        {
            double rate = KerbalLifeSupportSystemPlugin.Instance.ConsumptionRates[resource].Value;

            var curRate = KerbalLifeSupportSystemPlugin.Instance.LsConsumptionRates[resource] * rate * data.CurrentCrew;
            var maxRate = KerbalLifeSupportSystemPlugin.Instance.LsConsumptionRates[resource] * rate * data.MaximumCrew;

            curRate -= Math.Min(recyclerCapacities[resource], curRate);
            maxRate -= Math.Min(recyclerCapacities[resource], maxRate);

            var resourceId = Game.ResourceDefinitionDatabase.GetResourceIDFromName(resource);
            assembly.TryGetPartsWithResourceStored(resource, out var resourceParts);
            var stored = 0.0;
            foreach (var part in resourceParts)
            foreach (var container in part.Containers)
                stored += container.GetResourceStoredUnits(resourceId);

            data.CurrentResourcesCountdowns[resource] = stored / curRate;
            data.MaxResourcesCountdowns[resource] = stored / maxRate;
        }

        return data;
    }

    public void SetEnabled(bool newState)
    {
        _uiEnabled = newState;
        _container.style.display = newState ? DisplayStyle.Flex : DisplayStyle.None;
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
        _container = document.rootVisualElement;

        // Move the GUI to its starting position
        _container[0].transform.position = new Vector2(500, 50);
        _container[0].CenterByDefault();

        // Hide the GUI by default
        _container.style.display = DisplayStyle.None;
    }

    private void InitElements()
    {
        _closeButton = _container.Q<Button>("close-button");
        _closeButton.RegisterCallback<ClickEvent>(OnCloseButton);

        _lifeSupportEntriesView = _container.Q<ScrollView>("ls-entries-body");

        _initialized = true;
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