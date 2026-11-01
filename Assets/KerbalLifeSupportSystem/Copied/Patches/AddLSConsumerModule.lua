---@type KLSS.Constants
local C = require('constants')

PM.Parts:Patch("AddLSConsumerModule")
:Feature("Patches")
:Requires(|part| part.crewCapacity > 0, "part is not crewed")
:HasNo("Module_LifeSupportConsumer", "part already has the module")
:Do(function(part)
    part:AddModule("Module_LifeSupportConsumer", function(module)
        module:AddData("Data_LifeSupportConsumer", function(data)
            data.LifeSupportDefinition = {
                InternalName = "KerbalLifeSupport",
                FormulaLocalizationKey = "KLSS/LSR/Formulas/LifeSupport",
                InputResources = {
                    {
                        Rate = C.OXYGEN_PER_DAY / C.SECONDS_PER_DAY,
                        ResourceName = "Oxygen",
                        AcceptanceThreshold = 0.00000001
                    },
                    {
                        Rate = C.WATER_PER_DAY / C.SECONDS_PER_DAY,
                        ResourceName = "Water",
                        AcceptanceThreshold = 0.0001
                    },
                    {
                        Rate = C.FOOD_PER_DAY / C.SECONDS_PER_DAY,
                        ResourceName = "Food",
                        AcceptanceThreshold = 0.00000001
                    }
                },
                OutputResources = {
                    {
                        Rate = C.OXYGEN_PER_DAY / C.SECONDS_PER_DAY,
                        ResourceName = "CarbonDioxide",
                        AcceptanceThreshold = 0.00000001
                    },
                    {
                        Rate = C.WATER_PER_DAY / C.SECONDS_PER_DAY,
                        ResourceName = "WasteWater",
                        AcceptanceThreshold = 0.0001
                    },
                    {
                        Rate = C.FOOD_PER_DAY / C.SECONDS_PER_DAY,
                        ResourceName = "Waste",
                        AcceptanceThreshold = 0.00000001
                    }
                },
                AcceptanceThreshold = 0.000001
            }
        end)
    end)
end)