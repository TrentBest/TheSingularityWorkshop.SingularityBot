using SingularityBot.Behavior;
using SingularityBot.Observation;
using Xunit;

namespace SingularityBot.Tests;

public sealed class WorkerResourceAssignmentPlannerTests
{
    [Fact]
    public void AssignsWorkersDeterministicallyAndBalancesMineralSaturation()
    {
        var planner = new WorkerResourceAssignmentPlanner();
        var observation = Snapshot(
            Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0),
            Fact(10, "SCV", UnitAlliance.Self, 1, 0),
            Fact(11, "SCV", UnitAlliance.Self, 2, 0),
            Fact(12, "SCV", UnitAlliance.Self, 3, 0),
            Fact(20, "MineralField750", UnitAlliance.Neutral, 5, 0),
            Fact(21, "MineralField750", UnitAlliance.Neutral, 8, 0));

        var assignments = planner.Reconcile(observation);

        Assert.Equal(3, assignments.Count);
        Assert.Equal(20UL, assignments[10].MineralTag);
        Assert.Equal(21UL, assignments[11].MineralTag);
        Assert.Equal(21UL, assignments[12].MineralTag);
        Assert.Equal(1u, assignments[10].Generation);
        Assert.Equal(2u, assignments[11].Generation);
        Assert.Equal(3u, assignments[12].Generation);
    }

    [Fact]
    public void AdoptsExistingMineralOrdersInsteadOfIgnoringAlreadyWorkingScvs()
    {
        var planner = new WorkerResourceAssignmentPlanner();
        var worker = Fact(10, "SCV", UnitAlliance.Self, 2, 0, hasOrders: true,
            orders: new[] { new UnitOrderObservationFacts(3666, 20, null, null) });
        var observation = Snapshot(
            Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0),
            worker,
            Fact(20, "MineralField750", UnitAlliance.Neutral, 5, 0),
            Fact(21, "MineralField750", UnitAlliance.Neutral, 8, 0));

        var first = planner.Reconcile(observation);
        var second = planner.Reconcile(observation);

        Assert.Equal(20UL, first[10].MineralTag);
        Assert.Equal(first[10], second[10]);
        Assert.Equal(1, first.Count);
    }

    [Fact]
    public void RedistributesStartingWorkersWhenTheGamePointsThemAllAtOneMineral()
    {
        var planner = new WorkerResourceAssignmentPlanner();
        var initialGather = new[] { new UnitOrderObservationFacts(3666, 20, null, null) };
        var assignments = planner.Reconcile(Snapshot(
            Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0),
            Fact(10, "SCV", UnitAlliance.Self, 1, 0, hasOrders: true, orders: initialGather),
            Fact(11, "SCV", UnitAlliance.Self, 2, 0, hasOrders: true, orders: initialGather),
            Fact(12, "SCV", UnitAlliance.Self, 3, 0, hasOrders: true, orders: initialGather),
            Fact(13, "SCV", UnitAlliance.Self, 4, 0, hasOrders: true, orders: initialGather),
            Fact(20, "MineralField750", UnitAlliance.Neutral, 5, 0),
            Fact(21, "MineralField750", UnitAlliance.Neutral, 8, 0)));

        Assert.Equal(2, assignments.Values.Count(item => item.MineralTag == 20));
        Assert.Equal(2, assignments.Values.Count(item => item.MineralTag == 21));
        Assert.Equal(20UL, assignments[11].MineralTag);
        Assert.Equal(21UL, assignments[12].MineralTag);
        Assert.Equal(21UL, assignments[13].MineralTag);
    }

    [Fact]
    public void SpreadsWorkersAcrossAllPatchesBeforeDoublingTheFarthestPatches()
    {
        var planner = new WorkerResourceAssignmentPlanner();
        var initialGather = new[] { new UnitOrderObservationFacts(3666, 20, null, null) };
        var assignments = planner.Reconcile(Snapshot(
            Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0),
            Fact(10, "SCV", UnitAlliance.Self, 1, 0, hasOrders: true, orders: initialGather),
            Fact(11, "SCV", UnitAlliance.Self, 2, 0, hasOrders: true, orders: initialGather),
            Fact(12, "SCV", UnitAlliance.Self, 3, 0, hasOrders: true, orders: initialGather),
            Fact(13, "SCV", UnitAlliance.Self, 4, 0, hasOrders: true, orders: initialGather),
            Fact(14, "SCV", UnitAlliance.Self, 5, 0, hasOrders: true, orders: initialGather),
            Fact(15, "SCV", UnitAlliance.Self, 6, 0, hasOrders: true, orders: initialGather),
            Fact(20, "MineralField750", UnitAlliance.Neutral, 5, 0),
            Fact(21, "MineralField750", UnitAlliance.Neutral, 8, 0),
            Fact(22, "MineralField750", UnitAlliance.Neutral, 11, 0)));

        Assert.Equal(2, assignments.Values.Count(item => item.MineralTag == 20));
        Assert.Equal(2, assignments.Values.Count(item => item.MineralTag == 21));
        Assert.Equal(2, assignments.Values.Count(item => item.MineralTag == 22));
        Assert.Equal(20UL, assignments[11].MineralTag);
        Assert.Equal(21UL, assignments[12].MineralTag);
    }

    [Fact]
    public void GloballyMatchesStartingWorkersBeforeFillingRemainingMineralCapacity()
    {
        var planner = new WorkerResourceAssignmentPlanner();
        var assignments = planner.Reconcile(Snapshot(
            Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0),
            Fact(10, "SCV", UnitAlliance.Self, 1, 0, hasOrders: true,
                orders: new[] { new UnitOrderObservationFacts(3666, 20, null, null) }),
            Fact(11, "SCV", UnitAlliance.Self, 2, 0),
            Fact(12, "SCV", UnitAlliance.Self, 3, 0),
            Fact(20, "MineralField750", UnitAlliance.Neutral, 5, 0),
            Fact(21, "MineralField750", UnitAlliance.Neutral, 8, 0)));

        Assert.Equal(20UL, assignments[10].MineralTag);
        Assert.Equal(21UL, assignments[11].MineralTag);
        Assert.Equal(21UL, assignments[12].MineralTag);
    }

    [Fact]
    public void GloballyMatchesEachMineralToItsClosestStartingWorkerInsteadOfFollowingTagOrder()
    {
        var planner = new WorkerResourceAssignmentPlanner();
        var initialGather = new[] { new UnitOrderObservationFacts(3666, 20, null, null) };
        var assignments = planner.Reconcile(Snapshot(
            Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0),
            Fact(10, "SCV", UnitAlliance.Self, 10, 0, hasOrders: true, orders: initialGather),
            Fact(11, "SCV", UnitAlliance.Self, 0, 0, hasOrders: true, orders: initialGather),
            Fact(12, "SCV", UnitAlliance.Self, 0, 10, hasOrders: true, orders: initialGather),
            Fact(20, "MineralField750", UnitAlliance.Neutral, 0, 1),
            Fact(21, "MineralField750", UnitAlliance.Neutral, 10, 1),
            Fact(22, "MineralField750", UnitAlliance.Neutral, 0, 11)));

        Assert.Equal(21UL, assignments[10].MineralTag);
        Assert.Equal(20UL, assignments[11].MineralTag);
        Assert.Equal(22UL, assignments[12].MineralTag);
    }

    [Fact]
    public void AssignsExtraWorkersToTheFarthestMineralPatchesAfterPrimaryCoverage()
    {
        var planner = new WorkerResourceAssignmentPlanner();
        var initialGather = new[] { new UnitOrderObservationFacts(3666, 20, null, null) };
        var assignments = planner.Reconcile(Snapshot(
            Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0),
            Fact(10, "SCV", UnitAlliance.Self, 4, 0, hasOrders: true, orders: initialGather),
            Fact(11, "SCV", UnitAlliance.Self, 7, 0, hasOrders: true, orders: initialGather),
            Fact(12, "SCV", UnitAlliance.Self, 10, 0, hasOrders: true, orders: initialGather),
            Fact(13, "SCV", UnitAlliance.Self, 9, 0, hasOrders: true, orders: initialGather),
            Fact(14, "SCV", UnitAlliance.Self, 6, 0, hasOrders: true, orders: initialGather),
            Fact(20, "MineralField750", UnitAlliance.Neutral, 5, 0),
            Fact(21, "MineralField750", UnitAlliance.Neutral, 8, 0),
            Fact(22, "MineralField750", UnitAlliance.Neutral, 11, 0)));

        Assert.Equal(1, assignments.Values.Count(item => item.MineralTag == 20));
        Assert.Equal(2, assignments.Values.Count(item => item.MineralTag == 21));
        Assert.Equal(2, assignments.Values.Count(item => item.MineralTag == 22));
        Assert.Equal(20UL, assignments[10].MineralTag);
        Assert.Equal(21UL, assignments[11].MineralTag);
        Assert.Equal(22UL, assignments[12].MineralTag);
        Assert.Equal(22UL, assignments[13].MineralTag);
        Assert.Equal(21UL, assignments[14].MineralTag);
    }

    [Fact]
    public void AcolyteOpeningMatchingReducesInitialWorkerToMineralDistanceAgainstCapturedBaseline()
    {
        var planner = new WorkerResourceAssignmentPlanner();
        var initialGather = new[] { new UnitOrderObservationFacts(3666, 4296540161UL, null, null) };
        var workers = new[]
        {
            Fact(4346085377UL, "SCV", UnitAlliance.Self, 141.5f, 165.5f, true, orders: initialGather),
            Fact(4346347521UL, "SCV", UnitAlliance.Self, 141.5f, 164.5f, true, orders: initialGather),
            Fact(4346609665UL, "SCV", UnitAlliance.Self, 140.5f, 165.5f, true, orders: initialGather),
            Fact(4346871809UL, "SCV", UnitAlliance.Self, 141.5f, 163.5f, true, orders: initialGather),
            Fact(4347133953UL, "SCV", UnitAlliance.Self, 139.5f, 165.5f, true, orders: initialGather),
            Fact(4347396097UL, "SCV", UnitAlliance.Self, 141.5f, 162.5f, true, orders: initialGather),
            Fact(4347658241UL, "SCV", UnitAlliance.Self, 138.5f, 165.5f, true, orders: initialGather),
            Fact(4347920385UL, "SCV", UnitAlliance.Self, 141.5f, 161.5f, true, orders: initialGather),
            Fact(4348182529UL, "SCV", UnitAlliance.Self, 137.5f, 165.5f, true, orders: initialGather),
            Fact(4348444673UL, "SCV", UnitAlliance.Self, 141.5f, 160.5f, true, orders: initialGather),
            Fact(4348706817UL, "SCV", UnitAlliance.Self, 136.5f, 165.5f, true, orders: initialGather),
            Fact(4348968961UL, "SCV", UnitAlliance.Self, 141.5f, 159.5f, true, orders: initialGather)
        };
        var minerals = new[]
        {
            Fact(4294967297UL, "MineralField750", UnitAlliance.Neutral, 140, 168.5f),
            Fact(4295491585UL, "MineralField750", UnitAlliance.Neutral, 141, 169.5f),
            Fact(4296015873UL, "MineralField750", UnitAlliance.Neutral, 143, 168.5f),
            Fact(4296540161UL, "MineralField750", UnitAlliance.Neutral, 144, 167.5f),
            Fact(4297064449UL, "MineralField750", UnitAlliance.Neutral, 145, 166.5f),
            Fact(4297588737UL, "MineralField750", UnitAlliance.Neutral, 145, 164.5f),
            Fact(4298113025UL, "MineralField750", UnitAlliance.Neutral, 146, 163.5f),
            Fact(4298637313UL, "MineralField750", UnitAlliance.Neutral, 145, 162.5f)
        };
        var oldLoopTwentyTwoTargets = new Dictionary<ulong, ulong>
        {
            [4346085377UL] = 4296540161UL,
            [4346347521UL] = 4297064449UL,
            [4346609665UL] = 4298113025UL,
            [4346871809UL] = 4296015873UL,
            [4347133953UL] = 4295491585UL,
            [4347396097UL] = 4297588737UL,
            [4347658241UL] = 4298637313UL,
            [4347920385UL] = 4294967297UL,
            [4348182529UL] = 4297064449UL,
            [4348444673UL] = 4298113025UL,
            [4348706817UL] = 4296015873UL,
            [4348968961UL] = 4296540161UL
        };

        var assignments = planner.Reconcile(Snapshot(
            Fact(1, "CommandCenter", UnitAlliance.Self, 138.5f, 162.5f),
            workers.Concat(minerals).ToArray()));

        var mineralByTag = minerals.ToDictionary(mineral => mineral.Tag);
        var oldDistance = workers.Sum(worker =>
        {
            var mineral = mineralByTag[oldLoopTwentyTwoTargets[worker.Tag]];
            return Math.Sqrt(
                Math.Pow(worker.PositionX - mineral.PositionX, 2) +
                Math.Pow(worker.PositionY - mineral.PositionY, 2));
        });
        var matchedDistance = assignments.Values.Sum(assignment =>
            Math.Sqrt(
                Math.Pow(assignment.WorkerX - assignment.MineralX, 2) +
                Math.Pow(assignment.WorkerY - assignment.MineralY, 2)));

        Assert.Equal(12, assignments.Count);
        Assert.Equal(8, assignments.Values.Select(item => item.MineralTag).Distinct().Count());
        Assert.InRange(oldDistance, 69.43, 69.45);
        Assert.InRange(matchedDistance, 53.96, 53.98);
        Assert.True(matchedDistance < oldDistance);
    }

    [Fact]
    public void ReservedBuilderIsExcludedUntilExplicitlyUnreserved()
    {
        var planner = new WorkerResourceAssignmentPlanner();
        var observation = Snapshot(
            Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0),
            Fact(10, "SCV", UnitAlliance.Self, 1, 0),
            Fact(20, "MineralField750", UnitAlliance.Neutral, 5, 0));

        Assert.Contains(10UL, planner.Reconcile(observation).Keys);
        planner.Reserve(10);

        Assert.Empty(planner.Reconcile(observation));
        Assert.True(planner.IsReserved(10));

        planner.Unreserve(10);
        Assert.Contains(10UL, planner.Reconcile(observation).Keys);
    }

    [Fact]
    public void KeepsReservationAndGenerationWhenWorkerTemporarilyDisappears()
    {
        var planner = new WorkerResourceAssignmentPlanner();
        var initial = Snapshot(
            Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0),
            Fact(10, "SCV", UnitAlliance.Self, 1, 0),
            Fact(20, "MineralField750", UnitAlliance.Neutral, 5, 0));

        var first = planner.Reconcile(initial)[10];
        var next = new NormalizedObservation(
            110, 1,
            new[]
            {
                Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0, loop: 110),
                Fact(20, "MineralField750", UnitAlliance.Neutral, 5, 0, loop: 110)
            },
            new HashSet<ulong>());

        var retained = planner.Reconcile(next);

        Assert.Equal(first, retained[10]);
        Assert.Equal(first.Generation, retained[10].Generation);
    }

    [Fact]
    public void RetainsReservationWhenMineralIsTemporarilyAbsentFromObservation()
    {
        var planner = new WorkerResourceAssignmentPlanner();
        var first = planner.Reconcile(Snapshot(
            Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0),
            Fact(10, "SCV", UnitAlliance.Self, 1, 0),
            Fact(20, "MineralField750", UnitAlliance.Neutral, 5, 0)))[10];

        var temporarilyUnobserved = planner.Reconcile(Snapshot(
            Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0, loop: 120),
            Fact(10, "SCV", UnitAlliance.Self, 1, 0, loop: 120)));

        Assert.Equal(first, temporarilyUnobserved[10]);
    }

    [Fact]
    public void ReassignsWhenReservedMineralDiesAndAdvancesGeneration()
    {
        var planner = new WorkerResourceAssignmentPlanner();
        var initial = Snapshot(
            Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0),
            Fact(10, "SCV", UnitAlliance.Self, 1, 0),
            Fact(20, "MineralField750", UnitAlliance.Neutral, 5, 0),
            Fact(21, "MineralField750", UnitAlliance.Neutral, 8, 0));

        var first = planner.Reconcile(initial)[10];
        var afterDeath = new NormalizedObservation(
            120, 1,
            new[]
            {
                Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0, loop: 120),
                Fact(10, "SCV", UnitAlliance.Self, 1, 0, loop: 120),
                Fact(21, "MineralField750", UnitAlliance.Neutral, 8, 0, loop: 120)
            },
            new HashSet<ulong> { 20 });

        var reassigned = planner.Reconcile(afterDeath)[10];

        Assert.Equal(21UL, reassigned.MineralTag);
        Assert.NotEqual(first.Generation, reassigned.Generation);
    }

    [Fact]
    public void ReassignsWorkerWhenItsCurrentOrderTargetsItsExplicitlyDeadAssignedMineral()
    {
        var planner = new WorkerResourceAssignmentPlanner();
        var first = planner.Reconcile(Snapshot(
            Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0),
            Fact(10, "SCV", UnitAlliance.Self, 1, 0),
            Fact(20, "MineralField750", UnitAlliance.Neutral, 5, 0),
            Fact(21, "MineralField750", UnitAlliance.Neutral, 8, 0)))[10];

        var afterDeath = new NormalizedObservation(
            120, 1,
            new[]
            {
                Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0, loop: 120),
                Fact(10, "SCV", UnitAlliance.Self, 1, 0, hasOrders: true, loop: 120,
                    orders: new[] { new UnitOrderObservationFacts(3666, 20, null, null) }),
                Fact(21, "MineralField750", UnitAlliance.Neutral, 8, 0, loop: 120)
            },
            new HashSet<ulong> { 20 });

        var reassigned = planner.Reconcile(afterDeath)[10];

        Assert.Equal(21UL, reassigned.MineralTag);
        Assert.NotEqual(first.Generation, reassigned.Generation);
    }

    [Fact]
    public void DoesNotOverrideAnUnrelatedActiveWorkerOrderWhenAssignedMineralDies()
    {
        var planner = new WorkerResourceAssignmentPlanner();
        planner.Reconcile(Snapshot(
            Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0),
            Fact(10, "SCV", UnitAlliance.Self, 1, 0),
            Fact(20, "MineralField750", UnitAlliance.Neutral, 5, 0),
            Fact(21, "MineralField750", UnitAlliance.Neutral, 8, 0)));

        var afterDeath = new NormalizedObservation(
            120, 1,
            new[]
            {
                Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0, loop: 120),
                Fact(10, "SCV", UnitAlliance.Self, 1, 0, hasOrders: true, loop: 120,
                    orders: new[] { new UnitOrderObservationFacts(9999, 500, null, null) }),
                Fact(21, "MineralField750", UnitAlliance.Neutral, 8, 0, loop: 120)
            },
            new HashSet<ulong> { 20 });

        Assert.Empty(planner.Reconcile(afterDeath));
    }

    [Fact]
    public void DoesNotAssignWithoutTownHallOrBeyondMineralCapacity()
    {
        var planner = new WorkerResourceAssignmentPlanner();
        var noTownHall = Snapshot(
            Fact(10, "SCV", UnitAlliance.Self, 1, 0),
            Fact(20, "MineralField750", UnitAlliance.Neutral, 5, 0));
        Assert.Empty(planner.Reconcile(noTownHall));

        var saturated = Snapshot(
            Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0),
            Fact(10, "SCV", UnitAlliance.Self, 1, 0),
            Fact(11, "SCV", UnitAlliance.Self, 2, 0),
            Fact(12, "SCV", UnitAlliance.Self, 3, 0),
            Fact(20, "MineralField750", UnitAlliance.Neutral, 5, 0));
        Assert.Equal(2, planner.Reconcile(saturated, mineralSaturation: 2).Count);
    }

    [Fact]
    public void ExistingAssignmentsAreNotRecomputedFromWorkerMovement()
    {
        var planner = new WorkerResourceAssignmentPlanner();
        var first = planner.Reconcile(Snapshot(
            Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0),
            Fact(10, "SCV", UnitAlliance.Self, 1, 0),
            Fact(20, "MineralField750", UnitAlliance.Neutral, 5, 0),
            Fact(21, "MineralField750", UnitAlliance.Neutral, 8, 0)))[10];

        var moved = planner.Reconcile(Snapshot(
            Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0, loop: 130),
            Fact(10, "SCV", UnitAlliance.Self, 9, 0, loop: 130),
            Fact(20, "MineralField750", UnitAlliance.Neutral, 5, 0, loop: 130),
            Fact(21, "MineralField750", UnitAlliance.Neutral, 8, 0, loop: 130)))[10];

        Assert.Equal(first.MineralTag, moved.MineralTag);
        Assert.Equal(first.Generation, moved.Generation);
        Assert.Equal(first.WorkerX, moved.WorkerX);
    }

    [Fact]
    public void FirstFrameSpreadsAllTwelveStartingWorkersAndEmitsTheirGatherCommands()
    {
        var planner = new WorkerResourceAssignmentPlanner();
        var initialGather = new[] { new UnitOrderObservationFacts(3666, 200, null, null) };
        var units = new List<UnitObservationFacts>
        {
            Fact(1, "CommandCenter", UnitAlliance.Self, 0, 0)
        };

        for (ulong tag = 100; tag < 112; tag++)
            units.Add(Fact(tag, "SCV", UnitAlliance.Self, (float)(tag - 99), 0,
                hasOrders: true, orders: initialGather));

        for (ulong tag = 200; tag < 208; tag++)
            units.Add(Fact(tag, "MineralField750", UnitAlliance.Neutral, (float)(tag - 195), 4));

        var observation = Snapshot(units.ToArray());
        var assignments = planner.Reconcile(observation);
        var intents = assignments.Keys
            .OrderBy(tag => tag)
            .Select(tag => new KeyValuePair<ulong, BehaviorCommand>(
                tag, BehaviorCommand.AssignWorkerToMinerals))
            .ToArray();

        var actions = Sc2ActionTranslator.Translate(intents, observation, 1, assignments);

        Assert.Equal(12, assignments.Count);
        Assert.Equal(12, actions.Count);
        Assert.Equal(12, actions.SelectMany(action => action.ActionRaw.UnitCommand.UnitTags).Distinct().Count());
        Assert.All(assignments.Values.GroupBy(item => item.MineralTag),
            group => Assert.InRange(group.Count(), 1, WorkerResourceAssignmentPlanner.DefaultMineralSaturation));
        Assert.Equal(8, assignments.Values.Select(item => item.MineralTag).Distinct().Count());
    }

    private static NormalizedObservation Snapshot(params UnitObservationFacts[] units) =>
        new(units.Length == 0 ? 0u : units.Max(unit => unit.GameLoop), 1, units, new HashSet<ulong>());

    private static UnitObservationFacts Fact(
        ulong tag, string kind, UnitAlliance alliance, float x, float y,
        bool hasOrders = false, uint loop = 100,
        IReadOnlyList<UnitOrderObservationFacts> orders = null) =>
        new UnitObservationFacts(tag, 1, kind, alliance == UnitAlliance.Self ? 1 : 0, alliance,
            UnitVisibility.Visible, 45, 45, 0, 0, 1, x, y, true,
            false, hasOrders, false, false, false, loop)
        {
            Orders = orders ?? Array.Empty<UnitOrderObservationFacts>()
        };
}
