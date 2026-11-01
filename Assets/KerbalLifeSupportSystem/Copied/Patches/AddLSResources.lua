---@type KLSS.Constants
local C = require('constants')

PM.Parts:Patch("AddLSResources")
:Feature("Patches")
:Requires(|part| part.crewCapacity > 0, "part is not crewed")
:NotNamed("eva_kerbal", "seat_0v_external_crew", "lab_2v_science_orbital", "lab_2v_science_marine")
:Requires(|part| not part.resourceContainers:Has("CarbonDioxide"), "part already has life support resources")
:Do(function(part)
    part.resourceContainers:Add(
        "CarbonDioxide",
        C.POD_CAPACITY_DAYS * C.OXYGEN_PER_DAY * part.crewCapacity,
        0)
    part.resourceContainers:Add("WasteWater",
        C.POD_CAPACITY_DAYS * C.WATER_PER_DAY * part.crewCapacity,
        0)
    part.resourceContainers:Add("Oxygen",
        C.POD_CAPACITY_DAYS * C.OXYGEN_PER_DAY * part.crewCapacity,
        C.POD_CAPACITY_DAYS * C.OXYGEN_PER_DAY * part.crewCapacity)
    part.resourceContainers:Add("Water",
        C.POD_CAPACITY_DAYS * C.WATER_PER_DAY * part.crewCapacity,
        C.POD_CAPACITY_DAYS * C.WATER_PER_DAY * part.crewCapacity)
    part.resourceContainers:Add("Food",
        C.POD_CAPACITY_DAYS * C.FOOD_PER_DAY * part.crewCapacity,
        C.POD_CAPACITY_DAYS * C.FOOD_PER_DAY * part.crewCapacity)
    part.resourceContainers:Add("Waste",
        C.POD_CAPACITY_DAYS * C.FOOD_PER_DAY * part.crewCapacity,
        0)
    part:EnsureModule("Module_ResourceCapacities", function(module)
        module:EnsureData("Data_ResourceCapacities", function(_) end)
    end)
end)