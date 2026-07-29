using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;
using System.Collections.Generic;
using System.Linq;

namespace Spiderly.SourceGenerators.Net
{
    internal static class ServiceDeleteGenerator
    {
        // Emitted after the OnBefore...Delete and OnAfter...Delete hooks in both generated delete
        // methods: flushes any tracked write the hook staged (e.g. IOutbox.Enqueue) — the next step
        // (the untracked ExecuteDeleteAsync cascade, or the commit itself) never flushes the tracker,
        // and WithTransactionAsync's clean-tracker-at-commit guard would throw. Interpolated at
        // column 0 (like GetManyToOneDeleteQueries) — the snippet carries its own indentation.
        private const string FlushStagedHookWritesSnippet = """
                // Persist writes the hook staged (e.g. IOutbox.Enqueue) as part of this
                // transaction; the next step (untracked cascade delete, or the commit) won't
                // flush them and WithTransactionAsync's clean-tracker guard would throw.
                if (_deps.Context.ChangeTracker.HasChanges())
                    await _deps.Context.SaveChangesAsync();
""";

        internal static List<string> GetDeletingData(SpiderlyClass entity, List<SpiderlyClass> allEntities)
        {
            if (entity.IsAbstract || entity.IsReadonlyObject())
                return new List<string>();

            List<string> result = new();

            result.Add(GetDeleteEntityData(entity, allEntities));

            result.Add(GetDeleteEntityListData(entity, allEntities));

            return result;
        }

        private static string GetDeleteEntityData(SpiderlyClass entity, List<SpiderlyClass> allEntities)
        {
            string entityIdType = entity.GetIdType(allEntities);
            int deleteIterator = 1;

            return $$"""
        /// <summary>
        /// Per-id variant of the pre-delete hook. By default forwards to
        /// <see cref="OnBefore{{entity.Name}}ListDelete"/> with a one-element list, so override
        /// only the list hook unless single-id and batch flows genuinely diverge.
        /// </summary>
        /// <param name="id">The ID of the entity being deleted</param>
        public virtual Task OnBefore{{entity.Name}}Delete({{entityIdType}} id) =>
            OnBefore{{entity.Name}}ListDelete(id.StructToList());

        /// <summary>
        /// Per-id variant of the post-delete hook. By default forwards to
        /// <see cref="OnAfter{{entity.Name}}ListDelete"/> with a one-element list, so override
        /// only the list hook unless single-id and batch flows genuinely diverge.
        /// </summary>
        /// <param name="id">The ID of the entity that was deleted</param>
        public virtual Task OnAfter{{entity.Name}}Delete({{entityIdType}} id) =>
            OnAfter{{entity.Name}}ListDelete(id.StructToList());

        /// <summary>
        /// Deletes a single {{entity.Name}} entity with cascade delete handling for dependent entities.
        /// </summary>
        /// <param name="id">The ID of the entity to delete</param>
        public async virtual Task Delete{{entity.Name}}({{entityIdType}} id)
        {
            await _deps.Context.WithTransactionAsync(async () =>
            {
                await OnBefore{{entity.Name}}Delete(id);

{{FlushStagedHookWritesSnippet}}

                List<{{entityIdType}}> listForDelete_{{deleteIterator}} = id.StructToList();

{{string.Join("\n\n", GetManyToOneDeleteQueries(entity, allEntities, "listForDelete", deleteIterator))}}

                await DeleteEntityAsync<{{entity.Name}}, {{entityIdType}}>(id);

                await OnAfter{{entity.Name}}Delete(id);

{{FlushStagedHookWritesSnippet}}
            });
        }
""";
        }

