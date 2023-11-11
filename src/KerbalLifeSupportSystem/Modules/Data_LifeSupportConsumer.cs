using KSP.Modules;
using KSP.Sim;
using KSP.Sim.Definitions;
using KSP.Sim.ResourceSystem;
using UnityEngine;

namespace KerbalLifeSupportSystem.Modules;

[Serializable]
public class Data_LifeSupportConsumer : ModuleData
{
    [KSPDefinition] [Tooltip("Life support resources definition (for 1 Kerbal)")]
    public ResourceConverterFormulaDefinition LifeSupportDefinition;

    // Number of Kerbals currently in the part
    public int numKerbals;

    // Resource IDs for the input & output resources
    public ResourceDefinitionID[] ResourceDefinitions;

    // Game's resources database
    private ResourceDefinitionDatabase _resourceDB;

    [KSPState]
    [HideInInspector]
    [Tooltip("Last time each input resource was consumed for each kerbal currently in the vessel.")]
    public Dictionary<string, Dictionary<string, double>> lastConsumed = new();

    // Resource Request Configs for the input & output resources
    public ResourceFlowRequestCommandConfig[] RequestConfig;

    public override Type ModuleType => typeof(Module_LifeSupportConsumer);

    public override void SetupResourceRequest(ResourceFlowRequestBroker resourceFlowRequestBroker)
    {
        // Get the game's resource database
        _resourceDB = Game.ResourceDefinitionDatabase;

        // Total number of handled resources
        var length = LifeSupportDefinition.InputResources.Count + LifeSupportDefinition.OutputResources.Count;

        // Initialize resource definitions and resource requests
        ResourceDefinitions = new ResourceDefinitionID[length];
        RequestConfig = new ResourceFlowRequestCommandConfig[length];

        // Set up input resources
        for (var i = 0; i < LifeSupportDefinition.InputResources.Count; ++i)
        {
            // Resource Name & ID
            var resourceName = LifeSupportDefinition.InputResources[i].ResourceName;
            var resourceIdFromName = _resourceDB.GetResourceIDFromName(resourceName);

            if (resourceIdFromName == ResourceDefinitionID.InvalidID) continue;

            // Consumption rate setting
            var rate = KerbalLifeSupportSystemPlugin.Instance.ConsumptionRates[resourceName].Value;

            ResourceDefinitions[i] = resourceIdFromName;
            RequestConfig[i] = new ResourceFlowRequestCommandConfig
            {
                FlowResource = resourceIdFromName,
                FlowDirection = FlowDirection.FLOW_OUTBOUND,
                FlowUnits = (double)LifeSupportDefinition.InputResources[i].Rate * numKerbals * rate
            };
        }

        // Set up output resources
        for (var i = LifeSupportDefinition.InputResources.Count; i < length; ++i)
        {
            // Shifted index for output resource
            var index = i - LifeSupportDefinition.InputResources.Count;

            // Resource Name & ID
            var resourceName = LifeSupportDefinition.OutputResources[index].ResourceName;
            var resourceIdFromName = _resourceDB.GetResourceIDFromName(resourceName);

            if (resourceIdFromName == ResourceDefinitionID.InvalidID) continue;

            // Consumption rate setting from corresponding input resource
            var inputName = KerbalLifeSupportSystemPlugin.Instance.LsOutputInputNames[resourceName];
            var rate = KerbalLifeSupportSystemPlugin.Instance.ConsumptionRates[inputName].Value;

            ResourceDefinitions[i] = resourceIdFromName;
            RequestConfig[i] = new ResourceFlowRequestCommandConfig
            {
                FlowResource = resourceIdFromName,
                FlowDirection = FlowDirection.FLOW_INBOUND,
                FlowUnits = (double)LifeSupportDefinition.OutputResources[index].Rate * rate
            };
        }

        // Set up resource request
        RequestHandle = resourceFlowRequestBroker.AllocateOrGetRequest("LifeSupport");
        resourceFlowRequestBroker.SetCommands(RequestHandle, 1.0, RequestConfig);
    }
}