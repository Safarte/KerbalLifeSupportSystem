---@type KLSS.Constants
local C = require('constants')

PM.Parts:Patch("AddLSRecyclerModule")
:Feature("Patches")
:Requires(|part| part.crewCapacity > 0, "part is not crewed")
:NotNamed("eva_kerbal", "seat_0v_external_crew", "lab_2v_science_orbital", "lab_2v_science_marine")
:HasNo("Module_ResourceConverter", "part already has a resource converter")
:Do(function (part)
    part:AddModule("Module_ResourceConverter", function(module)
        module:AddData("Data_ResourceConverter",function (data)
            data.SelectedFormula = 0
            data.ToggleName = "KLSS/LSR/RecyclerEnabled"
            data.StartActionName = "KLSS/LSR/StartRecycler"
            data.StopActionName = "KLSS/LSR/StopRecycler"
            data.ToggleActionName = "KLSS/LSR/ToggleRecycler"
            data.FormulaDefinitions = {
                {
                    InternalName = "KLSS_Combined",
                    FormulaLocalizationKey = "KLSS/LSR/Formulas/Combined",
                    InputResources = {
                        {
                            Rate = part.crewCapacity,
                            ResourceName = "ElectricCharge",
                            AcceptanceThreshold = 0.01
                        },
                        {
                            Rate = C.RECYCLER_EFFICIENCY * part.crewCapacity * C.OXYGEN_PER_DAY / C.SECONDS_PER_DAY,
                            ResourceName = "CarbonDioxide",
                            AcceptanceThreshold = 0.01
                        },
                        {
                            Rate = C.RECYCLER_EFFICIENCY * part.crewCapacity * C.WATER_PER_DAY / C.SECONDS_PER_DAY,
                            ResourceName = "WasteWater",
                            AcceptanceThreshold = 0.01
                        }
                    },
                    OutputResources = {
                        {
                            Rate = C.RECYCLER_EFFICIENCY * part.crewCapacity * C.OXYGEN_PER_DAY / C.SECONDS_PER_DAY,
                            ResourceName = "Oxygen",
                            AcceptanceThreshold = 0.01
                        },
                        {
                            Rate = C.RECYCLER_EFFICIENCY * part.crewCapacity * C.WATER_PER_DAY / C.SECONDS_PER_DAY,
                            ResourceName = "Water",
                            AcceptanceThreshold = 0.01
                        }
                    },
                    AcceptanceThreshold = 0.000001
                },
                {
                    InternalName = "KLSS_CO2Scrubber",
                    FormulaLocalizationKey = "KLSS/LSR/Formulas/CO2Scrubber",
                    InputResources = {
                        {
                            Rate = 0.5 * part.crewCapacity,
                            ResourceName = "ElectricCharge",
                            AcceptanceThreshold = 0.01
                        },
                        {
                            Rate = C.RECYCLER_EFFICIENCY * part.crewCapacity * C.OXYGEN_PER_DAY / C.SECONDS_PER_DAY,
                            ResourceName = "CarbonDioxide",
                            AcceptanceThreshold = 0.01
                        }
                    },
                    OutputResources = {
                        {
                            Rate = C.RECYCLER_EFFICIENCY * part.crewCapacity * C.OXYGEN_PER_DAY / C.SECONDS_PER_DAY,
                            ResourceName = "Oxygen",
                            AcceptanceThreshold = 0.01
                        }
                    },
                    AcceptanceThreshold = 0.000001
                },
                {

                    InternalName = "KLSS_WaterRecycler",
                    FormulaLocalizationKey = "KLSS/LSR/Formulas/WaterRecycler",
                    InputResources = {
                        {
                            Rate = 0.5 * part.crewCapacity,
                            ResourceName = "ElectricCharge",
                            AcceptanceThreshold = 0.01
                        },
                        {
                            Rate = C.RECYCLER_EFFICIENCY * part.crewCapacity * C.WATER_PER_DAY / C.SECONDS_PER_DAY,
                            ResourceName = "WasteWater",
                            AcceptanceThreshold = 0.01
                        }
                    },
                    OutputResources = {
                        {
                            Rate = C.RECYCLER_EFFICIENCY * part.crewCapacity * C.WATER_PER_DAY / C.SECONDS_PER_DAY,
                            ResourceName = "Water",
                            AcceptanceThreshold = 0.01
                        }
                    },
                    AcceptanceThreshold = 0.000001
                }
            }
        end)
    end)

    part.PAMModuleSortOverride:Append({
        PartComponentModuleName = "PartComponentModule_ResourceConverter",
        sortIndex = 40
    })

    part.PAMModuleVisualsOverride:Append({
        PartComponentModuleName = "PartComponentModule_ResourceConverter",
        ModuleDisplayName = "PartModules/LifeSupportRecycler/Name",
        ShowHeader = true,
        ShowFooter = false
    })
end)