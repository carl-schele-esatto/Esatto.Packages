using System.Reflection;
using System.Text.Json;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Umbraco.Cms.Core.Composing;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The Management API's resolver (<c>UmbracoJsonTypeInfoResolver</c>) makes every INTERFACE it serializes polymorphic over the classes the
/// host's type finder knows that implement it. A site, or any package it runs, with a class like <c>class Tags : List&lt;string&gt;</c> then
/// turns an <c>IReadOnlyList&lt;string&gt;</c> member into one whose plain value cannot be written: every answer with such a member would
/// be a 500. So no member of an answer is an interface. This runs the real resolver over a type finder that knows a host class for EVERY
/// collection interface it is asked about, and every answer the controller gives must come out exactly as it does in a host without them.
/// </summary>
public class HostTypesJsonTests
{
    private static readonly JsonSerializerOptions HostOptions = ManagementJson.Build(new HostTypeFinder());

    private static readonly Lazy<Task<IReadOnlyList<(string Name, object Value)>>> Answers = new(RealResponses.AllAsync);

    private static readonly string[] Names =
    [
        "status", "report", "settings", "fetchProgress", "startFetch", "note", "targets", "userGroups", "connectionTest",
        "periodProblem", "fetchProblem", "analysis", "noAnalysis", "daily", "dailySources",
    ];

    public static TheoryData<string> AnswerNames() => [.. Names];

    [Theory]
    [MemberData(nameof(AnswerNames))]
    public async Task Every_answer_is_written_the_same_in_a_host_that_has_its_own_collection_classes(string name)
    {
        var value = (await Answers.Value).Single(answer => answer.Name == name).Value;

        var plain = ManagementJson.Serialize(value);
        var hosted = JsonSerializer.Serialize(value, value.GetType(), HostOptions);

        Assert.Equal(plain, hosted);
    }

    [Fact]
    public async Task Every_kind_of_answer_is_covered()
        => Assert.Equal(Names.Order(), (await Answers.Value).Select(answer => answer.Name).Order());

    /// <summary>The probe itself: an interface member does break under these options, so the tests above can fail.</summary>
    [Fact]
    public void An_interface_typed_member_cannot_be_written_in_such_a_host()
    {
        Assert.ThrowsAny<NotSupportedException>(() => JsonSerializer.Serialize(new WithList(["a"]), HostOptions));
        Assert.ThrowsAny<NotSupportedException>(() => JsonSerializer.Serialize(new WithMap(new Dictionary<string, object?> { ["a"] = 1 }), HostOptions));
    }

    private sealed record WithList(IReadOnlyList<string> Items);

    private sealed record WithMap(IReadOnlyDictionary<string, object?> Args);

    /// <summary>What a site's own code or another package could hold: classes that implement the common collection interfaces.</summary>
    public sealed class HostList : List<string>;

    public sealed class HostDict : Dictionary<string, object>;

    public sealed class HostList<T> : List<T>;

    public sealed class HostDict<TKey, TValue> : Dictionary<TKey, TValue>
        where TKey : notnull;

    /// <summary>
    /// A type finder that, for any collection interface it is asked about, knows a host class implementing it: the two plain ones above
    /// when they fit, and a list or dictionary of the very element types asked for, so no interface member of any answer goes untested.
    /// </summary>
    private sealed class HostTypeFinder : ITypeFinder
    {
        public IEnumerable<Assembly> AssembliesToScan => [];

        public Type? GetTypeByName(string name) => null;

        public IEnumerable<Type> FindClassesOfType(Type assignTypeFrom, IEnumerable<Assembly>? assemblies = null, bool onlyConcreteClasses = true)
            => Candidates(assignTypeFrom).Where(assignTypeFrom.IsAssignableFrom).Distinct();

        public IEnumerable<Type> FindClassesOfTypeWithAttribute(Type assignTypeFrom, Type attributeType, IEnumerable<Assembly>? assemblies = null, bool onlyConcreteClasses = true) => [];

        public IEnumerable<Type> FindClassesWithAttribute(Type attributeType, IEnumerable<Assembly>? assemblies = null, bool onlyConcreteClasses = true) => [];

        private static IEnumerable<Type> Candidates(Type wanted)
        {
            yield return typeof(HostList);
            yield return typeof(HostDict);

            if (!wanted.IsGenericType) yield break;

            var arguments = wanted.GetGenericArguments();
            if (arguments.Length == 1)
            {
                yield return typeof(HostList<>).MakeGenericType(arguments[0]);
                if (arguments[0].IsGenericType && arguments[0].GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                {
                    yield return typeof(HostDict<,>).MakeGenericType(arguments[0].GetGenericArguments());
                }
            }
            else if (arguments.Length == 2)
            {
                yield return typeof(HostDict<,>).MakeGenericType(arguments);
            }
        }
    }
}
