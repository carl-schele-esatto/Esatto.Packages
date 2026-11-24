using System.Reflection;
using System.Reflection.Emit;

namespace Esatto.Umbraco.Backoffice.Build.Tests.Support;

/// <summary>
/// Dynamic assemblies with a chosen name, assembly version and (optionally) informational version, so tests
/// can describe "which assemblies are loaded" without depending on what the test host really has loaded.
/// </summary>
public static class FakeAssemblies
{
    public static Assembly Create(string name, string? informationalVersion = null, Version? version = null)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name) { Version = version }, AssemblyBuilderAccess.Run);
        if (informationalVersion is not null)
        {
            var constructor = typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)])!;
            assembly.SetCustomAttribute(new CustomAttributeBuilder(constructor, [informationalVersion]));
        }

        return assembly;
    }
}
