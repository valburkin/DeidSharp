using DeidSharp.Verification;

namespace DeidSharp.Tests.Verification;

public class ResidualPhiExceptionTests
{
    [Fact]
    public void The_message_lists_where_and_what_but_never_the_value()
    {
        var report = new LeakReport([new LeakFinding("$.note", 4, 12, PhiCategory.Name, ["known"])]);
        var ex = new ResidualPhiException(report);

        Assert.Same(report, ex.Report);
        Assert.Contains("$.note: possible Name at [4, 12) (known)", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_standard_constructors_give_an_empty_report()
    {
        Assert.True(new ResidualPhiException().Report.IsClean);
        Assert.True(new ResidualPhiException("m").Report.IsClean);
        Assert.True(new ResidualPhiException("m", new InvalidOperationException()).Report.IsClean);
    }
}
