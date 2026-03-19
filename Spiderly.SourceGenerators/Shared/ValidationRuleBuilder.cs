using Spiderly.SourceGenerators.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Spiderly.SourceGenerators.Shared
{
    public static class ValidationRuleBuilder
    {
        public static List<SpiderValidationRule> GetValidationRules(List<SpiderlyProperty> DTOProperties, List<SpiderlyAttribute> DTOAttributes, SpiderlyClass entity)
        {
            List<SpiderValidationRule> rulesOnDTO = new(); // priority - 1.
            List<SpiderValidationRule> rulesOnDTOProperties = new(); // priority - 2.
            List<SpiderValidationRule> rulesOnEntity = new(); // priority - 3.
            List<SpiderValidationRule> rulesOnEntityProperties = new(); // priority - 4.

            rulesOnDTO.AddRange(GetRulesForAttributes(DTOAttributes, DTOProperties));

            foreach (SpiderlyProperty DTOproperty in DTOProperties)
            {
                SpiderValidationRule rule = GetRuleForProperty(DTOproperty, DTOProperties);

                if (rule != null)
                    rulesOnDTOProperties.Add(rule);
            }

            if (entity != null) // If it is null then we only made DTO, without entity class
            {
                rulesOnEntity.AddRange(GetRulesForAttributes(entity.Attributes, DTOProperties));

                foreach (SpiderlyProperty property in entity.Properties)
                {
                    SpiderValidationRule rule = GetRuleForProperty(property, DTOProperties);

                    if (rule != null)
                        rulesOnEntityProperties.Add(rule);
                }
            }

            List<SpiderValidationRule> mergedValidationRules = GetMergedValidationRules(rulesOnDTO, rulesOnDTOProperties, rulesOnEntity, rulesOnEntityProperties, DTOProperties);

            return mergedValidationRules;
        }

        /// <summary>
        /// Passing <paramref name="DTOProperties"/> because we are always validating only DTO with FluentValidation
        /// </summary>
        private static List<SpiderValidationRule> GetRulesForAttributes(List<SpiderlyAttribute> attributes, List<SpiderlyProperty> DTOProperties)
        {
            List<SpiderValidationRule> rules = new();

            foreach (SpiderlyAttribute attribute in attributes)
            {
                if (attribute.Name == "CustomValidator")
                {
                    string rulePropertyName = ParsePropertyNameFromCustomClassValidator(attribute.Value);

                    rules.Add(new SpiderValidationRule
                    {
                        Property = DTOProperties.Single(x => x.Name == rulePropertyName),
                        ValidationRuleParts = GetValidationRulePartsForCustomClassValidator(attribute.Value),
                    });
                }
            }

            return rules;
        }

        /// <summary>
        /// RuleFor(x => x.GetTransactionsEndpoint).Length(1, 1000).Unless(i => string.IsNullOrEmpty(i.GetTransactionsEndpoint)); -> GetTransactionsEndpoint
        /// </summary>
        private static string ParsePropertyNameFromCustomClassValidator(string rule)
        {
            int dotIndex = rule.IndexOf(".");
            int parenIndex = rule.IndexOf(")", dotIndex);

            return rule.Substring(dotIndex + 1, parenIndex - dotIndex - 1);
        }

        private static List<SpiderValidationRulePart> GetValidationRulePartsForCustomClassValidator(string rule)
        {
            List<string> rulePartsWithValues = rule.Split(").").Skip(1).SkipLast().ToList();
            string lastRulePart = rule.Split(").").Last().Replace(");", "");
            rulePartsWithValues.Add(lastRulePart);

            return rulePartsWithValues
                .Select(rulePart => new SpiderValidationRulePart
                {
                    Name = GetRulePartName(rulePart),
                    MethodParametersBody = GetRulePartMethodParametersBody(rulePart),
                })
                .ToList();
        }

        private static SpiderValidationRule GetRuleForProperty(SpiderlyProperty property, List<SpiderlyProperty> DTOProperties)
        {
            if (property.Type.IsEnumerable() && !property.Attributes.Any(x => x.Name == "Required"))
                return null;

            string rulePropertyName = GetRulePropertyName(property);
            SpiderlyProperty dtoProperty = DTOProperties.SingleOrDefault(x => x.Name == rulePropertyName);

            if (dtoProperty == null)
                return null;

            List<SpiderValidationRulePart> ruleParts = GetRulePartsForProperty(property, rulePropertyName); // NotEmpty(), Length(0, 70);

            if (ruleParts.Count == 0)
                return null;

            return new SpiderValidationRule
            {
                Property = dtoProperty,
                ValidationRuleParts = ruleParts
            };
        }

        private static string GetRulePropertyName(SpiderlyProperty property)
        {
            if (property.HasWithManyAttribute() && property.Type.IsManyToOneType())  // FT: if it is not base type and not enumerable than it's many to one for sure, and the validation can only be for id to be required
                return $"{property.Name}Id";

            if (property.HasUIOrderedOneToManyAttribute())
                return $"Ordered{property.Name}SaveBodyDTO";

            return property.Name;
        }

        private static List<SpiderValidationRulePart> GetRulePartsForProperty(SpiderlyProperty property, string rulePropertyName)
        {
            List<SpiderValidationRulePart> ruleParts = new();

            foreach (SpiderlyAttribute attribute in property.Attributes)
            {
                switch (attribute.Name)
                {
                    case "Required":
                        ruleParts.Add(new SpiderValidationRulePart
                        {
                            Name = "NotEmpty",
                            MethodParametersBody = ""
                        });
                        break;
                    case "StringLength":
                        string minValue = FindMinValueForStringLength(attribute.Value);
                        ruleParts.Add(new SpiderValidationRulePart
                        {
                            Name = "Length",
                            MethodParametersBody = minValue == null ? $"{FindMaxValueForStringLength(attribute.Value)}" : $"{minValue}, {FindMaxValueForStringLength(attribute.Value)}"
                        });
                        break;
                    case "Precision":
                        ruleParts.Add(new SpiderValidationRulePart
                        {
                            Name = "PrecisionScale",
                            MethodParametersBody = $"{attribute.Value}, false" // Only here the attribute.Value should be two values eg. 6, 7
                        });
                        break;
                    case "Range":
                        ruleParts.Add(new SpiderValidationRulePart
                        {
                            Name = "GreaterThanOrEqualTo",
                            MethodParametersBody = attribute.Value.Split(',')[0].Trim()
                        });
                        ruleParts.Add(new SpiderValidationRulePart
                        {
                            Name = "LessThanOrEqualTo",
                            MethodParametersBody = attribute.Value.Split(',')[1].Trim()
                        });
                        break;
                    case "GreaterThanOrEqualTo":
                        ruleParts.Add(new SpiderValidationRulePart
                        {
                            Name = "GreaterThanOrEqualTo",
                            MethodParametersBody = attribute.Value
                        });
                        break;
                    case "EmailAddress":
                        ruleParts.Add(new SpiderValidationRulePart
                        {
                            Name = "EmailAddress",
                            MethodParametersBody = ""
                        });
                        break;
                    case "CustomValidator":
                        ruleParts.Add(new SpiderValidationRulePart
                        {
                            Name = GetRulePartName(attribute.Value),
                            MethodParametersBody = GetRulePartMethodParametersBody(attribute.Value)
                        });
                        break;
                    default:
                        break;
                }
            }

            // If there is no Required attribute, we should let user save null to database
            if (ruleParts.Count > 0 && property.Attributes.Any(x => x.Name == "Required") == false)
            {
                if (property.Type == "string")
                {
                    ruleParts.Add(new SpiderValidationRulePart
                    {
                        Name = "Unless",
                        MethodParametersBody = $"i => string.IsNullOrEmpty(i.{rulePropertyName})"
                    });
                }
                else
                {
                    ruleParts.Add(new SpiderValidationRulePart
                    {
                        Name = "Unless",
                        MethodParametersBody = $"i => i.{rulePropertyName} == null"
                    });
                }
            }

            return ruleParts;
        }

        private static string GetRulePartName(string rulePart)
        {
            return rulePart.Substring(0, rulePart.IndexOf("("));
        }

        private static string GetRulePartMethodParametersBody(string rulePartWithoutLastParen)
        {
            if (rulePartWithoutLastParen.Length > 0 && rulePartWithoutLastParen[rulePartWithoutLastParen.Length - 1] == ')')
                rulePartWithoutLastParen = rulePartWithoutLastParen.Substring(0, rulePartWithoutLastParen.Length - 1);

            return rulePartWithoutLastParen.Substring(rulePartWithoutLastParen.IndexOf("(") + 1);
        }

        /// <summary>
        /// Getting merged validation rules for the single object (DTO + Entity)
        /// </summary>
        /// <returns></returns>
        private static List<SpiderValidationRule> GetMergedValidationRules(
            List<SpiderValidationRule> rulesOnDTO,
            List<SpiderValidationRule> rulesOnDTOProperties,
            List<SpiderValidationRule> rulesOnEntity,
            List<SpiderValidationRule> rulesOnEntityProperties,
            List<SpiderlyProperty> DTOProperties
        )
        {
            List<SpiderValidationRule> mergedRules = new();

            foreach (IGrouping<string, SpiderValidationRule> ruleGroup in rulesOnDTO.Concat(rulesOnDTOProperties).Concat(rulesOnEntity).Concat(rulesOnEntityProperties).GroupBy(x => x.Property.Name))
            {
                List<SpiderValidationRulePart> rulePartsOnDTO = rulesOnDTO.Where(x => x.Property.Name == ruleGroup.Key).SelectMany(x => x.ValidationRuleParts).ToList();
                List<SpiderValidationRulePart> rulePartsOnDTOProperties = rulesOnDTOProperties.Where(x => x.Property.Name == ruleGroup.Key).SelectMany(x => x.ValidationRuleParts).ToList();
                List<SpiderValidationRulePart> rulePartsOnEntity = rulesOnEntity.Where(x => x.Property.Name == ruleGroup.Key).SelectMany(x => x.ValidationRuleParts).ToList();
                List<SpiderValidationRulePart> rulePartsOnEntityProperties = rulesOnEntityProperties.Where(x => x.Property.Name == ruleGroup.Key).SelectMany(x => x.ValidationRuleParts).ToList();

                RemoveDuplicateRuleParts([rulePartsOnDTOProperties, rulePartsOnEntity, rulePartsOnEntityProperties], rulePartsOnDTO);
                RemoveDuplicateRuleParts([rulePartsOnEntity, rulePartsOnEntityProperties], rulePartsOnDTOProperties);
                RemoveDuplicateRuleParts([rulePartsOnEntityProperties], rulePartsOnEntity);

                List<SpiderValidationRulePart> mergedRuleParts = rulePartsOnDTO.Concat(rulePartsOnDTOProperties).Concat(rulePartsOnEntity).Concat(rulePartsOnEntityProperties).ToList();

                mergedRules.Add(new SpiderValidationRule
                {
                    Property = DTOProperties.Where(x => x.Name == ruleGroup.Key).Single(),
                    ValidationRuleParts = mergedRuleParts
                });
            }

            return mergedRules;
        }

        private static void RemoveDuplicateRuleParts(List<List<SpiderValidationRulePart>> rulePartsToRemove, List<SpiderValidationRulePart> priorRuleParts)
        {
            List<string> priorRulePartNames = priorRuleParts.Select(x => x.Name).ToList();

            foreach (List<SpiderValidationRulePart> ruleParts in rulePartsToRemove)
                ruleParts.RemoveAll(part => priorRulePartNames.Any(name => part.Name == name));
        }

        /// <summary>
        /// </summary>
        /// <param name="input">"70, MinimumLength = 5"</param>
        /// <returns></returns>
        internal static string FindMinValueForStringLength(string input)
        {
            string pattern = @"MinimumLength\s*=\s*(\d+)";

            Match match = Regex.Match(input, pattern);

            if (match.Success)
                return match.Groups[1].Value;
            else
                return null;
        }

        /// <summary>
        /// </summary>
        /// <param name="input">"70, MinimumLength = 5"</param>
        /// <returns></returns>
        internal static string FindMaxValueForStringLength(string input)
        {
            return input.Split(',').First().Replace(" ", "");
        }
    }
}
