using KSP.Modules;
using KSP.Sim;
using KSP.Sim.Definitions;
using UnityEngine;

// ReSharper disable InconsistentNaming

namespace KerbalLifeSupportSystem.Modules;

[Serializable]
public class Data_LifeSupportConsumer : ModuleData
{
    [KSPDefinition] [Tooltip("Life support resources definition (for 1 Kerbal)")]
    public ResourceConverterFormulaDefinition LifeSupportDefinition;

    // Number of Kerbals currently in the part
    public int numKerbals;

    [KSPState]
    [HideInInspector]
    [Tooltip(
        "Last time each input resource was consumed for each kerbal currently in the vessel. Structure: {KerbalName: {ResourceName: UT}}.")]
    public Dictionary<string, Dictionary<string, double>> lastConsumed = new();

    [KSPState]
    [HideInInspector]
    [Tooltip(
        "Was 'exhausted' notification sent for each input resource for each kerbal currently in the vessel. Structure: {KerbalName: {ResourceName: bool}}")]
    public Dictionary<string, Dictionary<string, bool>> exhaustNotificationSent = new();

    [KSPState]
    [HideInInspector]
    [Tooltip("Was 'kerbal went on strike' notification already sent. Structure: {KerbalName: bool}")]
    public Dictionary<string, bool> strikeNotificationSent = new();

    [KSPState] [HideInInspector] [Tooltip("Set of Kerbals currently on strike due to lack of supplies.")]
    public HashSet<string> kerbalsOnStrike = [];

    public override Type ModuleType => typeof(Module_LifeSupportConsumer);
}