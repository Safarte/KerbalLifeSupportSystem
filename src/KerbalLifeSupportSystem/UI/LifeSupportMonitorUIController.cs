using I2.Loc;
using KerbalLifeSupportSystem.Unity.Runtime;
using KSP.Game;
using KSP.Messages;
using KSP.Modules;
using KSP.OAB;
using KSP.Sim.impl;
using KSP.UI.Binding;
using UitkForKsp2.API;
using UnityEngine;
using UnityEngine.UIElements;

namespace KerbalLifeSupportSystem.UI;

/// <summary>
///     Controller for the LifeSupportMonitor UI
/// </summary>
internal class LifeSupportMonitorUIController : KerbalMonoBehaviour
{
    // The UIDocument component of the window game object
    private UIDocument _window;

    // The backing field for the IsWindowOpen property
    private bool _isWindowOpen;

    // The elements of the window that we need to access
    private Label _title;
    private VisualElement _rootElement;
    private LifeSupportFilterControl _filter;
    private TextField _searchBar;
    private LifeSupportHeaderControl _header;
    private ScrollView _lsEntriesView;
    private Toggle _showEmptyToggle;
    private Toggle _activeOnTopToggle;

    // Game Notification Manager
    private NotificationManager _notificationManager;

    // Dictionary with references to all visible life-support entries
    private readonly Dictionary<IGGuid, LifeSupportEntryControl> _lsEntries = new();

    // Track if the life-support entries need to be repopulated
    private bool _isLsEntriesDirty;

    // Track which resource exhausted notifications have been sent
    private readonly Dictionary<IGGuid, Dictionary<string, bool>> _exhaustedNotificationSent = new();

    /// <summary>
    ///     The state of the window. Setting this value will open or close the window.
    /// </summary>
    public bool IsWindowOpen
    {
        set
        {
            _isWindowOpen = value;

            // Set the display style of the root element to show or hide the window
            _rootElement.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;

            // Update the Flight AppBar button state
            GameObject.Find(KerbalLifeSupportSystemPlugin.ToolbarFlightButtonID)
                ?.GetComponent<UIValue_WriteBool_Toggle>()
                ?.SetValue(value);

            // Update the OAB AppBar button state
            GameObject.Find(KerbalLifeSupportSystemPlugin.ToolbarOabButtonID)
                ?.GetComponent<UIValue_WriteBool_Toggle>()
                ?.SetValue(value);
        }
    }

    /// <summary>
    /// Runs when the window is first created, and every time the window is re-enabled.
    /// </summary>
    private void OnEnable()
    {
        // Get the UIDocument component from the game object
        _window = GetComponent<UIDocument>();

        // Get the root element of the window.
        // Since we're cloning the UXML tree from a VisualTreeAsset, the actual root element is a TemplateContainer,
        // so we need to get the first child of the TemplateContainer to get our actual root VisualElement.
        _rootElement = _window.rootVisualElement[0];

        // Title
        _title = _rootElement.Q<Label>("title");

        // "Kerbal / Vessel / Both" filter
        _filter = _rootElement.Q<LifeSupportFilterControl>("ls-filter-select");

        // Search Bar
        _searchBar = _rootElement.Q<TextField>("search-bar");

        // Life-support entries header
        _header = _rootElement.Q<LifeSupportHeaderControl>("ls-entries-header");

        // Life-support entries list
        _lsEntriesView = _rootElement.Q<ScrollView>("ls-entries-body");

        // "Show empty vessels" toggle setting
        _showEmptyToggle = _rootElement.Q<Toggle>("settings-show-empty");

        // "Active vessel on top" toggle setting
        _activeOnTopToggle = _rootElement.Q<Toggle>("settings-active-on-top");

        // Center the window by default
        _rootElement.CenterByDefault();

        // Get the close button from the window
        var closeButton = _rootElement.Q<Button>("close-button");
        // Add a click event handler to the close button
        closeButton.clicked += () => IsWindowOpen = false;

        // Subscribe to entries dirtying events
        SubscribeToMessages();

        // Mark entries as dirty to trigger initialization
        _isLsEntriesDirty = true;

        // Set up notification manager
        _notificationManager = GameManager.Instance.Game.Notifications;

        // Apply localizations
        ApplyLocalization();
    }

    private void Update()
    {
        // Do not update if UI not visible or Game's Universe does not exist
        if (!_isWindowOpen || Game?.ViewController?.Universe is null) return;

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

            if (!_lsEntries.ContainsKey(assembly.Anchor.UniqueId)) PopulateLsEntries();

            _lsEntries[assembly.Anchor.UniqueId].SetData(GetObjectAssemblyData(assembly));
        }

