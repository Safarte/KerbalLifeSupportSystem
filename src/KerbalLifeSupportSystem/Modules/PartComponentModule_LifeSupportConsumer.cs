using KSP.Game;
using KSP.Messages;
using KSP.Sim;
using KSP.Sim.impl;
using KSP.Sim.ResourceSystem;

namespace KerbalLifeSupportSystem.Modules;

public class PartComponentModule_LifeSupportConsumer : PartComponentModule
{
    // Container group for the vessel
    private ResourceContainerGroup _containerGroup;

    // Ingredient & products units
    private ResourceUnitsPair[] _currentIngredientUnits;
    private ResourceUnitsPair[] _currentProductUnits;

    // Module data
    private Data_LifeSupportConsumer _dataLifeSupportConsumer;

    // Are there resource requests that need to be handled
    private bool _hasPendingRequests;

    // List of Kerbals in the part
    private List<KerbalInfo> _kerbalsInSimObject = new();

    // Game's database for the resource definitions
    private ResourceDefinitionDatabase _resourceDB;

    // State of the last resource request resolution
    private FlowRequestResolutionState _returnedRequestResolution;

    // Game's Kerbals roster manager
    private KerbalRosterManager _rosterManager;

    public override Type PartBehaviourModuleType => typeof(Module_LifeSupportConsumer);

    public override void OnStart(double universalTime)
    {
        if (!DataModules.TryGetByType(out _dataLifeSupportConsumer))
        {
            KerbalLifeSupportSystemPlugin.Logger.LogError(
                "Unable to find a Data_LifeSupportConsumer in the PartComponentModule for " + Part.PartName);
        }
        else if (GameManager.Instance.Game == null || GameManager.Instance.Game.ResourceDefinitionDatabase == null)
        {
            KerbalLifeSupportSystemPlugin.Logger.LogError(
                "Unable to find a valid game with a resource definition database");
        }
        else
        {
            // Initialize useful objects
            _dataLifeSupportConsumer.SetupResourceRequest(resourceFlowRequestBroker);
            _containerGroup = Part.PartOwner.ContainerGroup;
            _resourceDB = GameManager.Instance.Game.ResourceDefinitionDatabase;
            _rosterManager = GameManager.Instance.Game.SessionManager.KerbalRosterManager;

            // Set up resource request
            resourceFlowRequestBroker.SetCommands(_dataLifeSupportConsumer.RequestHandle,
                _dataLifeSupportConsumer.LifeSupportDefinition.AcceptanceThreshold,
                _dataLifeSupportConsumer.RequestConfig);
            resourceFlowRequestBroker.SetRequestActive(_dataLifeSupportConsumer.RequestHandle);
            RefreshIngredientDataStructures();
            SetResourceRates();

            // Kerbal EVA handling
            Game.Messages.Subscribe<KerbalLocationChanged>(OnKerbalLocationChanged);
        }
    }

    public override void OnShutdown()
    {
        RemoveResourceRequest(_dataLifeSupportConsumer.RequestHandle);
        Game.Messages.Unsubscribe<KerbalLocationChanged>(OnKerbalLocationChanged);
    }

    public override void OnUpdate(double universalTime, double deltaUniversalTime)
    {
        _kerbalsInSimObject = _rosterManager.GetAllKerbalsInSimObject(Part.SimulationObject.GlobalId);
        _dataLifeSupportConsumer.numKerbals = _kerbalsInSimObject.Count;

        if (_dataLifeSupportConsumer.numKerbals <= 0) return;

        // Initialize the last consumed database if needed
        foreach (var kerbal in _kerbalsInSimObject)
            if (!_dataLifeSupportConsumer.lastConsumed.ContainsKey(kerbal.NameKey))
                _dataLifeSupportConsumer.lastConsumed[kerbal.NameKey] = new Dictionary<string, double>();

        // Send life-support resource request
        RefreshIngredientDataStructures();
        SendResourceRequest();

        // Handle resource request response
        if (_hasPendingRequests) ResolveResourceRequest(universalTime);
        _hasPendingRequests = true;

        UpdateKerbalsStatus(universalTime);
    }

