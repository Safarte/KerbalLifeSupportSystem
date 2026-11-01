// ReSharper disable InconsistentNaming

using HarmonyLib;
using KerbalLifeSupportSystem.Modules;
using KSP.Game;
using KSP.Modules;
using KSP.Sim.Definitions;
using KSP.Sim.impl;

// ReSharper disable once CheckNamespace
namespace KerbalLifeSupportSystem
{
    [HarmonyPatch(typeof(PartComponentModule_Command), "GetExpectedControlStatus")]
    class PartComponentModule_Command_GetExpectedControlStatus_Patches
    {
        static bool Prefix(PartComponentModule_Command __instance, ref CommandControlState __result)
        {
            if (__instance.Part.TryGetModuleData<PartComponentModule_LifeSupportConsumer, Data_LifeSupportConsumer>(
                    out var consumer))
            {
                if (consumer.numKerbals - consumer.kerbalsOnStrike.Count < 1)
                {
                    __result = CommandControlState.NotEnoughCrew;
                    return false;
                }
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(PartComponentModule_ScienceExperiment), "IsExperimentAllowed")]
    class PartComponentModule_ScienceExperiment_IsExperimentAllowed_Patches
    {
        static void Postfix(PartComponentModule_ScienceExperiment __instance, ref bool __result, int experimentIndex,
            Data_ScienceExperiment ___dataScienceExperiment, KerbalRosterManager ____kerbalRosterManager)
        {
            // If experiment already wasn't possible, we do not need to do anything
            if (!__result)
                return;

            // Get the part's LifeSupportConsumer module
            if (!__instance.Part.TryGetModuleData<PartComponentModule_LifeSupportConsumer, Data_LifeSupportConsumer>(
                    out var consumer))
                return;

            var experiment = ___dataScienceExperiment.Experiments[experimentIndex];

            // We check if (kerbalCount - kerbalOnStrikeCount) >= CrewRequired
            var hasEnoughKerbals = ____kerbalRosterManager != null && experiment.CrewRequired <=
                ____kerbalRosterManager.GetAllKerbalsInSimObject(__instance.Part.SimulationObject.GlobalId)
                    .Count - consumer.kerbalsOnStrike.Count;

            if (hasEnoughKerbals)
            {
                __result = true;
                return;
            }

            // Stock experiment status handling
            __instance.SetExperimentState(experimentIndex, ExperimentState.INSUFFICIENTCREW);
            ___dataScienceExperiment.ExperimentStandings[experimentIndex].CurrentExperimentContext =
                experiment.CrewRequired.ToString();
        }
    }
}