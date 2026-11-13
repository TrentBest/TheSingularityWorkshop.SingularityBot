namespace SingularityBot.Behavior;

/// <summary>
/// Encodes the Marine Mode gas-economy progression gate. No Refinery may
/// start until the starting Command Center's Orbital morph order is observed.
/// The second Refinery also waits for the third Supply Depot milestone.
/// </summary>
public static class Sc2RefineryProgressionPolicy
{
    public static bool CanFundRefinery(
        uint minerals,
        uint refineryMineralCost,
        uint reservedMinerals) =>
        refineryMineralCost > 0 &&
        minerals >= refineryMineralCost &&
        minerals - refineryMineralCost >= reservedMinerals;

    public static bool ShouldConstruct(
        bool firstBarracksComplete,
        bool orbitalMorphingObserved,
        int completedRefineryCount,
        bool thirdSupplyDepotComplete,
        bool refineryUnderConstruction)
    {
        if (!firstBarracksComplete || !orbitalMorphingObserved ||
            refineryUnderConstruction || completedRefineryCount >= 2)
            return false;

        return completedRefineryCount == 0 || thirdSupplyDepotComplete;
    }
}
