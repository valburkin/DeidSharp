using DeidSharp.IntegrationTests;

// One container per analyzer image for the whole run, shared by every test class that
// takes it in its constructor. Two classes each starting their own stock Presidio at
// once was slower on CI runners, and a health check of one of them could hang.
[assembly: AssemblyFixture(typeof(StockPresidioContainer))]
[assembly: AssemblyFixture(typeof(ClinicalAnalyzerContainer))]
