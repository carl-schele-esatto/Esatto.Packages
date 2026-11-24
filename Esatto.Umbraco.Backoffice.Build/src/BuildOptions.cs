namespace Esatto.Umbraco.Backoffice.Build;

/// <summary>Settings under <c>Esatto:Build</c> in appsettings.json. Both are optional.</summary>
public sealed class BuildOptions
{
    public const string SectionName = "Esatto:Build";

    /// <summary>Where the pipeline's build stamp is, relative to the content root (an absolute path also works).</summary>
    public string FilePath { get; set; } = PipelineBuildReader.DefaultFileName;

    /// <summary>
    /// Assembly names to list under Packages, on top of the built-in ones. A trailing <c>*</c> matches a family,
    /// e.g. <c>Contoso.Umbraco.*</c>. Assemblies that are not loaded are skipped.
    /// </summary>
    public string[] Packages { get; set; } = [];
}
