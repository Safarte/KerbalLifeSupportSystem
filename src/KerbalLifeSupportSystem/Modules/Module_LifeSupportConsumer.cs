using KSP.Sim.Definitions;
using UnityEngine;

// ReSharper disable InconsistentNaming

namespace KerbalLifeSupportSystem.Modules;

[DisallowMultipleComponent]
public class Module_LifeSupportConsumer : PartBehaviourModule
{
    [SerializeField] protected Data_LifeSupportConsumer _dataLifeSupportConsumer;

    public override Type PartComponentModuleType => typeof(PartComponentModule_LifeSupportConsumer);

    protected override void AddDataModules()
    {
        base.AddDataModules();
        _dataLifeSupportConsumer ??= new Data_LifeSupportConsumer();
        DataModules.TryAddUnique(_dataLifeSupportConsumer, out _dataLifeSupportConsumer);
    }

    protected override void OnInitialize()
    {
        base.OnInitialize();
        if (PartBackingMode == PartBackingModes.Flight) moduleIsEnabled = true;
    }
}