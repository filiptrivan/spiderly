<!-- GENERATED FROM framework-metadata.json — DO NOT EDIT.
     Regenerate: `dotnet run --project Spiderly.MetadataExporter -- --out framework-metadata.json && node tools/extract-ts-metadata.mjs && node tools/gen-skill-docs.mjs` -->

# Filter match modes

String constants for the comparison operators a filter rule can use (carried on FilterRuleDTO.MatchMode). The values mirror PrimeNG's table filter match modes, so the same code drives both the Angular UI and the server-side query translation in the generated paginated-list logic.

| Name | Value | Description |
| --- | --- | --- |
| `Contains` | `contains` | String substring match, case-insensitive (value.Contains(...)). |
| `Equals` | `equals` | Equality match. For strings it is case-insensitive; for bool, number, and date/time properties it is an exact == comparison. |
| `GreaterThan` | `greaterThan` | Greater-than comparison (>), for number and date/time properties. |
| `In` | `in` | Membership match against a JSON array of values (value IN [...]), for number and id properties. |
| `LessThan` | `lessThan` | Less-than comparison (<), for number and date/time properties. |
| `StartsWith` | `startsWith` | String prefix match, case-insensitive (value.StartsWith(...)). |
