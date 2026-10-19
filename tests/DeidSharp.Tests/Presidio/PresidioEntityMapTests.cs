using DeidSharp.Presidio;

namespace DeidSharp.Tests.Presidio;

public class PresidioEntityMapTests
{
    [Theory]
    [InlineData("PERSON", PhiCategory.Name)]
    [InlineData("person", PhiCategory.Name)]
    [InlineData("US_SSN", PhiCategory.Ssn)]
    [InlineData("MEDICAL_LICENSE", PhiCategory.LicenseNumber)]
    [InlineData("LOCATION", PhiCategory.Location)]
    [InlineData("ORGANIZATION", PhiCategory.Organization)]
    [InlineData("AGE", PhiCategory.Age)]
    [InlineData("ID", PhiCategory.OtherIdentifier)]
    [InlineData("NEVER_SEEN", PhiCategory.OtherIdentifier)]
    public void Maps_entity_types(string entity, PhiCategory expected) => Assert.Equal(expected, PresidioEntityMap.Resolve(entity));

    [Fact]
    public void Overrides_win_over_the_default_map() =>
        Assert.Equal(PhiCategory.Organization, PresidioEntityMap.Resolve("LOCATION", new Dictionary<string, PhiCategory> { ["LOCATION"] = PhiCategory.Organization }));
}
