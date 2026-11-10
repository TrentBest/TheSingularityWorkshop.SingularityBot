using Xunit;

// FSM_API owns process-wide registries and its update loop is not concurrent.
// Tests that create, destroy, or tick FSM instances must therefore run serially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