        // Update all the player's vessels entries
        foreach (var (id, entry) in _lsEntries)
            if (Game?.OAB?.Current?.Stats?.MainAssembly is null ||
                id != Game.OAB.Current.Stats.MainAssembly.Anchor.UniqueId)
            {
                var vessel = Game.ViewController.Universe.FindVesselComponent(id);
                if (vessel != null)
                {
                    // Update the vessel entry
                    entry.SetData(GetVesselData(vessel));
                }
                else
                {
                    // An entry corresponds to a null vessel meaning we need to repopulate the entries
                    _isLsEntriesDirty = true;
                }
            }

        FilterAndSortEntries();
    }

    /// <summary>
    ///     Get the Life-Support entry data for the given vessel.
    /// </summary>
    /// <param name="vessel">Queried vessel</param>
    /// <returns>Life-Support entry data for the vessel</returns>
    private LifeSupportEntryControl.LsEntryData GetVesselData(VesselComponent vessel)
    {
        var containerGroup = vessel.GetControlOwner().PartOwner.ContainerGroup;

        LifeSupportEntryControl.LsEntryData data = new()
        {
            // Basic vessel information
            VesselName = vessel.DisplayName,
            IsActive = vessel.Game.ViewController.IsActiveVessel(vessel),
            CurrentCrew = vessel.Game.SessionManager.KerbalRosterManager
                .GetAllKerbalsInVessel(vessel.SimulationObject.GlobalId).Count,
            MaximumCrew = vessel.SimulationObject.IsKerbal ? 1 : 0,
            CurCrewRemainingTimes = [],
            MaxCrewRemainingTimes = []
        };

        // Add vessel to notifications tracker if not in it
        if (!_exhaustedNotificationSent.ContainsKey(vessel.SimulationObject.GlobalId))
            _exhaustedNotificationSent[vessel.SimulationObject.GlobalId] = new Dictionary<string, bool>();

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

            var curTime = data.CurrentCrew > 0 ? stored / curRate : 1e12;

            // Add resource to notifications tracker if not present
            if (!_exhaustedNotificationSent[vessel.SimulationObject.GlobalId].ContainsKey(resource))
                _exhaustedNotificationSent[vessel.SimulationObject.GlobalId][resource] = curTime < 1;

            // Send Resource Exhausted notification
            if (curTime < 1)
                NotifyResourceExhausted(Game.UniverseModel.UniverseTime, vessel, resource);

            // Set the current & max crew resource countdowns
            data.CurCrewRemainingTimes.Add(curTime);
            data.MaxCrewRemainingTimes.Add(stored / maxRate);
        }

        return data;
    }

    /// <summary>
    ///     Get the Life-Support entry data for the given OAB assembly.
    /// </summary>
    /// <param name="assembly">Queried assembly</param>
    /// <returns>Life-Support entry data for the assembly</returns>
    private LifeSupportEntryControl.LsEntryData GetObjectAssemblyData(IObjectAssembly assembly)
    {
        LifeSupportEntryControl.LsEntryData data = new()
        {
            // Basic vessel information
            VesselName = Game.OAB.Current.Stats.CurrentWorkspaceVehicleDisplayName.GetValue(),
            IsActive = true,
            CurrentCrew = Game.SessionManager.KerbalRosterManager
                .GetAllKerbalsInAssembly(Game.SessionManager.KerbalRosterManager.KSCGuid, assembly).Count,
            MaximumCrew = 0,
            CurCrewRemainingTimes = [],
            MaxCrewRemainingTimes = []
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
            data.CurCrewRemainingTimes.Add(data.CurrentCrew > 0 ? stored / curRate : 1e12);
            data.MaxCrewRemainingTimes.Add(stored / maxRate);
        }

        return data;
    }

    private void NotifyResourceExhausted(double universalTime, VesselComponent vessel, string resourceName)
    {
        var id = vessel.SimulationObject.GlobalId;
        if (_exhaustedNotificationSent.ContainsKey(id) && _exhaustedNotificationSent[id][resourceName])
            return;

        _notificationManager.ProcessNotification(new NotificationData
        {
            Tier = NotificationTier.Alert,
            Importance = NotificationImportance.High,
            AlertTitle = new NotificationLineItemData
            {
                ObjectParams = [resourceName],
                LocKey = "KLSS/Notifications/ResourceGraceStarted"
            },
            TimeStamp = universalTime,
            FirstLine = new NotificationLineItemData
            {
                LocKey = vessel.DisplayName
            }
        });

        if (_exhaustedNotificationSent.TryGetValue(id, out var value))
        {
            value[resourceName] = true;
        }
        else
        {
            _exhaustedNotificationSent[id] = new Dictionary<string, bool> { { resourceName, true } };
        }
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
    ///     Populate the Life-Support UI entries with all relevant vessels & EVA Kerbals
    /// </summary>
    private void PopulateLsEntries()
    {
        // Set Header localized resource names
        List<string> resourceNames = [];
        foreach (var res in KerbalLifeSupportSystemPlugin.Instance.LsInputResources)
            resourceNames.Add(new LocalizedString("Resource/DisplayName/" + res));
        _header.SetResources(resourceNames);

        // Get list of all owned vessels
        List<VesselComponent> vessels = [];
        Game.ViewController.Universe.GetAllOwnedVessels(Game.LocalPlayer.PlayerId, ref vessels);

        // Get list of owned vessel IDs
        var vesselIds = new List<IGGuid>();
        foreach (var vessel in vessels) vesselIds.Add(vessel.SimulationObject.GlobalId);

        // Mark entries that are neither the OAB assembly or an owned vessel to be removed
        var toRemove = new List<IGGuid>();
        foreach (var (id, _) in _lsEntries)
        {
            if (vesselIds.Contains(id) ||
                (Game?.OAB?.Current?.Stats?.MainAssembly != null &&
                 id == Game.OAB.Current.Stats.MainAssembly.Anchor.UniqueId))
                continue;
            toRemove.Add(id);
        }

        // Remove marked entries
        foreach (var id in toRemove) _lsEntries.Remove(id);

        // If in a OAB & there is a main assembly, add it to the entries
        if (Game?.OAB?.Current?.Stats?.MainAssembly != null)
        {
            var assembly = Game.OAB.Current.Stats.MainAssembly;

            // Add OAB assembly entry if not already present
            if (!_lsEntries.ContainsKey(assembly.Anchor.UniqueId))
            {
                _lsEntries[assembly.Anchor.UniqueId] =
                    new LifeSupportEntryControl(GetObjectAssemblyData(assembly));

                KerbalLifeSupportSystemPlugin.Logger.LogInfo("Added <" +
                                                             Game.OAB.Current.Stats.CurrentWorkspaceVehicleDisplayName
                                                                 .GetValue() + "> to the Life-Support UI list.");
            }
        }

        // Add an entry for each vessel owned by the player
        foreach (var vessel in vessels)
        {
            var simulationObject = vessel.SimulationObject;

            // Add vessel to the entries if it has non-zero crew capacity or if it is a Kerbal in EVA
            if (_lsEntries.ContainsKey(simulationObject.GlobalId) ||
                (simulationObject is not { IsKerbal: true } &&
                 (!simulationObject.IsVessel || vessel.TotalCommandCrewCapacity <= 0))) continue;

            _lsEntries[simulationObject.GlobalId] =
                new LifeSupportEntryControl(GetVesselData(simulationObject.Vessel));

            KerbalLifeSupportSystemPlugin.Logger.LogInfo("Added <" + simulationObject.Vessel.DisplayName +
                                                         "> to the Life-Support UI list.");
        }
    }

    private void FilterAndSortEntries()
    {
        // Reset life-support entries ScrollView
        FilterLsEntries();

        // Sort the entries based on the defined ordering
        _lsEntriesView.Sort(CompareLsEntries);
    }

    private void FilterLsEntries()
    {
        var filteredEntries = new List<LifeSupportEntryControl>();

        foreach (var (id, entry) in _lsEntries)
        {
            if (!_showEmptyToggle.value && entry.CurrentCrew <= 0) continue;

            var simObject = Game.ViewController.Universe.FindSimObject(id);
            switch (_filter.SelectedType)
            {
                case LifeSupportFilterControl.FilterType.Both:
                case LifeSupportFilterControl.FilterType.Kerbal when simObject.IsKerbal:
                case LifeSupportFilterControl.FilterType.Vessel when !simObject.IsKerbal:
                {
                    if (_searchBar.value == "" || entry.Name.Contains(_searchBar.value))
                        filteredEntries.Add(entry);
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        // Apply changes to ls entries view
        _lsEntriesView.Clear();
        foreach (var entry in filteredEntries) _lsEntriesView.Add(entry);
    }

    /// <summary>
    ///     Sets up the entries to be re-initialized on the next update tick
    /// </summary>
    private void OnLSEntriesDirtyingEvent(MessageCenterMessage msg)
    {
        _isLsEntriesDirty = true;
    }

    /// <summary>
    ///     Comparator for Life-Support UI entries, active OAB assembly and active vessels are shown on top, followed by
    ///     pinned entries, followed by other entries. The sorting order follows the selected sorting.
    /// </summary>
    /// <param name="e1">First LS UI entry</param>
    /// <param name="e2">Second LS UI entry</param>
    /// <returns>Comparison result (-1, 0 or 1)</returns>
    private int CompareLsEntries(VisualElement e1, VisualElement e2)
    {
        if (e1 is not LifeSupportEntryControl ls1 || e2 is not LifeSupportEntryControl ls2)
            return 0;

        return _activeOnTopToggle.value switch
        {
            true when ls1.IsActive => -1,
            true when ls2.IsActive => 1,
            _ => ls1.IsPinned switch
            {
                true when ls2.IsPinned => CompareEntriesSort(ls1, ls2),
                true => -1,
                _ => ls2.IsPinned ? 1 : CompareEntriesSort(ls1, ls2)
            }
        };
    }

    /// <summary>
    /// Comparator for Life-Support entries based on current selected sort direction
    /// </summary>
    /// <param name="ls1">First LS UI entry</param>
    /// <param name="ls2">Second LS UI entry</param>
    /// <returns>Comparison result (-1, 0 or 1)</returns>
    private int CompareEntriesSort(LifeSupportEntryControl ls1, LifeSupportEntryControl ls2)
    {
        if (_header.NameCell.SortDirection != HeaderCell.SortType.None)
            return (_header.NameCell.SortDirection == HeaderCell.SortType.Decreasing ? 1 : -1) *
                   string.CompareOrdinal(ls1.Name, ls2.Name);

        var comp = 0;

        for (var i = 0; i < _header.HeaderCells.Count; i++)
        {
            var dir = _header.HeaderCells[i].SortDirection;
            if (dir != HeaderCell.SortType.None)
                comp += (dir == HeaderCell.SortType.Increasing ? 1 : -1) * ls1.Times[i].CompareTo(ls2.Times[i]);
        }

        return comp;
    }

    private void ApplyLocalization()
    {
        _title.text = new LocalizedString("KLSS/UI/Monitor/Title");
        _filter.VesselsSelectText = new LocalizedString("KLSS/UI/Monitor/FilterVessels");
        _filter.KerbalSelectText = new LocalizedString("KLSS/UI/Monitor/FilterKerbals");
        _filter.BothSelectText = new LocalizedString("KLSS/UI/Monitor/FilterBoth");
        _header.NameCell.Text = new LocalizedString("KLSS/UI/Monitor/HeaderName");
        _showEmptyToggle.label = new LocalizedString("KLSS/UI/Monitor/SettingShowEmpty");
        _activeOnTopToggle.label = new LocalizedString("KLSS/UI/Monitor/SettingActiveOnTop");
        // Set Header localized resource names
        List<string> resourceNames = [];
        foreach (var res in KerbalLifeSupportSystemPlugin.Instance.LsInputResources)
            resourceNames.Add(new LocalizedString("Resource/DisplayName/" + res));
        _header.SetResources(resourceNames);
    }

    private void OnDestroy()
    {
        if (IsGameShuttingDown)
            return;

        // Clean up every event subscriptions
        Game.Messages.Unsubscribe<GameLanguageChangedMessage>(_ => ApplyLocalization());
        Game.Messages.Unsubscribe<GameLoadFinishedMessage>(OnLSEntriesDirtyingEvent);
        Game.Messages.Unsubscribe<VesselSplitMessage>(OnLSEntriesDirtyingEvent);
        Game.Messages.Unsubscribe<VesselChangingMessage>(OnLSEntriesDirtyingEvent);
    }

    /// <summary>
    ///     Set up all the needed game event subscriptions.
    /// </summary>
    private void SubscribeToMessages()
    {
        Game.Messages.PersistentSubscribe<GameLanguageChangedMessage>(_ => ApplyLocalization());
        Game.Messages.PersistentSubscribe<GameLoadFinishedMessage>(OnLSEntriesDirtyingEvent);
        Game.Messages.PersistentSubscribe<VesselSplitMessage>(OnLSEntriesDirtyingEvent);
        Game.Messages.PersistentSubscribe<VesselChangingMessage>(OnLSEntriesDirtyingEvent);
    }
}