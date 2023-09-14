using KSP.Game;
using KSP.Messages;
using KSP.Sim;
using KSP.Sim.impl;
using KSP.Sim.ResourceSystem;

namespace KerbalLifeSupportSystem.Modules
{
    public class PartComponentModule_LifeSupportConsumer : PartComponentModule
    {
        private static int SECONDS_PER_HOUR = 3600;
        private static int SECONDS_PER_DAY = 86400;
        private static int FOOD_GRACE_PERIOD = 30 * SECONDS_PER_DAY;
        private static int WATER_GRACE_PERIOD = 3 * SECONDS_PER_DAY;
        private static int OXYGEN_GRACE_PERIOD = 2 * SECONDS_PER_HOUR;

        private FlowRequestResolutionState _returnedRequestResolution;
        private bool _hasPendingRequests;
        private ResourceUnitsPair[] _currentIngredientUnits;
        private ResourceUnitsPair[] _currentProductUnits;
        private ResourceContainerGroup _containerGroup;
        private ResourceDefinitionDatabase _resourceDB;
        private KerbalRosterManager _rosterManager;

        protected PartComponentModule_Command _moduleCommand;
        private int _commandMinCrew;
        private List<KerbalInfo> _kerbalsInSimObject = new();
        private HashSet<IGGuid> _kerbalsOnStrike = new();
        protected Data_LifeSupportConsumer _dataLifeSupportConsumer;

        public override Type PartBehaviourModuleType => typeof(Module_LifeSupportConsumer);

        public override void OnStart(double universalTime)
        {
            if (!DataModules.TryGetByType<Data_LifeSupportConsumer>(out _dataLifeSupportConsumer))
                KerbalLifeSupportSystemPlugin.Logger.LogError("Unable to find a Data_LifeSupportConsumer in the PartComponentModule for " + Part.PartName);
            else if (GameManager.Instance.Game == null || GameManager.Instance.Game.ResourceDefinitionDatabase == null)
            {
                KerbalLifeSupportSystemPlugin.Logger.LogError("Unable to find a valid game with a resource definition database");
            }
            else if (!Part.TryGetModule<PartComponentModule_Command>(out _moduleCommand))
                KerbalLifeSupportSystemPlugin.Logger.LogError("Unable to find an attached PartComponentModule_Command for " + Part.PartName);
            else
            {
                Game.Messages.Subscribe<KerbalLocationChanged>(new Action<MessageCenterMessage>(OnKerbalLocationChanged));
                _commandMinCrew = _moduleCommand.dataCommand.minimumCrew;
                _dataLifeSupportConsumer.SetupResourceRequest(resourceFlowRequestBroker);
                _containerGroup = Part.PartOwner.ContainerGroup;
                _resourceDB = GameManager.Instance.Game.ResourceDefinitionDatabase;
                _rosterManager = GameManager.Instance.Game.SessionManager.KerbalRosterManager;
                resourceFlowRequestBroker.SetCommands(_dataLifeSupportConsumer.RequestHandle, _dataLifeSupportConsumer.LifeSupportDefinition.AcceptanceThreshold, _dataLifeSupportConsumer.RequestConfig);
                resourceFlowRequestBroker.SetRequestActive(_dataLifeSupportConsumer.RequestHandle);
                RefreshIngredientDataStructures();
                SetResourceRates();
            }
        }

        public override void OnShutdown()
        {
            RemoveResourceRequest(_dataLifeSupportConsumer.RequestHandle);
        }

        public override void OnUpdate(double universalTime, double deltaUniversalTime)
        {
            _dataLifeSupportConsumer.numKerbals = Part._currentKerbalCountTotal;

            if (_dataLifeSupportConsumer.numKerbals > 0)
            {
                _kerbalsInSimObject = _rosterManager.GetAllKerbalsInSimObject(Part.SimulationObject.GlobalId);
                foreach (KerbalInfo kerbal in _kerbalsInSimObject)
                {
                    _kerbalsOnStrike.Add(kerbal.Id);
                }
                RefreshIngredientDataStructures();
                SendResourceRequest();
                if (_hasPendingRequests)
                {
                    _returnedRequestResolution = resourceFlowRequestBroker.GetRequestState(_dataLifeSupportConsumer.RequestHandle);
                    if (_returnedRequestResolution.WasLastTickDeliveryAccepted)
                    {
                        foreach (ResourceUnitsPair ingredient in _currentIngredientUnits)
                        {
                            if (_containerGroup.GetResourceStoredUnits(ingredient.resourceID) > _dataLifeSupportConsumer.LifeSupportDefinition.AcceptanceThreshold)
                                foreach (KerbalInfo kerbal in _kerbalsInSimObject)
                                {
                                    if (!_dataLifeSupportConsumer.lastConsumed.ContainsKey(kerbal.Id))
                                        _dataLifeSupportConsumer.lastConsumed[kerbal.Id] = new Dictionary<ResourceDefinitionID, double>();
                                    _dataLifeSupportConsumer.lastConsumed[kerbal.Id][ingredient.resourceID] = universalTime;
                                }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < _currentProductUnits.Length; ++i)
                        {
                            if (_containerGroup.GetResourceCapacityUnits(_currentProductUnits[i].resourceID) < _dataLifeSupportConsumer.LifeSupportDefinition.AcceptanceThreshold)
                                _currentProductUnits[i].units = 0.0;
                        }
                        for (int i = 0; i < _currentIngredientUnits.Length; ++i)
                        {
                            if (_containerGroup.GetResourceStoredUnits(_currentIngredientUnits[i].resourceID) < _dataLifeSupportConsumer.LifeSupportDefinition.AcceptanceThreshold)
                                _currentIngredientUnits[i].units = 0.0;
                        }
                        SendResourceRequest();
                    }
                    _hasPendingRequests = false;
                }
                _hasPendingRequests = true;

                UpdateKerbalsStatus(universalTime);
            }
        }

