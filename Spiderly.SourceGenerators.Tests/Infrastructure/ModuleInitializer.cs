using System.Runtime.CompilerServices;
using VerifyTests;

namespace Spiderly.SourceGenerators.Tests.Infrastructure;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        VerifySourceGenerators.Initialize();
    }
}
