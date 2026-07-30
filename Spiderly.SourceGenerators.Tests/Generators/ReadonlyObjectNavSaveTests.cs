using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Spiderly.SourceGenerators.Net;
using Spiderly.SourceGenerators.Tests.Infrastructure;
using System.Linq;

namespace Spiderly.SourceGenerators.Tests.Generators;

/// <summary>
/// A navigation to a <c>ReadonlyObject</c> takes a different branch of the save emission than one to a
/// <c>BusinessObject</c> (<c>ServiceSaveGenerator.GetManyToOneInstancesForSave</c>), and that branch used
/// <c>prop.Type</c> where the other used <c>prop.Type.Name</c> — so it emitted the nullable ANNOTATION into a
/// type argument: <c>GetInstanceAsync&lt;TaskCategory?, byte&gt;</c>. That violates
/// <c>ServiceBase.GetInstanceAsync</c>'s <c>class</c> and <c>IReadonlyObject&lt;ID&gt;</c> constraints
/// (CS8631 + CS8634), breaking the consumer's build.
/// <para>
/// It needs three things at once to appear — a readonly-object target, a nullable annotation on the nav, and
/// an NRT-enabled consumer — which is why no snapshot test caught it and why it only became reachable when
/// the e2e fixture's navigations were annotated nullable. Found by
/// <see cref="GeneratedCodeCompilationTests"/>, pinned narrowly here so the failure names the bug instead of
/// arriving as one of hundreds of diagnostics.
/// </para>
/// </summary>
public class ReadonlyObjectNavSaveTests
{
    private const string Source = """
        using System.Collections.Generic;

        namespace TestApp.Business.Entities
        {
            [SpiderlyEntity]
            public class TaskCategory : ReadonlyObject<byte>
            {
                [DisplayName]
                [Required]
                public string Name { get; set; } = null!;

                public virtual List<ProjectTask> ProjectTasks { get; } = new();
            }

            [SpiderlyEntity]
            public class ProjectTask : BusinessObject<long>
            {
                [DisplayName]
                [Required]
                public string Title { get; set; } = null!;

                [WithMany(nameof(TaskCategory.ProjectTasks))]
                public virtual TaskCategory? TaskCategory { get; set; }
            }
        }
        """;

    [Fact]
    public void NullableReadonlyObjectNav_EmitsTheTypeWithoutItsNullableAnnotation()
    {
        string source = EmittedProjectTaskService();

        Assert.Contains("GetInstanceAsync<TaskCategory, byte>", source);
        Assert.DoesNotContain("GetInstanceAsync<TaskCategory?", source);
    }

    private static string EmittedProjectTaskService()
    {
        return GeneratorTestHarness.Run<ServicesGenerator>(Source, NullableContextOptions.Enable)
            .GetRunResult().GeneratedTrees
            .Single(t => t.FilePath.EndsWith("ProjectTaskService.generated.cs"))
            .ToString();
    }
}
