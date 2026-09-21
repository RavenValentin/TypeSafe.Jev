// The client writes to a process-wide ActivitySource and Meter, so tests running in parallel land in each
// other's listeners. The suite is ~70ms; serializing it is cheaper than making every assertion defensive.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
