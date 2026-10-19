using DeidSharp.Detection;
using DeidSharp.Detection.Known;
using DeidSharp.Detection.Patterns;

namespace DeidSharp.Tests.Detection;

public class DeterministicDetectorsTests
{
    [Fact]
    public void The_stack_is_the_known_identifier_detector_then_the_pattern_detector() =>
        Assert.Collection(
            DeterministicDetectors.Create(),
            detector => Assert.IsType<KnownIdentifierDetector>(detector),
            detector => Assert.IsType<PatternPhiDetector>(detector));

    [Fact]
    public void Each_call_builds_new_detectors() =>
        Assert.NotSame(DeterministicDetectors.Create()[0], DeterministicDetectors.Create()[0]);
}