    private void ResolveResourceRequest(double time)
    {
        // Get resource request response
        _returnedRequestResolution = resourceFlowRequestBroker.GetRequestState(_dataLifeSupportConsumer.RequestHandle);

        if (_returnedRequestResolution.WasLastTickDeliveryAccepted)
        {
            // If delivery was accepted, update the "last consumed" database
            UpdateLastConsumed(time);
        }
        else
        {
            // If delivery was denied, resend a resource request only for the remaining resources
            ClearMissingResources();
            SendResourceRequest();
        }

        _hasPendingRequests = false;
    }

    private void UpdateLastConsumed(double time)
    {
        foreach (var ingredient in _currentIngredientUnits)
        {
            var resourceName = _resourceDB.GetResourceNameFromID(ingredient.resourceID);

            // If resource is not exhausted, set last consumed to now
            if (_containerGroup.GetResourceStoredUnits(ingredient.resourceID) >
                _dataLifeSupportConsumer.LifeSupportDefinition.AcceptanceThreshold)
                foreach (var kerbal in _kerbalsInSimObject)
                    _dataLifeSupportConsumer.lastConsumed[kerbal.NameKey][resourceName] = time;
        }
    }

    private void ClearMissingResources()
    {
        // Clear missing products
        for (var i = 0; i < _currentProductUnits.Length; ++i)
            if (_containerGroup.GetResourceCapacityUnits(_currentProductUnits[i].resourceID) <
                _dataLifeSupportConsumer.LifeSupportDefinition.AcceptanceThreshold)
                _currentProductUnits[i].units = 0.0;

        // Clear missing ingredients
        for (var i = 0; i < _currentIngredientUnits.Length; ++i)
            if (_containerGroup.GetResourceStoredUnits(_currentIngredientUnits[i].resourceID) <
                _dataLifeSupportConsumer.LifeSupportDefinition.AcceptanceThreshold)
                _currentIngredientUnits[i].units = 0.0;
    }

    private void SendResourceRequest()
    {
        SetResourceRates();
        resourceFlowRequestBroker.SetCommands(_dataLifeSupportConsumer.RequestHandle,
            _dataLifeSupportConsumer.LifeSupportDefinition.AcceptanceThreshold, _dataLifeSupportConsumer.RequestConfig);
        resourceFlowRequestBroker.SetRequestActive(_dataLifeSupportConsumer.RequestHandle);
    }

    private void RefreshIngredientDataStructures()
    {
        var inputCount = _dataLifeSupportConsumer.LifeSupportDefinition.InputResources.Count;
        var outputCount = _dataLifeSupportConsumer.LifeSupportDefinition.OutputResources.Count;

        _currentIngredientUnits = new ResourceUnitsPair[inputCount];
        _currentProductUnits = new ResourceUnitsPair[outputCount];

        var resourceUnitsPair = new ResourceUnitsPair();
        for (var i = 0; i < inputCount; ++i)
        {
            var scale = KerbalLifeSupportSystemPlugin.Instance.ConsumptionRates[
                _dataLifeSupportConsumer.LifeSupportDefinition.InputResources[i].ResourceName].Value;
            resourceUnitsPair.resourceID = _dataLifeSupportConsumer.ResourceDefinitions[i];
            resourceUnitsPair.units = _dataLifeSupportConsumer.LifeSupportDefinition.InputResources[i].Rate *
                                      _dataLifeSupportConsumer.numKerbals * scale;
            _currentIngredientUnits[i] = resourceUnitsPair;
        }

        for (var i = 0; i < outputCount; ++i)
        {
            var outputName = _dataLifeSupportConsumer.LifeSupportDefinition.OutputResources[i].ResourceName;
            var inputName = KerbalLifeSupportSystemPlugin.Instance.LsOutputInputNames[outputName];
            var scale = KerbalLifeSupportSystemPlugin.Instance.ConsumptionRates[inputName].Value;
            resourceUnitsPair.resourceID = _dataLifeSupportConsumer.ResourceDefinitions[inputCount + i];
            resourceUnitsPair.units = _dataLifeSupportConsumer.LifeSupportDefinition.OutputResources[i].Rate *
                                      _dataLifeSupportConsumer.numKerbals * scale;
            _currentProductUnits[i] = resourceUnitsPair;
        }
    }

    private void SetResourceRates()
    {
        var inputCount = _dataLifeSupportConsumer.LifeSupportDefinition.InputResources.Count;

        for (var i = 0; i < _dataLifeSupportConsumer.RequestConfig.Length; ++i)
            _dataLifeSupportConsumer.RequestConfig[i].FlowUnits =
                i >= inputCount
                    ? _currentProductUnits[i - inputCount].units
                    : _currentIngredientUnits[i].units;
    }

