using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace Spiderly.SourceGenerators.Tests.Infrastructure;

/// <summary>
/// The metadata references every generator test compiles against: this test assembly's full runtime closure.
/// <para>
/// Taking the whole closure rather than naming assemblies means the list cannot drift as generated code starts
/// referencing something new — and since the test project references <c>Spiderly.Shared</c> / <c>.Security</c> /
/// <c>.Infrastructure</c>, ASP.NET Core and Mapster, all of those are included.
/// </para>
/// <para>
/// Built once per test run (<c>static readonly</c>): <see cref="MetadataReference.CreateFromFile"/> over ~170
/// assemblies is expensive enough that doing it per test case would be the dominant cost of the suite.
/// </para>
/// </summary>
internal static class TestMetadataReferences
{
    internal static readonly IReadOnlyList<MetadataReference> All = Build();

    private static IReadOnlyList<MetadataReference> Build()
    {
        // File.Exists("") is false, so no separate empty-string guard is needed.
        string trustedPlatform = (string)System.AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;

        return trustedPlatform.Split(Path.PathSeparator)
            .Where(File.Exists)
            .Distinct()
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
    }
}
