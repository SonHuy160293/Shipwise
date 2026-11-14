using Xunit;

// Host startup configures process-wide Serilog state; isolate host fixtures.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