        private static string GetDeleteEntityListData(SpiderlyClass entity, List<SpiderlyClass> allEntities)
        {
            string entityIdType = entity.GetIdType(allEntities);
            int deleteIterator = 1;

            return $$"""
        /// <summary>
        /// Lifecycle hook called before deleting a list of {{entity.Name}} entities.
        /// Override this to add custom validation or business logic before batch deletion.
        /// </summary>
        /// <param name="listForDelete">The list of entity IDs being deleted</param>
        public virtual async Task OnBefore{{entity.Name}}ListDelete(List<{{entityIdType}}> listForDelete) { }

        /// <summary>
        /// Lifecycle hook called after deleting a list of {{entity.Name}} entities (cascades included),
        /// still inside the delete transaction — queries observe the post-delete state, and anything
        /// written here commits or rolls back atomically with the delete.
        /// Override this to recompute denormalized aggregates or stage post-delete work.
        /// </summary>
        /// <param name="deletedIds">The list of entity IDs that were deleted</param>
        /// <example>
        /// public override async Task OnAfter{{entity.Name}}ListDelete(List&lt;{{entityIdType}}&gt; deletedIds)
        /// {
        ///     await RecalculateAggregatesAsync(); // reads the post-delete state
        /// }
        /// </example>
        public virtual async Task OnAfter{{entity.Name}}ListDelete(List<{{entityIdType}}> deletedIds) { }

        /// <summary>
        /// Deletes multiple {{entity.Name}} entities with cascade delete handling for dependent entities.
        /// </summary>
        /// <param name="listForDelete_{{deleteIterator}}">The list of entity IDs to delete</param>
        public async virtual Task Delete{{entity.Name}}List(List<{{entityIdType}}> listForDelete_{{deleteIterator}})
        {
            await _deps.Context.WithTransactionAsync(async () =>
            {
                await OnBefore{{entity.Name}}ListDelete(listForDelete_{{deleteIterator}});

{{FlushStagedHookWritesSnippet}}

{{string.Join("\n\n", GetManyToOneDeleteQueries(entity, allEntities, "listForDelete", deleteIterator))}}

                await DeleteEntitiesAsync<{{entity.Name}}, {{entityIdType}}>(listForDelete_{{deleteIterator}});

                await OnAfter{{entity.Name}}ListDelete(listForDelete_{{deleteIterator}});

{{FlushStagedHookWritesSnippet}}
            });
        }
""";
        }

        private static List<string> GetManyToOneDeleteQueries(SpiderlyClass entity, List<SpiderlyClass> allEntities, string listForDeleteVariableName, int deleteIterator)
        {
            if (deleteIterator > 5000)
                return new List<string> { "You made cascade delete infinite loop." };

            List<string> result = new();

            List<SpiderlyProperty> cascadeDeleteProperties = Helpers.GetCascadeDeleteProperties(entity.Name, allEntities);

            foreach (SpiderlyProperty property in cascadeDeleteProperties)
            {
                SpiderlyClass parentEntity = allEntities.SingleOrDefault(x => x.Name == property.EntityName);

                if (parentEntity.IsManyToMany())
                {
                    result.Add($$"""
                await _deps.Context.DbSet<{{parentEntity.Name}}>()
                    .Where(x => {{listForDeleteVariableName}}_{{deleteIterator}}.Contains({{property.GetForeignKeyAccessExpression(parentEntity, allEntities)}}))
                    .ExecuteDeleteAsync();
""");

                    continue; // FT: Continue because M2M could never be required
                }
                else
                {
                    result.Add($$"""
                var {{parentEntity.Name.FirstCharToLower()}}ListForDeleteBecause{{property.Name}}_{{deleteIterator + 1}} = await _deps.Context.DbSet<{{parentEntity.Name}}>()
                    .AsNoTracking()
                    .Where(x => {{listForDeleteVariableName}}_{{deleteIterator}}.Contains({{property.GetForeignKeyAccessExpression(parentEntity, allEntities)}}))
                    .Select(x => x.Id)
                    .ToListAsync();
""");

                }

                result.AddRange(GetManyToOneDeleteQueries(parentEntity, allEntities, $"{parentEntity.Name.FirstCharToLower()}ListForDeleteBecause{property.Name}", deleteIterator + 1));

                result.Add($$"""
                await _deps.Context.DbSet<{{parentEntity.Name}}>()
                    .Where(x => {{parentEntity.Name.FirstCharToLower()}}ListForDeleteBecause{{property.Name}}_{{deleteIterator + 1}}.Contains(x.Id))
                    .ExecuteDeleteAsync();
""");
            }

            return result;
        }
    }
}
