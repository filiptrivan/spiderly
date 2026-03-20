using Spiderly.SourceGenerators.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Spiderly.SourceGenerators.Shared
{
    public static class ValidationRuleBuilder
    {
        public static List<SpiderValidationRule> GetValidationRules(List<SpiderlyProperty> DTOProperties, SpiderlyClass entity)
        {
            List<SpiderValidationRule> rulesOnDTOProperties = new();
            List<SpiderValidationRule> rulesOnEntityProperties = new();

            foreach (SpiderlyProperty DTOproperty in DTOProperties)
            {
                SpiderValidationRule rule = GetRuleForProperty(DTOproperty, DTOProperties);

                if (rule != null)
                    rulesOnDTOProperties.Add(rule);
            }

            if (entity != null)
            {
                foreach (SpiderlyProperty property in entity.Properties)
                {
                    SpiderValidationRule rule = GetRuleForProperty(property, DTOProperties);

                    if (rule != null)
                        rulesOnEntityProperties.Add(rule);
                }
            }

            List<SpiderValidationRule> mergedValidationRules = GetMergedValidationRules(rulesOnDTOProperties, rulesOnEntityProperties, DTOProperties);

            return mergedValidationRules;
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
                        string maxValue = FindMaxValueForStringLength(attribute.Value);
                        if (minValue == null)
                        {
                            ruleParts.Add(new SpiderValidationRulePart
                            {
                                Name = "MaximumLength",
                                MethodParametersBody = maxValue
                            });
                        }
                        else if (minValue == maxValue)
                        {
                            ruleParts.Add(new SpiderValidationRulePart
                            {
                                Name = "Length",
                                MethodParametersBody = minValue
                            });
                        }
                        else
                        {
                            ruleParts.Add(new SpiderValidationRulePart
                            {
                                Name = "Length",
                                MethodParametersBody = $"{minValue}, {maxValue}"
                            });
                        }
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
                    case "Email":
                        ruleParts.Add(new SpiderValidationRulePart
                        {
                            Name = "EmailAddress",
                            MethodParametersBody = ""
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

        /// <summary>
        /// Getting merged validation rules for the single object (DTO + Entity).
        /// DTO property rules take priority — duplicate rule parts from entity properties are removed.
        /// </summary>
        private static List<SpiderValidationRule> GetMergedValidationRules(
            List<SpiderValidationRule> rulesOnDTOProperties,
            List<SpiderValidationRule> rulesOnEntityProperties,
            List<SpiderlyProperty> DTOProperties
        )
        {
            List<SpiderValidationRule> mergedRules = new();

            foreach (IGrouping<string, SpiderValidationRule> ruleGroup in rulesOnDTOProperties.Concat(rulesOnEntityProperties).GroupBy(x => x.Property.Name))
            {
                List<SpiderValidationRulePart> rulePartsOnDTOProperties = rulesOnDTOProperties.Where(x => x.Property.Name == ruleGroup.Key).SelectMany(x => x.ValidationRuleParts).ToList();
                List<SpiderValidationRulePart> rulePartsOnEntityProperties = rulesOnEntityProperties.Where(x => x.Property.Name == ruleGroup.Key).SelectMany(x => x.ValidationRuleParts).ToList();

                RemoveDuplicateRuleParts([rulePartsOnEntityProperties], rulePartsOnDTOProperties);

                List<SpiderValidationRulePart> mergedRuleParts = rulePartsOnDTOProperties.Concat(rulePartsOnEntityProperties).ToList();

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
