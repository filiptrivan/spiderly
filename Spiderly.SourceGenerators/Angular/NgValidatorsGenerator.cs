using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using Spiderly.SourceGenerators.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Net;
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
                new List<NamespaceExtensionCodes> { NamespaceExtensionCodes.Entities, NamespaceExtensionCodes.DTO },
                new List<NamespaceExtensionCodes> { NamespaceExtensionCodes.Entities, NamespaceExtensionCodes.DTO });

            context.RegisterImplementationSourceOutput(combined, static (spc, source) =>
            {
                var (classesAndEntitiesAndPath, config) = source;
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

            List<SpiderlyClass> entities = referencedProjectClasses.Where(x => x.Namespace.EndsWith(".Entities")).ToList();
            List<SpiderlyClass> dtoClasses = referencedProjectClasses.Where(x => x.Namespace.EndsWith(".DTO")).ToList();

            List<string> formControlSwitchCases = new();
            List<string> validatorMethods = new();
            List<string> formArraySwitchCases = new();

            foreach (SpiderlyClass dtoClass in dtoClasses)
            {
                SpiderlyClass entityClass = entities.SingleOrDefault(x =>
                    dtoClass.Name.Replace("DTO", "") == x.Name ||
                    dtoClass.Name.Replace("SaveBodyDTO", "") == x.Name
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
            string entityName = dtoClass.Name.Replace("DTO", "");

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
            string entityName = dtoClass.Name.Replace("DTO", "");

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
            string entityName = dtoClass.Name.Replace("DTO", "");

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

            return valid ? null : { _ : this.translocoService.translate('{{string.Join("", parts.TranslationTags)}}', {{{string.Join(", ", parts.TranslocoVariables)}}}) };
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
            AddNotEmptyRule(rule, parts);
            AddLengthRule(rule, parts);
            AddLessThanOrEqualToRule(rule, parts);
            AddGreaterThanOrEqualToRule(rule, parts);
            AddNotHaveWhiteSpaceRule(rule, parts);
            AddEmailAddressRule(rule, parts);
            AddPrecisionScaleRule(rule, parts);
        }

        private static void AddNotEmptyRule(SpiderValidationRule rule, ValidationMethodParts parts)
        {
            if (rule.ValidationRuleParts.Any(x => x.Name == "NotEmpty") == false)
                return;

            string ruleName = "notEmptyRule";

            parts.RuleStatements.Add($$"""
            const {{ruleName}} = {{GetNotEmptyCheckExpression(rule.Property)}};
""");
            parts.RuleNames.Add(ruleName);
            parts.TranslationTags.Add("NotEmpty");
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

        private static void AddLengthRule(SpiderValidationRule rule, ValidationMethodParts parts)
        {
            SpiderValidationRulePart rulePart = rule.ValidationRuleParts.SingleOrDefault(x => x.Name == "Length");
            if (rulePart == null)
                return;

            Match lengthMatch = Regex.Match(rulePart.MethodParametersBody, @"(\d+),\s*(\d+)");
            Match singleLengthMatch = Regex.Match(rulePart.MethodParametersBody, @"(\d+)");

            if (lengthMatch.Success)
            {
                string ruleName = "stringLengthRule";
                string min = lengthMatch.Groups[1].Value;
                string max = lengthMatch.Groups[2].Value;
                parts.RuleStatements.Add($$"""
            const min = {{min}};
            const max = {{max}};
            const {{ruleName}} = (value?.length >= min && value?.length <= max) || (typeof value === 'undefined' || value === null || value === '');
""");
                parts.RuleNames.Add(ruleName);
                parts.TranslocoVariables.AddRange(["min", "max"]);
                parts.TranslationTags.Add("Length");
            }
            else if (singleLengthMatch.Success)
            {
                string ruleName = "stringSingleLengthRule";
                string length = singleLengthMatch.Groups[1].Value;
                parts.RuleStatements.Add($$"""
            const length = {{length}};
            const {{ruleName}} = (value?.length == length) || (typeof value === 'undefined' || value === null || value === '');
""");
                parts.RuleNames.Add(ruleName);
                parts.TranslocoVariables.AddRange(["length"]);
                parts.TranslationTags.Add("SingleLength");
            }
        }

        private static void AddLessThanOrEqualToRule(SpiderValidationRule rule, ValidationMethodParts parts)
        {
            SpiderValidationRulePart rulePart = rule.ValidationRuleParts.SingleOrDefault(x => x.Name == "LessThanOrEqualTo");
            if (rulePart == null)
                return;

            string ruleName = "numberMaxRangeRule";
            string max = rulePart.MethodParametersBody;
            parts.RuleStatements.Add($$"""
            const max = {{max}};
            const {{ruleName}} = (value <= max) || (typeof value === 'undefined' || value === null || value === '');
""");
            parts.RuleNames.Add(ruleName);
            parts.TranslocoVariables.AddRange(["max"]);
            parts.TranslationTags.Add("NumberRangeMax");
        }

        private static void AddGreaterThanOrEqualToRule(SpiderValidationRule rule, ValidationMethodParts parts)
        {
            SpiderValidationRulePart rulePart = rule.ValidationRuleParts.SingleOrDefault(x => x.Name == "GreaterThanOrEqualTo");
            if (rulePart == null)
                return;

            string ruleName = "numberMinRangeRule";
            string min = rulePart.MethodParametersBody;
            parts.RuleStatements.Add($$"""
            const min = {{min}};
            const {{ruleName}} = (value >= min) || (typeof value === 'undefined' || value === null || value === '');
""");
            parts.RuleNames.Add(ruleName);
            parts.TranslocoVariables.AddRange(["min"]);
            parts.TranslationTags.Add("NumberRangeMin");
        }

        private static void AddNotHaveWhiteSpaceRule(SpiderValidationRule rule, ValidationMethodParts parts)
        {
            if (rule.ValidationRuleParts.Any(x => x.Name == "NotHaveWhiteSpace") == false)
                return;

            string ruleName = "notHaveWhiteSpaceRule";
            parts.RuleStatements.Add($$"""
            const {{ruleName}} = !/\\s/.test(value);
""");
            parts.RuleNames.Add(ruleName);
            parts.TranslationTags.Add("NotHaveWhiteSpace");
        }

        private static void AddEmailAddressRule(SpiderValidationRule rule, ValidationMethodParts parts)
        {
            if (rule.ValidationRuleParts.Any(x => x.Name == "EmailAddress") == false)
                return;

            string ruleName = "emailAddressRule";
            parts.RuleStatements.Add($$"""
            const {{ruleName}} = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value);
""");
            parts.RuleNames.Add(ruleName);
            parts.TranslationTags.Add("EmailAddress");
        }

        private static void AddPrecisionScaleRule(SpiderValidationRule rule, ValidationMethodParts parts)
        {
            SpiderValidationRulePart rulePart = rule.ValidationRuleParts.SingleOrDefault(x => x.Name == "PrecisionScale");
            if (rulePart == null)
                return;

            Match precisionScaleMatch = Regex.Match(rulePart.MethodParametersBody, @"(\d+),\s*(\d+),\s*(true|false)");
            if (precisionScaleMatch.Success == false)
                return;

            string ruleName = "precisionScaleRule";
            string precision = precisionScaleMatch.Groups[1].Value;
            string scale = precisionScaleMatch.Groups[2].Value;
            string ignoreTrailingZeros = precisionScaleMatch.Groups[3].Value;

            parts.RuleStatements.Add($$"""
            const precision = {{precision}};
            const scale = {{scale}};
            const ignoreTrailingZeros = {{ignoreTrailingZeros}};
            const {{ruleName}} = validatePrecisionScale(value, precision, scale, ignoreTrailingZeros) || (typeof value === 'undefined' || value === null || value === '');
""");
            parts.RuleNames.Add(ruleName);
            parts.TranslocoVariables.AddRange(["precision", "scale"]);
            parts.TranslationTags.Add("PrecisionScale");
        }

        private static string GenerateNotEmptyMarkers(List<string> ruleNames)
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

        private static string GenerateDateValidityUpdate(SpiderlyProperty property)
        {
            if (property.Type == "DateTime" || property.Type == "DateTime?")
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
