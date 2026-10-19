namespace DeidSharp.IntegrationTests;

/// <summary>
/// The stock Presidio image the integration tests run is the one docker-compose.yml pins.
/// Dependabot updates the compose file; this test fails until the tests run the new
/// image too, so an update is never merged without being tested. Needs no Docker.
/// </summary>
public class PinnedImageTests
{
    [Fact]
    public void The_tests_run_the_presidio_image_compose_pins()
    {
        var compose = File.ReadAllText(Path.Combine(RepositoryRoot(), "docker-compose.yml"));

        Assert.Contains($"image: {StockPresidioContainer.PinnedImage}", compose, StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DeidSharp.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root (DeidSharp.slnx) was not found.");
    }
}
