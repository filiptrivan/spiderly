using Spiderly.SourceGenerators.Shared;

namespace Spiderly.SourceGenerators.Tests;

public class SpiderlyTypeScriptEmissionTests
{
    [Fact]
    public void WriteSpiderlyTypeScriptFile_SameContent_DoesNotTouchExistingFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ts");
        DateTime originalWriteTime = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        try
        {
            SpiderlyTypeScriptEmission.WriteSpiderlyTypeScriptFile(path, "same content", "TestGenerator", "the test source");
            File.SetLastWriteTimeUtc(path, originalWriteTime);

            SpiderlyTypeScriptEmission.WriteSpiderlyTypeScriptFile(path, "same content", "TestGenerator", "the test source");

            Assert.Equal(originalWriteTime, File.GetLastWriteTimeUtc(path));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void WriteSpiderlyTypeScriptFile_DifferentContent_UpdatesExistingFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ts");

        try
        {
            SpiderlyTypeScriptEmission.WriteSpiderlyTypeScriptFile(path, "old content", "TestGenerator", "the test source");

            SpiderlyTypeScriptEmission.WriteSpiderlyTypeScriptFile(path, "new content", "TestGenerator", "the test source");

            Assert.EndsWith("new content", File.ReadAllText(path));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
