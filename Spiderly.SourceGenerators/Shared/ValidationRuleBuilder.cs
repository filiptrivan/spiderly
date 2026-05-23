using Spiderly.SourceGenerators.Models;
using System;
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
                SpiderValidationRule rule = GetRuleForProperty(DTOproperty, DTOProperties, entity);

                if (rule != null)
                    rulesOnDTOProperties.Add(rule);
            }

            if (entity != null)
            {
                foreach (SpiderlyProperty property in entity.Properties)
                {
                    SpiderValidationRule rule = GetRuleForProperty(property, DTOProperties, entity);

                    if (rule != null)
                        rulesOnEntityProperties.Add(rule);
                }
            }

            List<SpiderValidationRule> mergedValidationRules = GetMergedValidationRules(rulesOnDTOProperties, rulesOnEntityProperties, DTOProperties);

            return mergedValidationRules;
        }

        private static SpiderValidationRule GetRuleForProperty(SpiderlyProperty property, List<SpiderlyProperty> DTOProperties, SpiderlyClass entity)
        {
            if (property.Type.IsEnumerable() && !property.Attributes.Any(x => x.Name == "Required"))
                return null;

            string rulePropertyName = GetRulePropertyName(property, entity);
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

        private static string GetRulePropertyName(SpiderlyProperty property, SpiderlyClass entity)
        {
            if (property.HasWithManyAttribute() && property.IsManyToOneType())  // FT: if it is not base type and not enumerable than it's many to one for sure, and the validation can only be for id to be required
                return property.ResolveExplicitForeignKeyName(entity) ?? $"{property.Name}Id";

            if (property.HasUIOrderedOneToManyAttribute())
                return $"Ordered{property.Name}SaveBodyDTO";

            return property.Name;
        }

        internal static List<SpiderValidationRulePart> GetRulePartsForProperty(SpiderlyProperty property, string rulePropertyName)
        {
            List<SpiderValidationRulePart> ruleParts = new();

            // `[M2MWithMany]` on a junction entity implies required — junction rows can't exist
            // with a null side — but Spiderly's M2M template never writes `[Required]`. Emit the
            // NotEmpty rule here so the switch below doesn't need to special-case it.
            if (property.IsEffectivelyRequired() && property.HasRequiredAttribute() == false)
                ruleParts.Add(new NotEmptyRulePart());

            foreach (SpiderlyAttribute attribute in property.Attributes)
            {
                switch (attribute.Name)
                {
                    case "Required":
                        ruleParts.Add(new NotEmptyRulePart());
                        break;
                    case "StringLength":
                        string minValue = FindMinValueForStringLength(attribute.Value);
                        string maxValue = FindMaxValueForStringLength(attribute.Value);
                        if (minValue == null)
                        {
                            ruleParts.Add(new MaximumLengthRulePart(int.Parse(maxValue)));
                        }
                        else if (minValue == maxValue)
                        {
                            ruleParts.Add(new ExactLengthRulePart(int.Parse(minValue)));
                        }
                        else
                        {
                            ruleParts.Add(new LengthRangePart(int.Parse(minValue), int.Parse(maxValue)));
                        }
                        break;
                    case "Precision":
                        string[] precisionParts = attribute.Value.Split(',');
                        ruleParts.Add(new PrecisionScaleRulePart(
                            int.Parse(precisionParts[0].Trim()),
                            int.Parse(precisionParts[1].Trim())
                        ));
                        break;
                    case "Range":
                        string[] rangeParts = attribute.Value.Split(',');
                        ruleParts.Add(new GreaterThanOrEqualToRulePart(rangeParts[0].Trim()));
                        ruleParts.Add(new LessThanOrEqualToRulePart(rangeParts[1].Trim()));
                        break;
                    case "GreaterThanOrEqualTo":
                        ruleParts.Add(new GreaterThanOrEqualToRulePart(attribute.Value));
                        break;
                    case "Email":
                        ruleParts.Add(new EmailAddressRulePart());
                        break;
                    default:
                        break;
                }
            }

            // If there is no Required attribute, we should let user save null to database
            if (ruleParts.Count > 0 && property.IsEffectivelyRequired() == false)
            {
                if (property.Type.Raw == "string")
                {
                    ruleParts.Add(new UnlessRulePart($"i => string.IsNullOrEmpty(i.{rulePropertyName})"));
                }
                else
                {
                    ruleParts.Add(new UnlessRulePart($"i => i.{rulePropertyName} == null"));
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
            List<Type> priorRulePartTypes = priorRuleParts.Select(x => x.GetType()).ToList();

            foreach (List<SpiderValidationRulePart> ruleParts in rulePartsToRemove)
                ruleParts.RemoveAll(part => priorRulePartTypes.Any(type => part.GetType() == type));
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
