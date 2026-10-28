using Xunit;

namespace Esatto.Umbraco.Backoffice.Accessibility.Tests;

public class ScaffoldTests
{
    [Fact]
    public void Package_assembly_name_matches_package_id()
        => Assert.Equal("Esatto.Umbraco.Backoffice.Accessibility", typeof(AccessibilityComposer).Assembly.GetName().Name);
}
