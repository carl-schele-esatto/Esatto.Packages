using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;

/// <summary>Reads the arguments of a <see cref="TextRef"/> with their exact types: the server sends raw numbers, never formatted text.</summary>
internal static class TextRefArgs
{
    /// <summary>The argument <paramref name="name"/>, which must be there and have exactly the type <typeparamref name="T"/>.</summary>
    public static T Arg<T>(this TextRef text, string name)
    {
        Assert.NotNull(text.Args);
        Assert.True(text.Args.TryGetValue(name, out var value), $"{text.Key} carries no argument '{name}'");
        return Assert.IsType<T>(value);
    }

    /// <summary>The argument <paramref name="name"/>, which must be there and null: a part of a text that has nothing to say.</summary>
    public static void AssertNullArg(this TextRef text, string name)
    {
        Assert.NotNull(text.Args);
        Assert.True(text.Args.TryGetValue(name, out var value), $"{text.Key} carries no argument '{name}'");
        Assert.Null(value);
    }

    /// <summary>The names of the arguments, none for a text without any.</summary>
    public static string[] ArgNames(this TextRef text) => text.Args is null ? [] : [.. text.Args.Keys];

    /// <summary>Every figure among the arguments: the doubles must be finite numbers, never NaN or infinity.</summary>
    public static void AssertFinite(this TextRef text)
    {
        foreach (var (name, value) in text.Args ?? new Dictionary<string, object?>())
        {
            if (value is double number) Assert.True(double.IsFinite(number), $"{text.Key}.{name} is {number}");
        }
    }
}
