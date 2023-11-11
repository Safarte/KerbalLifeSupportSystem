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
        // Setup the UI document
        SetupDocument();

        // Initialize all event listeners
        InitMessages();
    }

    private void Update()
    {
        // Initialize UI elements
        if (!_initialized) InitElements();

        // Do not update if UI not visible or Game's Universe does not exist
        if (!_uiEnabled || Game?.ViewController?.Universe is null) return;

        // (Re)populate the UI entries if needed
        if (_isLsEntriesDirty)
        {
            _isLsEntriesDirty = false;
            PopulateLsEntries();
        }

        // Update the OAB Main Assembly entry
        if (Game?.OAB?.Current?.Stats?.MainAssembly is not null)
        {
            var assembly = Game.OAB.Current.Stats.MainAssembly;

            if (!_lifeSupportEntries.ContainsKey(assembly.Anchor.UniqueId)) PopulateLsEntries();

            _lifeSupportEntries[assembly.Anchor.UniqueId].SetValues(GetObjectAssemblyData(assembly), true);
        }

        // Update all the player's vessels entries
        foreach (var entry in _lifeSupportEntries)
            if (Game?.OAB?.Current?.Stats?.MainAssembly is null ||
                entry.Key != Game.OAB.Current.Stats.MainAssembly.Anchor.UniqueId)
            {
                var vessel = Game.ViewController.Universe.FindVesselComponent(entry.Key);
                if (vessel != null)
                {
                    // Update the vessel entry
                    var isActiveVessel = Game.ViewController.IsActiveVessel(vessel);
                    entry.Value.SetValues(GetVesselData(vessel), isActiveVessel);
                }
                else
                {
                    // An entry corresponds to a null vessel meaning we need to repopulate the entries
                    _isLsEntriesDirty = true;
                }
            }
    }

    private void OnDestroy()
    {
        if (IsGameShuttingDown)
            return;

        // Clean up every event subscriptions
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

    /// <summary>
    ///     Set up all the needed game event subscriptions (LS UI entries refreshing).
    /// </summary>
    private void InitMessages()
    {
        // TODO: Remove potentially redundant event subscriptions
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

    /// <summary>
    ///     Get the Life-Support entry data for the given vessel.
    /// </summary>
    /// <param name="vessel">Queried vessel</param>
    /// <returns>Life-Support entry data for the vessel</returns>
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

        // Iterate over parts to compute total crew capacity
        foreach (var part in vessel.SimulationObject.PartOwner.Parts)
            data.MaximumCrew += !vessel.SimulationObject.IsKerbal ? part.PartData.crewCapacity : 0;

        // Initialize LS resources production rates from recyclers
        Dictionary<string, double> recyclerCapacities = new();
        foreach (var resource in KerbalLifeSupportSystemPlugin.Instance.LsInputResources)
            recyclerCapacities[resource] = 0.0;

        // Get the LS recycler production rates for the vessel
        foreach (var part in vessel.SimulationObject.PartOwner.Parts)
            if (part.TryGetModuleData<PartComponentModule_ResourceConverter, Data_ResourceConverter>(out var converter))
                if (converter.ConverterIsActive)
                    GetRecyclerCapacities(converter, ref recyclerCapacities);

        foreach (var resource in KerbalLifeSupportSystemPlugin.Instance.LsInputResources)
        {
            // Consumption rate setting
            double rate = KerbalLifeSupportSystemPlugin.Instance.ConsumptionRates[resource].Value;

            // Consumption rates for current and maximum amount of crew
            var curRate = KerbalLifeSupportSystemPlugin.Instance.LsConsumptionRates[resource] * rate * data.CurrentCrew;
            var maxRate = KerbalLifeSupportSystemPlugin.Instance.LsConsumptionRates[resource] * rate * data.MaximumCrew;

            // Subtract recyclers resource production rate
            curRate -= Math.Min(recyclerCapacities[resource], curRate);
            maxRate -= Math.Min(recyclerCapacities[resource], maxRate);

            // Get currently stored resource amount
            var resourceId = Game.ResourceDefinitionDatabase.GetResourceIDFromName(resource);
            var stored = containerGroup.GetResourceStoredUnits(resourceId);

            // Set the current & max crew resource countdowns
            data.CurrentResourcesCountdowns[resource] = stored / curRate;
            data.MaxResourcesCountdowns[resource] = stored / maxRate;
        }

        return data;
    }

    /// <summary>
    ///     Get the Life-Support entry data for the given OAB assembly.
    /// </summary>
    /// <param name="assembly">Queried assembly</param>
    /// <returns>Life-Support entry data for the assembly</returns>
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

        // Iterate over parts to compute total crew capacity
        foreach (var part in assembly.Parts) data.MaximumCrew += part.AvailablePart.PartData.crewCapacity;

        // Initialize LS resources production rates from recyclers
        Dictionary<string, double> recyclerCapacities = new();
        foreach (var resource in KerbalLifeSupportSystemPlugin.Instance.LsInputResources)
            recyclerCapacities[resource] = 0.0;

        // Get the LS recycler production rates for the vessel
        foreach (var part in assembly.Parts)
            if (part.TryGetModuleData<PartComponentModule_ResourceConverter, Data_ResourceConverter>(out var converter))
                if (converter.EnabledToggle.GetValue())
                    GetRecyclerCapacities(converter, ref recyclerCapacities);

        foreach (var resource in KerbalLifeSupportSystemPlugin.Instance.LsInputResources)
        {
            // Consumption rate setting
            double rate = KerbalLifeSupportSystemPlugin.Instance.ConsumptionRates[resource].Value;

            // Consumption rates for current and maximum amount of crew
            var curRate = KerbalLifeSupportSystemPlugin.Instance.LsConsumptionRates[resource] * rate * data.CurrentCrew;
            var maxRate = KerbalLifeSupportSystemPlugin.Instance.LsConsumptionRates[resource] * rate * data.MaximumCrew;

            // Subtract recyclers resource production rate
            curRate -= Math.Min(recyclerCapacities[resource], curRate);
            maxRate -= Math.Min(recyclerCapacities[resource], maxRate);

            // Get currently stored resource amount
            var stored = GetAssemblyResourceStored(assembly, resource);

            // Set the current & max crew resource countdowns
            data.CurrentResourcesCountdowns[resource] = stored / curRate;
            data.MaxResourcesCountdowns[resource] = stored / maxRate;
        }

        return data;
    }

    /// <summary>
    ///     Get the total amount of <paramref name="resource" /> in the OAB assembly.
    /// </summary>
    /// <param name="assembly">OAB object assembly</param>
    /// <param name="resource">Queried resource name</param>
    /// <returns>Amount of stored resource</returns>
    private double GetAssemblyResourceStored(IObjectAssembly assembly, string resource)
    {
        // Get list of parts containing the resource
        assembly.TryGetPartsWithResourceStored(resource, out var resourceParts);

        // Get the resource ID
        var resourceId = Game.ResourceDefinitionDatabase.GetResourceIDFromName(resource);

        // Accumulate the amount of resource stored in the parts
        var stored = 0.0;
        foreach (var part in resourceParts)
        foreach (var container in part.Containers)
            stored += container.GetResourceStoredUnits(resourceId);

        return stored;
    }

    /// <summary>
    ///     Adds all the recyclers production capacities for the converter's selected formula.
    /// </summary>
    /// <param name="converter">The ResourceConverter module data</param>
    /// <param name="recyclerCapacities">Reference to the recycler capacities dictionary for all LS resources</param>
    private static void GetRecyclerCapacities(Data_ResourceConverter converter,
        ref Dictionary<string, double> recyclerCapacities)
    {
        // Selected recycler formula definition
        var selectedFormula = converter.SelectedFormula;

        // Needed to avoid weird behaviour in the VAB caused by incomplete initialization of the ModuleData
        if (converter.SelectedFormula < 0 || converter.SelectedFormula >= converter.FormulaDefinitions.Count)
            selectedFormula = 0;

        var def = converter.FormulaDefinitions[selectedFormula];

        // Add the recyclers' production rate for each produced LS resource
        foreach (var resource in KerbalLifeSupportSystemPlugin.Instance.LsInputResources)
            if (def.OutputResources.Any(res => res.ResourceName == resource))
                recyclerCapacities[resource] += converter.conversionRate.GetValue() *
                                                def.OutputResources
                                                    .Find(res => res.ResourceName == resource).Rate;
    }

    /// <summary>
    ///     Set the Life-Support UI visibility state
    /// </summary>
    /// <param name="newState"></param>
    public void SetEnabled(bool newState)
    {
        _uiEnabled = newState;
        _container.style.display = newState ? DisplayStyle.Flex : DisplayStyle.None;

        // Update the toolbars toggle value
        GameObject.Find(KerbalLifeSupportSystemPlugin.ToolbarOabButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()
            ?.SetValue(newState);
        GameObject.Find(KerbalLifeSupportSystemPlugin.ToolbarFlightButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()
            ?.SetValue(newState);
    }

    /// <summary>
    ///     Setup the UI document (localization and starting position)
    /// </summary>
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

    /// <summary>
    ///     Initialize UI elements
    /// </summary>
    private void InitElements()
    {
        // Close button
        _closeButton = _container.Q<Button>("close-button");
        _closeButton.RegisterCallback<ClickEvent>(OnCloseButton);

        // Life-Support entries list
        _lifeSupportEntriesView = _container.Q<ScrollView>("ls-entries-body");

        _initialized = true;
    }

    /// <summary>
    ///     Closes the Life-Support UI on Close Button click
    /// </summary>
    private void OnCloseButton(ClickEvent evt)
    {
        SetEnabled(false);
    }

    /// <summary>
    ///     Populate the Life-Support UI entries with all relevant vessels & EVA Kerbals
    /// </summary>
    private void PopulateLsEntries()
    {
        // Reset life-support UI entries
        _lifeSupportEntries.Clear();
        _lifeSupportEntriesView.Clear();

        // If in a OAB & there is a main assembly, add it to the entries
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

            // Add vessel to the entries if it has non-zero crew capacity or if it is a Kerbal in EVA
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

        // Sort the entries based on the defined ordering
        _lifeSupportEntriesView.Sort(CompareLsEntries);
    }

    /// <summary>
    ///     Sets up the entries to be re-initialized on the next update tick
    /// </summary>
    private void OnLSEntriesDirtyingEvent(MessageCenterMessage msg)
    {
        _isLsEntriesDirty = true;
    }

    /// <summary>
    ///     Comparator for Life-Support UI entries, active OAB assembly and active vessels are shown on top.
    /// </summary>
    /// <param name="e1">First LS UI entry</param>
    /// <param name="e2">Second LS UI entry</param>
    /// <returns>Comparison result (-1, 0 or 1)</returns>
    private int CompareLsEntries(VisualElement e1, VisualElement e2)
    {
        if (e1 is not LifeSupportEntryControl ls1 || e2 is not LifeSupportEntryControl ls2)
            return 0;

        // The current OAB assembly should always be first
        if (Game?.OAB?.Current?.Stats?.MainAssembly != null)
        {
            var id = Game.OAB.Current.Stats.MainAssembly.Anchor.UniqueId;
            if (ls1.Data.Id == id)
                return -1;
            if (ls2.Data.Id == id)
                return 1;
        }

        // In flight, the active vessel should always be first
        var vessel1 = Game.ViewController.Universe.FindVesselComponent(ls1.Data.Id);
        if (Game.ViewController.IsActiveVessel(vessel1))
            return -1;
        var vessel2 = Game.ViewController.Universe.FindVesselComponent(ls2.Data.Id);
        if (Game.ViewController.IsActiveVessel(vessel2))
            return 1;

        // Lexical ordering for all other entries
        return string.CompareOrdinal(vessel1.DisplayName, vessel2.DisplayName);
    }
}