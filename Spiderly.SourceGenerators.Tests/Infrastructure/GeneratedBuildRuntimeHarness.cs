using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Spiderly.Shared.DTO;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Spiderly.SourceGenerators.Tests.Infrastructure;

/// <summary>
/// EXECUTES the generated <c>Build</c> against a real EF provider (Sqlite in-memory) — the layer the
/// text-pin tests structurally cannot see. Two shipped defects motivated it, both invisible to "the
/// generated text contains line X": <c>ApplySort</c>'s runtime-type guard that could never fire on a
/// real provider, and the Id tie-breaker running <c>isFirst: false</c> after a client sort matched no
/// case (Sentry BACKEND-RS-1F, 2026-08-10 — a 500 on any unknown sort field).
/// <para>
/// The provider must be EF, not <c>EnumerableQuery</c>: <c>Build</c> ends in <c>CountAsync()</c>,
/// which needs an async query provider. Compilation + emit happen once (<see cref="Lazy{T}"/>) via
/// <see cref="GeneratedCodeCompilationHarness.CompileAllGenerators"/> with one purpose-built
/// scalar-only entity appended — scalar-only so the EF model registers just it, with no navigation
/// discovery cascading over the whole fixture graph.
/// </para>
/// </summary>
internal static class GeneratedBuildRuntimeHarness
{
    internal sealed record ProbeRow(int Id, string Title, int Rank);

    internal sealed record BuildOutcome(int TotalRecords, IReadOnlyList<ProbeRow> Rows);

    private const string EntityName = "SortProbeRow";

    private const string EntitySource = $$"""
        using Spiderly.Shared.Attributes.Entity;
        using Spiderly.Shared.BaseEntities;
        using System.ComponentModel.DataAnnotations;

        namespace {{GeneratedCodeCompilationHarness.AppName}}.Business.Entities
        {
            [SpiderlyEntity]
            public class {{EntityName}} : BusinessObject<int>
            {
                [DisplayName]
                [Required]
                [StringLength(100, MinimumLength = 1)]
                public string Title { get; set; } = null!;

                public int Rank { get; set; }
            }
        }
        """;

    private static readonly Lazy<Assembly> Emitted = new(EmitAndLoad);

    private static Assembly EmitAndLoad()
    {
        Compilation compilation = GeneratedCodeCompilationHarness.CompileAllGenerators(
            out ImmutableArray<Diagnostic> generatorDiagnostics,
            extraSources: [EntitySource]);

        Diagnostic[] generatorErrors = generatorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        if (generatorErrors.Length > 0)
            throw new InvalidOperationException($"Generator errors:\n{string.Join("\n", generatorErrors.Select(d => d.ToString()))}");

        using MemoryStream peStream = new();
        var emitResult = compilation.Emit(peStream);

        if (!emitResult.Success)
        {
            string errors = string.Join("\n", emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString()));
            throw new InvalidOperationException($"Emit failed:\n{errors}");
        }

        // The compilation references the SAME Spiderly.Shared / EF Core assembly files this test process
        // already has loaded, so the emitted assembly binds to them by identity — FilterDTO,
        // BusinessException and PaginatedResult<T> are shared types, directly usable from tests.
        return Assembly.Load(peStream.ToArray());
    }

    private static Type EntityType =>
        Emitted.Value.GetType($"{GeneratedCodeCompilationHarness.AppName}.Business.Entities.{EntityName}")
            ?? throw new InvalidOperationException($"{EntityName} not found in the emitted assembly.");

    private static MethodInfo BuildMethod =>
        Emitted.Value.GetType($"{GeneratedCodeCompilationHarness.AppName}.Business.Filtering.PaginatedResultGenerator")!
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == "Build" && m.GetParameters()[0].ParameterType.GenericTypeArguments[0] == EntityType);

    /// <summary>
    /// Seeds <paramref name="rows"/> (Ids are assigned 1..N in order), runs the generated
    /// <c>Build</c> with <paramref name="filterDTO"/>, and materializes the resulting query. Exceptions
    /// thrown by <c>Build</c> surface unwrapped — the async state machine faults the returned task.
    /// </summary>
    internal static async Task<BuildOutcome> RunBuildAsync(FilterDTO filterDTO, params (string Title, int Rank)[] rows)
    {
        using SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();

        DbContextOptions<ProbeDbContext> options = new DbContextOptionsBuilder<ProbeDbContext>()
            .UseSqlite(connection)
            .Options;

        using ProbeDbContext context = new(options, EntityType);
        context.Database.EnsureCreated();

        PropertyInfo title = EntityType.GetProperty("Title")!;
        PropertyInfo rank = EntityType.GetProperty("Rank")!;

        foreach ((string Title, int Rank) row in rows)
        {
            object entity = Activator.CreateInstance(EntityType)!;
            title.SetValue(entity, row.Title);
            rank.SetValue(entity, row.Rank);
            context.Add(entity);
        }

        context.SaveChanges();

        object queryable = typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!
            .MakeGenericMethod(EntityType)
            .Invoke(context, null)!;

        Task task = (Task)BuildMethod.Invoke(null, [queryable, filterDTO])!;
        await task;

        object paginated = task.GetType().GetProperty("Result")!.GetValue(task)!;
        int totalRecords = (int)paginated.GetType().GetProperty("TotalRecords")!.GetValue(paginated)!;
        IEnumerable query = (IEnumerable)paginated.GetType().GetProperty("Query")!.GetValue(paginated)!;

        PropertyInfo id = EntityType.GetProperty("Id")!;
        List<ProbeRow> materialized = query.Cast<object>()
            .Select(row => new ProbeRow((int)id.GetValue(row)!, (string)title.GetValue(row)!, (int)rank.GetValue(row)!))
            .ToList();

        return new BuildOutcome(totalRecords, materialized);
    }

    private sealed class ProbeDbContext : DbContext
    {
        private readonly Type _entityType;

        public ProbeDbContext(DbContextOptions<ProbeDbContext> options, Type entityType) : base(options)
        {
            _entityType = entityType;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity(_entityType);
    }
}
