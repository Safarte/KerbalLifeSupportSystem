using KSP.Game;
using KSP.Messages;
using KSP.Sim;
using KSP.Sim.impl;
using KSP.Sim.ResourceSystem;

// ReSharper disable InconsistentNaming

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

    // List of Kerbals in the part
    private List<KerbalInfo> _kerbalsInSimObject = [];

    // Useful game objects
    private NotificationManager _notificationManager;
    private ResourceDefinitionDatabase _resourceDB;
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
            _containerGroup = Part.PartOwner.ContainerGroup;
            _notificationManager = GameManager.Instance.Game.Notifications;
            _resourceDB = GameManager.Instance.Game.ResourceDefinitionDatabase;
            _rosterManager = GameManager.Instance.Game.SessionManager.KerbalRosterManager;

            // Set up resource request
            SetupIngredientDataStructures();

            // Kerbal EVA handling
            Game.Messages.Subscribe<KerbalLocationChanged>(OnKerbalLocationChanged);
        }
    }

    public override void OnShutdown()
    {
        Game.Messages.Unsubscribe<KerbalLocationChanged>(OnKerbalLocationChanged);
    }

    public override void OnUpdate(double universalTime, double deltaUniversalTime)
    {
        _kerbalsInSimObject = _rosterManager.GetAllKerbalsInSimObject(Part.SimulationObject.GlobalId);
        _dataLifeSupportConsumer.numKerbals = _kerbalsInSimObject.Count;

        if (_dataLifeSupportConsumer.numKerbals <= 0) return;

        // Initialize the last consumed database if needed
        foreach (var kerbal in _kerbalsInSimObject)
        {
            if (!_dataLifeSupportConsumer.lastConsumed.ContainsKey(kerbal.NameKey))
                _dataLifeSupportConsumer.lastConsumed[kerbal.NameKey] = new Dictionary<string, double>();
            if (!_dataLifeSupportConsumer.exhaustNotificationSent.ContainsKey(kerbal.NameKey))
                _dataLifeSupportConsumer.exhaustNotificationSent[kerbal.NameKey] = new Dictionary<string, bool>();
        }

        UpdateIngredients();
        SendResourceRequest(deltaUniversalTime);
        UpdateLastConsumed(universalTime);
        UpdateKerbalsStatus(universalTime);
    }

    /// <summary>
    ///     Update ingredient and product data structures
    /// </summary>
    private void UpdateIngredients()
    {
        // Products
        for (var i = 0; i < _currentProductUnits.Length; ++i)
        {
            // Find product resource scale setting
            var outputName = _dataLifeSupportConsumer.LifeSupportDefinition.OutputResources[i].ResourceName;
            var inputName = KerbalLifeSupportSystemPlugin.Instance.LsOutputInputNames[outputName];
            var scale = KerbalLifeSupportSystemPlugin.Instance.ConsumptionRates[inputName].Value;

            // Remove product from request if container full
            _currentProductUnits[i].units =
                _containerGroup.GetResourceCapacityUnits(_currentProductUnits[i].resourceID) <
                _dataLifeSupportConsumer.LifeSupportDefinition.AcceptanceThreshold
                    ? 0.0
                    : _dataLifeSupportConsumer.LifeSupportDefinition.OutputResources[i].Rate *
                      _dataLifeSupportConsumer.numKerbals * scale;
        }

        // Ingredients
        for (var i = 0; i < _currentIngredientUnits.Length; ++i)
        {
            // Find ingredient resource scale setting
            var scale = KerbalLifeSupportSystemPlugin.Instance.ConsumptionRates[
                _dataLifeSupportConsumer.LifeSupportDefinition.InputResources[i].ResourceName].Value;

            // Remove ingredient from request if container empty
            _currentIngredientUnits[i].units =
                _containerGroup.GetResourceStoredUnits(_currentIngredientUnits[i].resourceID) <
                _dataLifeSupportConsumer.LifeSupportDefinition.AcceptanceThreshold
                    ? 0.0
                    : _currentIngredientUnits[i].units =
                        _dataLifeSupportConsumer.LifeSupportDefinition.InputResources[i].Rate *
                        _dataLifeSupportConsumer.numKerbals * scale;
        }
    }

    /// <summary>
    ///     Update the Data_LifeSupportConsumer.lastConsumed data structure based on remaining supplies
    /// </summary>
    /// <param name="time"></param>
    private void UpdateLastConsumed(double time)
    {
        foreach (var ingredient in _currentIngredientUnits)
        {
            var resourceName = _resourceDB.GetResourceNameFromID(ingredient.resourceID);

            // If resource is not exhausted, set last consumed to given time
            if (_containerGroup.GetResourceStoredUnits(ingredient.resourceID) >
                _dataLifeSupportConsumer.LifeSupportDefinition.AcceptanceThreshold)
                foreach (var kerbal in _kerbalsInSimObject)
                    _dataLifeSupportConsumer.lastConsumed[kerbal.NameKey][resourceName] = time;
        }
    }

    /// <summary>
    ///     Consume ingredients and produce products based on the current consumption data structures and elapsed time
    /// </summary>
    /// <param name="deltaTime">Elapsed universal time</param>
    private void SendResourceRequest(double deltaTime)
    {
        var inputCount = _dataLifeSupportConsumer.LifeSupportDefinition.InputResources.Count;
        var outputCount = _dataLifeSupportConsumer.LifeSupportDefinition.OutputResources.Count;

        // Ingredients
        for (var i = 0; i < inputCount; ++i)
            _containerGroup.RemoveResourceUnits(_currentIngredientUnits[i].resourceID, _currentIngredientUnits[i].units,
                deltaTime);

        // Products
        for (var i = 0; i < outputCount; ++i)
            _containerGroup.AddResourceUnits(_currentProductUnits[i].resourceID, _currentProductUnits[i].units,
                deltaTime);
    }

    /// <summary>
    ///     Setup the data structures storing the ingredients and products for life-support on the part
    /// </summary>
    private void SetupIngredientDataStructures()
    {
        var inputCount = _dataLifeSupportConsumer.LifeSupportDefinition.InputResources.Count;
        var outputCount = _dataLifeSupportConsumer.LifeSupportDefinition.OutputResources.Count;

        _currentIngredientUnits = new ResourceUnitsPair[inputCount];
        _currentProductUnits = new ResourceUnitsPair[outputCount];

        var resourceUnitsPair = new ResourceUnitsPair();
        for (var i = 0; i < inputCount; ++i)
        {
            // Resource name
            var inputName = _dataLifeSupportConsumer.LifeSupportDefinition.InputResources[i].ResourceName;

            // Consumption scale setting
            var scale = KerbalLifeSupportSystemPlugin.Instance.ConsumptionRates[inputName].Value;

            // Setup the resource
            resourceUnitsPair.resourceID = _resourceDB.GetResourceIDFromName(inputName);
            resourceUnitsPair.units = _dataLifeSupportConsumer.LifeSupportDefinition.InputResources[i].Rate *
                                      _dataLifeSupportConsumer.numKerbals * scale;
            _currentIngredientUnits[i] = resourceUnitsPair;
        }

        for (var i = 0; i < outputCount; ++i)
        {
            // Resource name
            var outputName = _dataLifeSupportConsumer.LifeSupportDefinition.OutputResources[i].ResourceName;

            // Get consumption scale from corresponding input resource
            var inputName = KerbalLifeSupportSystemPlugin.Instance.LsOutputInputNames[outputName];
            var scale = KerbalLifeSupportSystemPlugin.Instance.ConsumptionRates[inputName].Value;

            // Setup resource
            resourceUnitsPair.resourceID = _resourceDB.GetResourceIDFromName(outputName);
            resourceUnitsPair.units = _dataLifeSupportConsumer.LifeSupportDefinition.OutputResources[i].Rate *
                                      _dataLifeSupportConsumer.numKerbals * scale;
            _currentProductUnits[i] = resourceUnitsPair;
        }
    }

    /// <summary>
    ///     Updates the status of all Kerbals in the part based on the last time they consumed LS resources
    /// </summary>
    /// <param name="universalTime">Current universal time</param>
    private void UpdateKerbalsStatus(double universalTime)
    {
        foreach (var kerbal in _kerbalsInSimObject)
        foreach (var ingredient in _currentIngredientUnits)
            if (ResourceExhausted(kerbal, ingredient, universalTime))
            {
                KerbalLifeSupportSystemPlugin.Logger.LogInfo("Kerbal " + kerbal.NameKey + " ran out of life-support.");
                if (Part.PartOwner.SimulationObject.IsKerbal)
                    Part.PartOwner.SimulationObject.Destroy(universalTime);
                else
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

        if (timeDelta > KerbalLifeSupportSystemPlugin.Instance.LsGracePeriods[resourceName])
            NotifyKerbalDied(time, kerbal.NameKey, resourceName);

        // Return true if exhausted for longer than the allowed grace period for this resource
        return timeDelta > KerbalLifeSupportSystemPlugin.Instance.LsGracePeriods[resourceName];
    }

    private void NotifyKerbalDied(double universalTime, string kerbalName, string resourceName)
    {
        _notificationManager.ProcessNotification(new NotificationData
        {
            Tier = NotificationTier.Alert,
            Importance = NotificationImportance.High,
            AlertTitle = new NotificationLineItemData
            {
                ObjectParams = [Part.PartOwner.SimulationObject.Vessel.Name, resourceName],
                LocKey = "KLSS/Notifications/ResourceExhausted"
            },
            TimeStamp = universalTime,
            FirstLine = new NotificationLineItemData
            {
                LocKey = kerbalName
            }
        });
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

            // Transfer resources to new EVA Kerbal
            for (var i = 0; i < _dataLifeSupportConsumer.LifeSupportDefinition.InputResources.Count; ++i)
            {
                var resourceID = _currentIngredientUnits[i].resourceID;

                // Split the remaining resources evenly between the EVA Kerbal & the Kerbals remaining in the part
                var resourceUnits =
                    Math.Min(_containerGroup.GetResourceStoredUnits(resourceID) / (1 + remainingKerbals),
                        kerbalContainerGroup.GetResourceCapacityUnits(resourceID));

                _containerGroup.RemoveResourceUnits(resourceID, resourceUnits);
                kerbalContainerGroup.AddResourceUnits(resourceID, resourceUnits);
            }

            // Initialize new object's lastConsumed
            if (newSimObject.Part
                .TryGetModuleData<PartComponentModule_LifeSupportConsumer, Data_LifeSupportConsumer>(out var data))
            {
                var evaKerbalName = _rosterManager.GetAllKerbalsInSimObject(newLocationId)[0].NameKey;
                data.lastConsumed[evaKerbalName] = _dataLifeSupportConsumer.lastConsumed[evaKerbalName];
            }
        }
        else if (newLocationId.Equals(Part.SimulationObject.GlobalId) && oldSimObject.IsPart &&
                 oldSimObject.Part.PartOwner.SimulationObject.IsKerbal)
            // Kerbal entered the current part from EVA
        {
            // Kerbal resource container group
            var kerbalContainerGroup = oldSimObject.Part.PartOwner.ContainerGroup;

            // Transfer all resources remaining on the Kerbal to the new vessel
            for (var i = 0; i < _dataLifeSupportConsumer.LifeSupportDefinition.InputResources.Count; ++i)
            {
                var resourceID = _currentIngredientUnits[i].resourceID;

                _containerGroup.AddResourceUnits(resourceID, kerbalContainerGroup.GetResourceStoredUnits(resourceID));
            }

            // Transfer Kerbal lastConsumed
            if (oldSimObject.Part
                .TryGetModuleData<PartComponentModule_LifeSupportConsumer, Data_LifeSupportConsumer>(out var data))
            {
                var evaKerbalName = _rosterManager.GetAllKerbalsInSimObject(oldLocationId)[0].NameKey;
                _dataLifeSupportConsumer.lastConsumed[evaKerbalName] = data.lastConsumed[evaKerbalName];
            }
        }
    }
}