        private void SendResourceRequest()
        {
            SetResourceRates();
            resourceFlowRequestBroker.SetCommands(_dataLifeSupportConsumer.RequestHandle, _dataLifeSupportConsumer.LifeSupportDefinition.AcceptanceThreshold, _dataLifeSupportConsumer.RequestConfig);
            resourceFlowRequestBroker.SetRequestActive(_dataLifeSupportConsumer.RequestHandle);
        }

        private void RefreshIngredientDataStructures()
        {
            _currentIngredientUnits = new ResourceUnitsPair[_dataLifeSupportConsumer.LifeSupportDefinition.InputResources.Count];
            _currentProductUnits = new ResourceUnitsPair[_dataLifeSupportConsumer.LifeSupportDefinition.OutputResources.Count];
            ResourceUnitsPair resourceUnitsPair = new ResourceUnitsPair();
            for (int i = 0; i < _dataLifeSupportConsumer.LifeSupportDefinition.InputResources.Count; ++i)
            {
                resourceUnitsPair.resourceID = _dataLifeSupportConsumer.ResourceDefinitions[i];
                float scale = KerbalLifeSupportSystemPlugin.Instance.ConfigResourceConsumptionRate.Value;
                resourceUnitsPair.units = _dataLifeSupportConsumer.LifeSupportDefinition.InputResources[i].Rate * _dataLifeSupportConsumer.numKerbals * scale;
                _currentIngredientUnits[i] = resourceUnitsPair;
            }
            for (int i = 0; i < _dataLifeSupportConsumer.LifeSupportDefinition.OutputResources.Count; ++i)
            {
                resourceUnitsPair.resourceID = _dataLifeSupportConsumer.ResourceDefinitions[_dataLifeSupportConsumer.LifeSupportDefinition.InputResources.Count + i];
                float scale = KerbalLifeSupportSystemPlugin.Instance.ConfigResourceConsumptionRate.Value;
                resourceUnitsPair.units = _dataLifeSupportConsumer.LifeSupportDefinition.OutputResources[i].Rate * _dataLifeSupportConsumer.numKerbals * scale;
                _currentProductUnits[i] = resourceUnitsPair;
            }
        }

        private void SetResourceRates()
        {
            for (int i = 0; i < _dataLifeSupportConsumer.RequestConfig.Length; ++i)
            {
                if (i >= _dataLifeSupportConsumer.LifeSupportDefinition.InputResources.Count)
                    _dataLifeSupportConsumer.RequestConfig[i].FlowUnits = _currentProductUnits[i - _dataLifeSupportConsumer.LifeSupportDefinition.InputResources.Count].units;
                else
                    _dataLifeSupportConsumer.RequestConfig[i].FlowUnits = _currentIngredientUnits[i].units;
            }
        }

        private void UpdateKerbalsStatus(double universalTime)
        {
            foreach (KerbalInfo kerbal in _kerbalsInSimObject)
            {
                bool anyResourceExhausted = false;
                foreach (ResourceUnitsPair ingredient in _currentIngredientUnits)
                {
                    if (anyResourceExhausted)
                        break;
                    if (_dataLifeSupportConsumer.lastConsumed.ContainsKey(kerbal.Id))
                    {
                        var timeDelta = universalTime - _dataLifeSupportConsumer.lastConsumed[kerbal.Id][ingredient.resourceID];
                        string resourceName = _resourceDB.GetResourceNameFromID(ingredient.resourceID);
                        var gracePeriod = resourceName switch
                        {
                            "Food" => FOOD_GRACE_PERIOD,
                            "Water" => WATER_GRACE_PERIOD,
                            "Oxygen" => OXYGEN_GRACE_PERIOD,
                            _ => SECONDS_PER_DAY
                        };
                        anyResourceExhausted = timeDelta > gracePeriod;
                    }
                }

                if (anyResourceExhausted)
                {
                    if (KerbalLifeSupportSystemPlugin.Instance.ConfigKerbalsDie.Value)
                        _rosterManager.DestroyKerbal(kerbal.Id);
                    else
                        _kerbalsOnStrike.Add(kerbal.Id);
                }
                else
                    _kerbalsOnStrike.Remove(kerbal.Id);

                _moduleCommand.dataCommand.minimumCrew = _commandMinCrew - _kerbalsOnStrike.Count;
                _moduleCommand.UpdateKerbalControlStatus();
                _moduleCommand.UpdateControlStatus();
            }
        }

        private void OnKerbalLocationChanged(MessageCenterMessage msg)
        {
            if (msg is not KerbalLocationChanged kerbalLocationChanged)
                return;
            IGGuid simObjectId = kerbalLocationChanged.OldLocation.SimObjectId;
        }
    }
}
