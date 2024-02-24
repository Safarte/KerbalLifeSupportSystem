// ReSharper disable InconsistentNaming

using KerbalLifeSupportSystem.Modules;
using KSP.Game;
using KSP.Modules;
using KSP.Sim.impl;
using Premonition.Core.Attributes;
using UniLinq;


namespace KerbalLifeSupportSystem.Utilities;

[PremonitionAssembly("Assembly-CSharp")]
[PremonitionType("KSP.Sim.impl.PartComponentModule_Command")]
public class PartComponentModule_Command_Patches
{
    [PremonitionMethod("UpdateKerbalControlStatus")]
    [PremonitionPostfix]
    public static void UpdateKerbalControlStatus(PartComponentModule_Command __instance)
    {
        if (__instance.Part.TryGetModuleData<PartComponentModule_LifeSupportConsumer, Data_LifeSupportConsumer>(
                out var consumer))
        {
            int count = GameManager.Instance.Game.SessionManager.KerbalRosterManager
                            .GetAllKerbalsInSimObject(__instance.Part.SimulationObject.GlobalId).Count -
                        consumer.kerbalsOnStrike.Count;

            KerbalLifeSupportSystemPlugin.Logger.LogInfo($"{count} working Kerbals in the vessel.");

            // We check if (kerbalCount - kerbalOnStrikeCount) >= minimumCrew
            __instance._hasCrewToOperate =
                __instance.dataCommand.minimumCrew == 0 || count >= __instance.dataCommand.minimumCrew;
        }
    }
}

[PremonitionAssembly("Assembly-CSharp")]
[PremonitionType("KSP.Sim.impl.PartComponentModule_ScienceExperiment")]
public class PartComponentModule_ScienceExperiment_Patches
{
    [PremonitionMethod("IsExperimentAllowed")]
    [PremonitionPostfix]
    public static bool IsExperimentAllowed(bool __retVal, int experimentIndex, bool notify,
        PartComponentModule_ScienceExperiment __instance)
    {
        // If experiment already wasn't possible, we do not need to do anything
        if (!__retVal)
            return false;

        // Get the part's LifeSupportConsumer module
        if (!__instance.Part
                .TryGetModuleData<PartComponentModule_LifeSupportConsumer, Data_LifeSupportConsumer>(
                    out var consumer)) return true;

        var experiment = __instance.dataScienceExperiment.Experiments[experimentIndex];

        // We check if (kerbalCount - kerbalOnStrikeCount) >= CrewRequired
        var hasEnoughKerbals = __instance._kerbalRosterManager != null && experiment.CrewRequired <=
            __instance._kerbalRosterManager.GetAllKerbalsInSimObject(__instance.Part.SimulationObject.GlobalId)
                .Count - consumer.kerbalsOnStrike.Count;

        if (hasEnoughKerbals) return true;

        // Stock experiment status handling
        __instance.SetExperimentState(experimentIndex, ExperimentState.INSUFFICIENTCREW);
        __instance.dataScienceExperiment.ExperimentStandings[experimentIndex].CurrentExperimentContext =
            experiment.CrewRequired.ToString();
        if (__instance.dataScienceExperiment.ExperimentStandings[experimentIndex].PreviousExperimentState !=
            ExperimentState.INSUFFICIENTCREW)
            __instance.TrySendStateChangeMessage(experimentIndex,
                notify && __instance.dataScienceExperiment.ExperimentStandings[experimentIndex]
                    .CurrentExperimentState == ExperimentState.RUNNING);

        return false;
    }
}