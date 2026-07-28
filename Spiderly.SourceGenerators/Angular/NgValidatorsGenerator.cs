using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using Spiderly.SourceGenerators.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Enums;

namespace Spiderly.SourceGenerators.Angular
{
    /// <summary>
    /// Generates an Angular `ValidatorServiceGenerated` (`validators.generated.ts`)
    /// within the `{your-app-name}\Frontend\src\app\business\services\validators` directory.
    /// This service provides methods to dynamically set Angular form validators based on validation attributes
    /// defined on your C# Entity and DTO properties.
    /// </summary>
    [Generator]
    public class NgValidatorsGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            //#if DEBUG
            //            if (!Debugger.IsAttached)
            //            {
            //                Debugger.Launch();
            //            }
            //#endif
            var combined = PipelineFactory.CreatePipelineWithCallingPath(context,
                new List<ClassCategoryCodes> { ClassCategoryCodes.Entities, ClassCategoryCodes.DTO },
                new List<ClassCategoryCodes> { ClassCategoryCodes.Entities, ClassCategoryCodes.DTO });

            context.RegisterSafeImplementationSourceOutput(combined, static (spc, source) =>
            {
                var (withConfig, _) = source; // nullable context — TS emission is annotation-agnostic
                var (classesAndEntitiesAndPath, config) = withConfig;
                var (classesAndEntities, callingPath) = classesAndEntitiesAndPath;
                var (classes, referencedClasses) = classesAndEntities;

                Execute(classes, referencedClasses, callingPath, config, spc);
            });
        }

        private static void Execute(IList<ClassDeclarationSyntax> classes, List<SpiderlyClass> referencedProjectClasses, string callingProjectDirectory, SpiderlyConfig config, SourceProductionContext context)
        {
            if (referencedProjectClasses.Count == 0)
                return;

            if (!config.IsGeneratorEnabled(nameof(NgValidatorsGenerator)))
                return;

            if (callingProjectDirectory.Contains(".WebAPI") == false)
                return;

            // ...\Backend\PlayertyLoyals.Business -> ...\Frontend\src\app\business\services\validators
            string rootPath = callingProjectDirectory.GetRootPath();
            string outputPath = Path.Combine(rootPath, "Frontend", "src", "app", "business", "services", "validators");

            referencedProjectClasses = referencedProjectClasses.OrderBy(x => x.Name).ToList();

            List<SpiderlyClass> entities = referencedProjectClasses.Where(x => x.HasSpiderlyEntityAttribute()).ToList();
            List<SpiderlyClass> dtoClasses = referencedProjectClasses.Where(x => x.HasSpiderlyDTOAttribute()).ToList();

            List<string> formControlSwitchCases = new();
            List<string> validatorMethods = new();
            List<string> formArraySwitchCases = new();

            foreach (SpiderlyClass dtoClass in dtoClasses)
            {
                SpiderlyClass entityClass = entities.SingleOrDefault(x =>
                    SpiderlyNaming.IsGeneratedDTOName(dtoClass.Name, x.Name, "DTO", "SaveBodyDTO")
                ); // If it is null then we only made DTO, without entity class

                List<SpiderValidationRule> rules = ValidationRuleBuilder.GetValidationRules(dtoClass.Properties, entityClass);
                List<SpiderValidationRule> formControlRules = rules.Where(r => !r.Property.Type.IsEnumerable()).ToList();
                List<SpiderValidationRule> formArrayRules = rules.Where(r => r.Property.Type.IsEnumerable()).ToList();

                string formControlSwitchCase = GenerateFormControlSwitchCases(dtoClass, formControlRules);
                if (string.IsNullOrEmpty(formControlSwitchCase) == false)
                    formControlSwitchCases.Add(formControlSwitchCase);

                string methods = GenerateValidatorMethods(dtoClass, formControlRules);
                if (string.IsNullOrEmpty(methods) == false)
                    validatorMethods.Add(methods);

                string formArraySwitchCase = GenerateFormArraySwitchCases(dtoClass, formArrayRules);
                if (string.IsNullOrEmpty(formArraySwitchCase) == false)
                    formArraySwitchCases.Add(formArraySwitchCase);
            }

            string result = $$"""
import { Injectable } from '@angular/core';
import { ValidationErrors } from '@angular/forms';
import { TranslocoService } from '@jsverse/transloco';
import {
    SpiderlyFormArray,
    SpiderlyFormControl,
    SpiderlyValidatorFn,
    validatePrecisionScale,
    ValidatorAbstractService,
} from 'spiderly';

@Injectable({
    providedIn: 'root',
})
export class ValidatorServiceGenerated extends ValidatorAbstractService {
    constructor(protected override translocoService: TranslocoService) {
        super(translocoService);
    }

    setValidator = (formControl: SpiderlyFormControl, className: string): SpiderlyValidatorFn => {
        switch(formControl.label + className){
{{string.Join("\n", formControlSwitchCases)}}
            default:
                return null;
        }
    }

    setFormArrayValidator = (formArray: SpiderlyFormArray, className: string): void => {
        switch(formArray.label + className){
{{string.Join("\n", formArraySwitchCases)}}
            default:
                return;
        }
    }

{{string.Join("\n", validatorMethods)}}

}
""";

            Helpers.WriteToTheFile(result, Path.Combine(outputPath, "validators.generated.ts"));
        }

        private static string GenerateFormControlSwitchCases(SpiderlyClass dtoClass, List<SpiderValidationRule> rules)
        {
            string entityName = Helpers.RemoveDtoSuffix(dtoClass.Name);

            StringBuilder sb = new();

            foreach (SpiderValidationRule rule in rules)
            {
                sb.AppendLine($$"""
            case '{{rule.Property.Name.FirstCharToLower()}}{{entityName}}':
                return this.{{rule.Property.Name.FirstCharToLower()}}{{entityName}}Validator(formControl);
""");
            }

            return sb.ToString();
        }

        private static string GenerateFormArraySwitchCases(SpiderlyClass dtoClass, List<SpiderValidationRule> rules)
        {
            string entityName = Helpers.RemoveDtoSuffix(dtoClass.Name);

            StringBuilder sb = new();

            foreach (SpiderValidationRule rule in rules)
            {
                sb.AppendLine($$"""
            case '{{rule.Property.Name.FirstCharToLower()}}{{entityName}}':
                this.isFormArrayEmpty(formArray);
                return;
""");
            }

            return sb.ToString();
        }

        private static string GenerateValidatorMethods(SpiderlyClass dtoClass, List<SpiderValidationRule> rules)
        {
            string entityName = Helpers.RemoveDtoSuffix(dtoClass.Name);

            StringBuilder sb = new();

            foreach (SpiderValidationRule rule in rules)
                sb.AppendLine(GenerateValidatorMethod(rule, entityName));

            return sb.ToString();
        }

        private static string GenerateValidatorMethod(SpiderValidationRule rule, string entityName)
        {
            ValidationMethodParts parts = new();

            PopulateValidationParts(rule, parts);

            string allRuleConditions = string.Join(" && ", parts.RuleNames);

            string result = $$"""
    {{rule.Property.Name.FirstCharToLower()}}{{entityName}}Validator = (control: SpiderlyFormControl): SpiderlyValidatorFn => {
        const validator: SpiderlyValidatorFn = (): ValidationErrors | null => {
            const value = control.value;

{{string.Join("\n", parts.RuleStatements)}}

            const valid = {{allRuleConditions}};

            return valid ? null : { _ : this.translocoService.translate('{{BuildTranslationKey(parts.TranslationTags)}}', {{{string.Join(", ", parts.TranslocoVariables)}}}) };
        };
{{GenerateNotEmptyMarkers(parts.RuleNames)}}
        control.validator = validator;
{{GenerateDateValidityUpdate(rule.Property)}}
        return validator;
    }

""";

            return result;
        }

        private static void PopulateValidationParts(SpiderValidationRule rule, ValidationMethodParts parts)
        {
            foreach (SpiderValidationRulePart rulePart in rule.ValidationRuleParts)
            {
                switch (rulePart)
                {
                    case NotEmptyRulePart:
                        AddNotEmptyRule(rule.Property, parts);
                        break;
                    case LengthRangePart p:
                        AddLengthRangeRule(p, parts);
                        break;
                    case ExactLengthRulePart p:
                        AddExactLengthRule(p, parts);
                        break;
                    case MaximumLengthRulePart p:
                        AddMaximumLengthRule(p, parts);
                        break;
                    case LessThanOrEqualToRulePart p:
                        AddLessThanOrEqualToRule(p, parts);
                        break;
                    case GreaterThanOrEqualToRulePart p:
                        AddGreaterThanOrEqualToRule(p, parts);
                        break;
                    case NotHaveWhiteSpaceRulePart:
                        AddNotHaveWhiteSpaceRule(parts);
                        break;
                    case EmailAddressRulePart:
                        AddEmailAddressRule(parts);
                        break;
                    case PrecisionScaleRulePart p:
                        AddPrecisionScaleRule(p, parts);
                        break;
                    case UnlessRulePart:
                        break; // Angular doesn't use Unless
                    default:
                        throw new NotSupportedException($"Unknown validation rule part: {rulePart.GetType().Name}");
                }
            }
        }

        private static void AddNotEmptyRule(SpiderlyProperty property, ValidationMethodParts parts)
        {
            string ruleName = "notEmptyRule";

            parts.RuleStatements.Add($$"""
            const {{ruleName}} = {{GetNotEmptyCheckExpression(property)}};
""");
            parts.RuleNames.Add(ruleName);
            parts.TranslationTags.Add(TranslationTags.NotEmpty);
        }

        private static string GetNotEmptyCheckExpression(SpiderlyProperty property)
        {
            if (property.IsEditorControlType())
            {
                return "typeof value !== 'undefined' && value !== null && value !== '' && value !== '<p></p>'";
            }
            else
            {
                return "typeof value !== 'undefined' && value !== null && value !== ''";
            }
        }

        private static void AddLengthRangeRule(LengthRangePart part, ValidationMethodParts parts)
        {
            string ruleName = "stringLengthRule";
            parts.RuleStatements.Add($$"""
            const min = {{part.Min}};
            const max = {{part.Max}};
            const {{ruleName}} = (value?.length >= min && value?.length <= max) || (typeof value === 'undefined' || value === null || value === '');
""");
            parts.RuleNames.Add(ruleName);
            parts.TranslocoVariables.AddRange(["min", "max"]);
            parts.TranslationTags.Add(TranslationTags.Length);
        }

        private static void AddExactLengthRule(ExactLengthRulePart part, ValidationMethodParts parts)
        {
            string ruleName = "stringSingleLengthRule";
            parts.RuleStatements.Add($$"""
            const length = {{part.Length}};
            const {{ruleName}} = (value?.length == length) || (typeof value === 'undefined' || value === null || value === '');
""");
            parts.RuleNames.Add(ruleName);
            parts.TranslocoVariables.AddRange(["length"]);
            parts.TranslationTags.Add(TranslationTags.SingleLength);
        }

        private static void AddMaximumLengthRule(MaximumLengthRulePart part, ValidationMethodParts parts)
        {
            string ruleName = "stringMaxLengthRule";
            parts.RuleStatements.Add($$"""
            const max = {{part.MaxLength}};
            const {{ruleName}} = (value?.length <= max) || (typeof value === 'undefined' || value === null || value === '');
""");
            parts.RuleNames.Add(ruleName);
            parts.TranslocoVariables.AddRange(["max"]);
            parts.TranslationTags.Add(TranslationTags.MaxLength);
        }

        private static void AddLessThanOrEqualToRule(LessThanOrEqualToRulePart part, ValidationMethodParts parts)
        {
            string ruleName = "numberMaxRangeRule";
            parts.RuleStatements.Add($$"""
            const max = {{part.Value}};
            const {{ruleName}} = (value <= max) || (typeof value === 'undefined' || value === null || value === '');
""");
            parts.RuleNames.Add(ruleName);
            parts.TranslocoVariables.AddRange(["max"]);
            parts.TranslationTags.Add(TranslationTags.NumberRangeMax);
        }

        private static void AddGreaterThanOrEqualToRule(GreaterThanOrEqualToRulePart part, ValidationMethodParts parts)
        {
            string ruleName = "numberMinRangeRule";
            parts.RuleStatements.Add($$"""
            const min = {{part.Value}};
            const {{ruleName}} = (value >= min) || (typeof value === 'undefined' || value === null || value === '');
""");
            parts.RuleNames.Add(ruleName);
            parts.TranslocoVariables.AddRange(["min"]);
            parts.TranslationTags.Add(TranslationTags.NumberRangeMin);
        }

        private static void AddNotHaveWhiteSpaceRule(ValidationMethodParts parts)
        {
            string ruleName = "notHaveWhiteSpaceRule";
            parts.RuleStatements.Add($$"""
            const {{ruleName}} = !/\\s/.test(value);
""");
            parts.RuleNames.Add(ruleName);
            parts.TranslationTags.Add(TranslationTags.NotHaveWhiteSpace);
        }

        private static void AddEmailAddressRule(ValidationMethodParts parts)
        {
            string ruleName = "emailAddressRule";
            parts.RuleStatements.Add($$"""
            const {{ruleName}} = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value);
""");
            parts.RuleNames.Add(ruleName);
            parts.TranslationTags.Add(TranslationTags.EmailAddress);
        }

        private static void AddPrecisionScaleRule(PrecisionScaleRulePart part, ValidationMethodParts parts)
        {
            string ruleName = "precisionScaleRule";

            parts.RuleStatements.Add($$"""
            const precision = {{part.Precision}};
            const scale = {{part.Scale}};
            const ignoreTrailingZeros = false;
            const {{ruleName}} = validatePrecisionScale(value, precision, scale, ignoreTrailingZeros) || (typeof value === 'undefined' || value === null || value === '');
""");
            parts.RuleNames.Add(ruleName);
            parts.TranslocoVariables.AddRange(["precision", "scale"]);
            parts.TranslationTags.Add(TranslationTags.PrecisionScale);
        }

        private static class TranslationTags
        {
            public const string NotEmpty = nameof(NotEmpty);
            public const string Length = nameof(Length);
            public const string MaxLength = nameof(MaxLength);
            public const string SingleLength = nameof(SingleLength);
            public const string NumberRangeMin = nameof(NumberRangeMin);
            public const string NumberRangeMax = nameof(NumberRangeMax);
            public const string PrecisionScale = nameof(PrecisionScale);
            public const string EmailAddress = nameof(EmailAddress);
            public const string NotHaveWhiteSpace = nameof(NotHaveWhiteSpace);
        }

        /// <summary>
        /// Translation tags are added in the order rule parts are processed (which mirrors
        /// attribute-declaration order on the DTO/entity). That order is not stable — `[Required, Email]`
        /// and `[Email, Required]` would produce different keys (`NotEmptyEmailAddress` vs `EmailAddressNotEmpty`),
        /// only one of which the i18n template ships. Sort tags into a fixed canonical order so the
        /// translation key is deterministic and the init template only needs one entry per combo.
        /// </summary>
        private static readonly Dictionary<string, int> TagCanonicalOrder = new()
        {
            [TranslationTags.NotEmpty] = 0,
            [TranslationTags.Length] = 1,
            [TranslationTags.MaxLength] = 2,
            [TranslationTags.SingleLength] = 3,
            [TranslationTags.NumberRangeMin] = 4,
            [TranslationTags.NumberRangeMax] = 5,
            [TranslationTags.PrecisionScale] = 6,
            [TranslationTags.EmailAddress] = 7,
            [TranslationTags.NotHaveWhiteSpace] = 8,
        };

        private static string BuildTranslationKey(List<string> tags)
        {
            return string.Concat(tags.OrderBy(t => TagCanonicalOrder.TryGetValue(t, out int rank)
                ? rank
                : throw new NotSupportedException($"Translation tag '{t}' is not registered in {nameof(TagCanonicalOrder)} — add it so the emitted key is deterministic.")));
        }

        private static string? GenerateNotEmptyMarkers(List<string> ruleNames)
        {
            if (ruleNames.Any(x => x == "notEmptyRule"))
            {
                return $$"""
        validator.hasNotEmptyRule = true;
        control.required = true;
""";
            }

            return null;
        }

        private static string? GenerateDateValidityUpdate(SpiderlyProperty property)
        {
            if (property.Type.Raw == "DateTime" || property.Type.Raw == "DateTime?")
            {
                return $$"""
        control.updateValueAndValidity(); // It's necessary only for Date Angular type
""";
            }

            return null;
        }

        private class ValidationMethodParts
        {
            public List<string> RuleStatements { get; } = new();
            public List<string> TranslocoVariables { get; } = new();
            public List<string> RuleNames { get; } = new();
            public List<string> TranslationTags { get; } = new();
        }
    }
}
