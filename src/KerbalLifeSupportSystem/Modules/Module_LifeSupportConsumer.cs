using KSP.Sim.Definitions;
using KSP.Sim.ResourceSystem;
using UnityEngine;

namespace KerbalLifeSupportSystem.Modules;

[DisallowMultipleComponent]
public class Module_LifeSupportConsumer : PartBehaviourModule
{
    [SerializeField] protected Data_LifeSupportConsumer _dataLifeSupportConsumer;

    public override Type PartComponentModuleType => typeof(PartComponentModule_LifeSupportConsumer);

    public override void AddDataModules()
    {
        base.AddDataModules();
        _dataLifeSupportConsumer ??= new Data_LifeSupportConsumer();
        DataModules.TryAddUnique(_dataLifeSupportConsumer, out _dataLifeSupportConsumer);
    }

    public override void OnInitialize()
    {
        base.OnInitialize();
        if (PartBackingMode == PartBackingModes.Flight) moduleIsEnabled = true;
    }

    public override void OnModuleOABFixedUpdate(float deltaTime)
    {
        if ((OABPart == null || !(_dataLifeSupportConsumer.RequestHandle == ResourceFlowRequestHandle.InvalidID))
            && (resourceFlowRequestBroker == null ||
                resourceFlowRequestBroker.TryGetCurrentRequest(_dataLifeSupportConsumer.RequestHandle, out var _)))
            return;
        _dataLifeSupportConsumer.SetupResourceRequest(resourceFlowRequestBroker);
    }
}