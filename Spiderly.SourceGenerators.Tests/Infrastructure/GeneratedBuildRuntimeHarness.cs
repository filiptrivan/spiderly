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

        // Warning+ threshold and Describe formatting match AssertCompilesClean's no-allowlist rule —
        // a SPIDERLY### warning must not pass the runtime harness while failing the compilation one.
        string[] generatorFailures = generatorDiagnostics
            .Where(d => d.Severity >= DiagnosticSeverity.Warning)
            .Select(GeneratedCodeCompilationHarness.Describe)
            .Distinct()
            .ToArray();

        if (generatorFailures.Length > 0)
            throw new InvalidOperationException($"Generators reported {generatorFailures.Length} diagnostic(s):\n{string.Join("\n", generatorFailures)}");

        using MemoryStream peStream = new();
        var emitResult = compilation.Emit(peStream);

        if (!emitResult.Success)
        {
            string errors = string.Join("\n", emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(GeneratedCodeCompilationHarness.Describe)
                .Distinct());

            throw new InvalidOperationException($"Emit failed:\n{errors}");
        }

        // The compilation references the SAME Spiderly.Shared / EF Core assembly files this test process
        // already has loaded, so the emitted assembly binds to them by identity — FilterDTO,
        // BusinessException and PaginatedResult<T> are shared types, directly usable from tests.
        return Assembly.Load(peStream.ToArray());
    }

    private static readonly Lazy<Type> EntityType = new(() =>
        Emitted.Value.GetType($"{GeneratedCodeCompilationHarness.AppName}.Business.Entities.{EntityName}")
            ?? throw new InvalidOperationException($"{EntityName} not found in the emitted assembly."));

    private static readonly Lazy<MethodInfo> BuildMethod = new(() =>
        Emitted.Value.GetType($"{GeneratedCodeCompilationHarness.AppName}.Business.Filtering.PaginatedResultGenerator")!
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == "Build" && m.GetParameters()[0].ParameterType.GenericTypeArguments[0] == EntityType.Value));

    /// <summary>
    /// Seeds <paramref name="rows"/> (Ids are assigned 1..N in order), runs the generated
    /// <c>Build</c> with <paramref name="filterDTO"/>, and materializes the resulting query. Exceptions
    /// thrown by <c>Build</c> surface unwrapped — the async state machine faults the returned task.
    /// The emitted types are public, so member access goes through <c>dynamic</c> rather than
    /// hand-rolled <c>PropertyInfo</c> lookups.
    /// </summary>
    internal static async Task<BuildOutcome> RunBuildAsync(FilterDTO filterDTO, params (string Title, int Rank)[] rows)
    {
        using SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();

        DbContextOptions<ProbeDbContext> options = new DbContextOptionsBuilder<ProbeDbContext>()
            .UseSqlite(connection)
            .Options;

        using ProbeDbContext context = new(options);
        context.Database.EnsureCreated();

        foreach ((string Title, int Rank) row in rows)
        {
            dynamic entity = Activator.CreateInstance(EntityType.Value)!;
            entity.Title = row.Title;
            entity.Rank = row.Rank;
            context.Add((object)entity);
        }

        context.SaveChanges();

        object queryable = typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!
            .MakeGenericMethod(EntityType.Value)
            .Invoke(context, null)!;

        dynamic paginated = await (dynamic)BuildMethod.Value.Invoke(null, [queryable, filterDTO])!;

        List<ProbeRow> materialized = new();
        foreach (dynamic row in (IEnumerable)paginated.Query)
            materialized.Add(new ProbeRow((int)row.Id, (string)row.Title, (int)row.Rank));

        return new BuildOutcome((int)paginated.TotalRecords, materialized);
    }

    private sealed class ProbeDbContext : DbContext
    {
        public ProbeDbContext(DbContextOptions<ProbeDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity(EntityType.Value);
    }
}
