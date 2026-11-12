namespace SingularityBot.Behavior;

/// <summary>
/// Encodes the gas-economy progression gate. The first Refinery is the source
/// of gas required by the Orbital morph, so it must not depend on observing
/// that morph. Later Refineries remain behind the post-Orbital Depot milestone.
/// </summary>
public static class Sc2RefineryProgressionPolicy
{
    public static bool ShouldConstruct(
        bool firstBarracksComplete,
        bool orbitalMorphingObserved,
        int completedRefineryCount,
        bool thirdSupplyDepotComplete,
        bool refineryUnderConstruction)
    {
        if (!firstBarracksComplete || refineryUnderConstruction || completedRefineryCount >= 2)
            return false;

        return completedRefineryCount == 0 ||
               (orbitalMorphingObserved && thirdSupplyDepotComplete);
    }
}