    private void UpdateKerbalsStatus(double universalTime)
    {
        foreach (var kerbal in _kerbalsInSimObject)
        foreach (var ingredient in _currentIngredientUnits)
            if (ResourceExhausted(kerbal, ingredient, universalTime))
            {
                KerbalLifeSupportSystemPlugin.Logger.LogInfo("Kerbal " + kerbal.NameKey + " ran out of life-support.");
                _rosterManager.DestroyKerbal(kerbal.Id);
                break;
            }
    }

    /// <summary>
    ///     Checks if any life-support resource has been exhausted for the Kerbal for longer than the allowed grace period
    /// </summary>
    /// <param name="kerbal">The Kerbal in question</param>
    /// <param name="resource">The life-support resource</param>
    /// <param name="time">Current universal time</param>
    private bool ResourceExhausted(KerbalInfo kerbal, ResourceUnitsPair resource, double time)
    {
        if (!_dataLifeSupportConsumer.lastConsumed.TryGetValue(kerbal.NameKey, out var lastCons)) return false;

        // Get resource name
        var resourceName = _resourceDB.GetResourceNameFromID(resource.resourceID);

        // For how long the resource was exhausted
        if (!lastCons.ContainsKey(resourceName)) return false;
        var timeDelta = time - lastCons[resourceName];

        // Return true if exhausted for longer than the allowed grace period for this resource
        return timeDelta > KerbalLifeSupportSystemPlugin.Instance.LsGracePeriods[resourceName];
    }

    /// <summary>
    ///     Handles the transfer of resources to Kerbals going on EVA and from those returning from EVA
    /// </summary>
    private void OnKerbalLocationChanged(MessageCenterMessage msg)
    {
        if (msg is not KerbalLocationChanged kerbalLocationChanged)
            return;

        // Old location
        var oldLocationId = kerbalLocationChanged.OldLocation.SimObjectId;
        var oldSimObject = Game.UniverseModel.FindSimObject(oldLocationId);

        // New location
        var newLocationId = kerbalLocationChanged.Kerbal.Location.SimObjectId;
        var newSimObject = Game.UniverseModel.FindSimObject(newLocationId);

        if (oldLocationId.Equals(Part.SimulationObject.GlobalId) && newSimObject.IsPart &&
            newSimObject.Part.PartOwner.SimulationObject.IsKerbal)
            // Kerbal left the current part to go on EVA
        {
            // Number of Kerbals remaining in the part
            var remainingKerbals = _rosterManager.GetAllKerbalsInSimObject(Part.SimulationObject.GlobalId).Count;

            // Kerbal resource container group
            var kerbalContainerGroup = newSimObject.Part.PartOwner.ContainerGroup;

            for (var i = 0; i < _dataLifeSupportConsumer.LifeSupportDefinition.InputResources.Count; ++i)
            {
                var resourceID = _dataLifeSupportConsumer.ResourceDefinitions[i];

                // Split the remaining resources evenly between the EVA Kerbal & the Kerbals remaining in the part
                var resourceUnits =
                    Math.Min(_containerGroup.GetResourceStoredUnits(resourceID) / (1 + remainingKerbals),
                        kerbalContainerGroup.GetResourceCapacityUnits(resourceID));

                _containerGroup.RemoveResourceUnits(resourceID, resourceUnits);
                kerbalContainerGroup.AddResourceUnits(resourceID, resourceUnits);
            }
        }
        else if (newLocationId.Equals(Part.SimulationObject.GlobalId) && oldSimObject.IsPart &&
                 oldSimObject.Part.PartOwner.SimulationObject.IsKerbal)
            // Kerbal entered the current part from EVA
        {
            // Kerbal resource container group
            var kerbalContainerGroup = oldSimObject.Part.PartOwner.ContainerGroup;

            // Resource request configs setup
            for (var i = 0; i < _dataLifeSupportConsumer.LifeSupportDefinition.InputResources.Count; ++i)
            {
                var resourceID = _dataLifeSupportConsumer.ResourceDefinitions[i];

                _containerGroup.AddResourceUnits(resourceID, kerbalContainerGroup.GetResourceStoredUnits(resourceID));
            }
        }
    }
}