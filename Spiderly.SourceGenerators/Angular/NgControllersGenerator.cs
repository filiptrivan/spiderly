using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;

namespace Spiderly.SourceGenerators.Angular
{
    /// <summary>
    /// This generator produces an Angular `ApiService` (`{your-app-name}\Frontend\src\app\business\services\api\api.service.generated.ts`)
    /// containing strongly-typed methods for interacting with your .NET Web API controllers.
    /// It analyzes C# controller classes (within the '.Controllers' namespace) and referenced entity and DTO classes
    /// to create corresponding Angular `HttpClient` calls.
    /// </summary>
    [Generator]
    public class NgControllersGenerator : IIncrementalGenerator
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
                new List<ClassCategoryCodes> { ClassCategoryCodes.Controllers },
                new List<ClassCategoryCodes> { ClassCategoryCodes.Entities, ClassCategoryCodes.DTO });

            var combinedWithEnums = combined.Combine(PipelineFactory.GetSpiderlyEnumNamesProvider(context.SyntaxProvider));

            context.RegisterSafeImplementationSourceOutput(combinedWithEnums, static (spc, source) =>
            {
                var (combinedSource, enumNames) = source;
                var (classesAndEntitiesAndPath, config) = combinedSource;
                var (classesAndEntities, callingPath) = classesAndEntitiesAndPath;
                var (classes, referencedClasses) = classesAndEntities;

                Execute(classes, referencedClasses, enumNames, callingPath, config, spc);
            });
        }

        private static void Execute(IList<ClassDeclarationSyntax> classes, List<SpiderlyClass> referencedProjectClasses, ImmutableArray<string> spiderlyEnumNames, string callingProjectDirectory, SpiderlyConfig config, SourceProductionContext context)
        {
            if (classes.Count == 0)
                return;

            if (!config.IsGeneratorEnabled(nameof(NgControllersGenerator)))
                return;

            if (callingProjectDirectory.Contains(".WebAPI") == false)
                return;

            // ...\Backend\PlayertyLoyals.Business -> ...\Frontend\src\app\business\services\api\api.service.generated.ts
            string rootPath = callingProjectDirectory.GetRootPath();
            string outputPath = Path.Combine(rootPath, "Frontend", "src", "app", "business", "services", "api", "api.service.generated.ts");

            List<SpiderlyClass> currentProjectClasses = SpiderlyClassFactory.GetSpiderlyClasses(classes, referencedProjectClasses, spiderlyEnumNames);

            List<SpiderlyClass> controllerClasses = currentProjectClasses
                .Where(x => x.HasSpiderlyControllerAttribute())
                .ToList();

            List<SpiderlyClass> referencedDTOs = referencedProjectClasses
                .Where(x => x.HasSpiderlyDTOAttribute())
                .ToList();

            List<SpiderlyClass> allEntities = referencedProjectClasses
                .Where(x => x.HasSpiderlyEntityAttribute())
                .ToList();

            HashSet<string> knownTsTypes = new(
                referencedDTOs.Select(d => d.Name.Replace("DTO", ""))
                    .Concat(allEntities.Select(e => e.Name))
                    .Concat(Helpers.BaseClassNames));

            string result = $$"""
{{string.Join("\n", GetImports(referencedDTOs))}}

@Injectable({
    providedIn: 'root'
})
export class ApiGeneratedService extends ApiSecurityService {

    constructor(
        protected override http: HttpClient,
        protected override config: ConfigService
    ) {
        super(http, config);
    }

{{string.Join("\n\n", GetAngularHttpMethods(controllerClasses, allEntities, referencedDTOs, knownTsTypes, spiderlyEnumNames, context))}}

}
""";

            Helpers.WriteToTheFile(result, outputPath);
        }

        private static List<string> GetAngularHttpMethods(List<SpiderlyClass> controllerClasses, List<SpiderlyClass> allEntities, List<SpiderlyClass> referencedDTOs, HashSet<string> knownTsTypes, ImmutableArray<string> spiderlyEnumNames, SourceProductionContext context)
        {
            List<string> result = new();
            // Methods already defined in ApiSecurityService (Angular). Keep this list in sync
            // with ApiSecurityService whenever methods are added to or removed from SecurityBaseController.
            HashSet<string> alreadyAddedMethods = new()
            {
                "SendLoginVerificationEmail",
                "Login",
                "LoginWithCookies",
                "Logout",
                "LogoutWithCookies",
                "RefreshTokenWithHeaders",
                "RefreshTokenWithCookies",
                "GetCurrentUserBase",
                "GetCurrentUserPermissionCodes",
            };

            foreach (SpiderlyClass controllerClass in controllerClasses)
            {
                if (controllerClass.HasUIDoNotGenerateAttribute())
                    continue;

                string controllerName = controllerClass.Name.Replace("Controller", "");

                foreach (SpiderlyMethod controllerMethod in controllerClass.Methods)
                {
                    if (!IsEndpointMethod(controllerMethod))
                        continue;

                    if (!alreadyAddedMethods.Add(controllerMethod.Name))
                        continue;

                    ValidateControllerType(context, "return", controllerMethod.ReturnType, controllerClass.Name, controllerMethod.Name, knownTsTypes, spiderlyEnumNames, controllerMethod.Location);

                    foreach (SpiderParameter parameter in controllerMethod.Parameters)
                        ValidateControllerType(context, $"parameter '{parameter.Name}'", parameter.Type, controllerClass.Name, controllerMethod.Name, knownTsTypes, spiderlyEnumNames, controllerMethod.Location);

                    if (controllerMethod.Parameters.Any(x => x.HasFromFormAttribute()) && controllerMethod.Parameters.Any(x => x.Type.Raw == "IFormFile") == false)
                    {
                        result.Add(GetCustomFromFormControllerMethod(controllerMethod, controllerName, referencedDTOs, spiderlyEnumNames));
                    }
                    else
                    {
                        result.Add(GetCustomAngularControllerMethod(controllerMethod, controllerName, spiderlyEnumNames));
                    }
                }
            }

            foreach (SpiderlyClass entity in allEntities.Where(x => x.HasUIDoNotGenerateAttribute() == false))
            {
                result.Add(GetBaseAngularControllerMethods(entity, allEntities, alreadyAddedMethods));
            }

            return result;
        }

        /// <summary>
        /// Reports SPIDERLY001 if <paramref name="type"/> is a custom class that won't resolve to a known
        /// TypeScript type in the generated Angular client (i.e. not a primitive, enum, discovered DTO, or discovered entity).
        /// </summary>
        private static void ValidateControllerType(
            SourceProductionContext context,
            string kind,
            SpiderlyTypeRef type,
            string controllerName,
            string methodName,
            HashSet<string> knownTsTypes,
            ImmutableArray<string> spiderlyEnumNames,
            Location location)
        {
            if (type.Raw.IsBaseDataType()
                || type.Raw == "Task"
                || type.Raw == "void"
                || type.Raw.Contains("ActionResult")
                || type.Raw.Contains("IFormFile")
                || type.Raw.IsEnum(spiderlyEnumNames))
                return;

            string target = AngularTypeMapper.GetValidationTargetSymbol(type, spiderlyEnumNames);

            if (string.IsNullOrEmpty(target))
                return;

            if (AngularTypeMapper.IsKnownTsScalar(target) || target.IsEnum(spiderlyEnumNames) || knownTsTypes.Contains(target))
                return;

            context.ReportDiagnostic(Diagnostic.Create(
                SpiderlyDiagnostics.UnresolvableControllerType,
                location ?? Location.None,
                kind,
                type.Raw,
                controllerName,
                methodName));
        }

        private static List<string> GetImports(List<SpiderlyClass> DTOs)
        {
            List<string> result = new();

            result.Add($$"""
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiSecurityService, Filter, PaginatedResult, Namebook, Codebook, LazyLoadSelectedIdsResult, VerificationTokenRequest, AuthResult, AuthResultWithCookies, ExternalProvider, Login, SendLoginVerificationEmailResult, EditorImageUploadResult } from 'spiderly';
import { ConfigService } from '../config.service';
""");

            foreach (SpiderlyClass DTO in DTOs)
            {
                string[] projectNameHelper = DTO.Namespace.Split('.');
                string projectName = projectNameHelper[projectNameHelper.Length - 2];

                if (projectName == "Security")
                    continue;

                string ngType = DTO.Name.Replace("DTO", "");

                if (Helpers.BaseClassNames.Contains(ngType))
                    continue;

                result.Add($$"""
import { {{ngType}} } from '../../entities/entities.generated';
""");
            }

            return result;
        }

        #region Custom Angular Controller Method

        private static string GetCustomAngularControllerMethod(SpiderlyMethod controllerMethod, string controllerName, ImmutableArray<string> spiderlyEnumNames)
        {
            string angularReturnType = AngularTypeMapper.GetAngularType(controllerMethod.ReturnType, spiderlyEnumNames);

            HttpTypeCodes httpType = GetHttpType(controllerMethod);

            Dictionary<string, string> inputParameters = controllerMethod.Parameters
                .ToDictionary(
                    x => x.Name,
                    x => AngularTypeMapper.GetAngularType(x.Type, spiderlyEnumNames)
                );

            string httpOptions = GetHttpOptions(controllerMethod, spiderlyEnumNames);

            return GetAngularControllerMethod(controllerMethod.Name, inputParameters, angularReturnType, httpType, controllerName, httpOptions);
        }

        private static string GetHttpOptions(SpiderlyMethod controllerMethod, ImmutableArray<string> spiderlyEnumNames)
        {
            if (AngularTypeMapper.GetAngularType(controllerMethod.ReturnType, spiderlyEnumNames) == "string")
                return Settings.HttpOptionsText;

            if (controllerMethod.ReturnType.Contains("IActionResult"))
                return Settings.HttpOptionsBlob;

            if (ShouldSkipSpinner(controllerMethod, spiderlyEnumNames))
                return Settings.HttpOptionsSkipSpinner;

            return Settings.HttpOptionsBase;
        }

        /// <summary>
        /// Decides whether a generated Angular method opts out of the global full-screen blocking spinner.
        /// The spinner exists for primary, user-initiated blocking operations (form load, save, delete);
        /// it must never black out the screen for frequent or background fetches.
        /// </summary>
        internal static bool ShouldSkipSpinner(SpiderlyMethod controllerMethod, ImmutableArray<string> spiderlyEnumNames)
        {
            // Explicit attributes always win over inference. [ShowSpinner] is the opt-back-in for the rare
            // case the auto-skip below gets wrong (a deliberately slow scalar GET); checked first so it also
            // overrides the read-shaped-DTO skip if a consumer insists.
            if (controllerMethod.Attributes.Any(attr => attr.Name == "ShowSpinner"))
                return false;

            // Explicit opt-out.
            if (controllerMethod.Attributes.Any(attr => attr.Name == "SkipSpinner"))
                return true;

            // Read-shaped DTOs fetched frequently (autocomplete, dropdown, table pagination, M2M).
            if (controllerMethod.ReturnType.Contains("NamebookDTO") ||
                controllerMethod.ReturnType.Contains("CodebookDTO") ||
                controllerMethod.ReturnType.Contains("PaginatedResultDTO") ||
                controllerMethod.ReturnType.Contains("LazyLoadSelectedIdsResultDTO"))
            {
                return true;
            }

            // A GET returning a bare scalar (count, flag, status, timestamp) is almost always a lightweight
            // or polled read; blacking out the whole screen for a single value is never what the caller wants.
            // Primary blocking loads return an entity/DTO, never a bare scalar, so the false-positive risk is
            // low. This is the case authors (and coding agents) most often forget to mark with [SkipSpinner],
            // so we infer it instead of relying on the attribute. ("string" is handled earlier as a text
            // response. A user-triggered operation that does real work is usually a POST, which keeps the
            // spinner; for the rare slow scalar GET that genuinely wants it, mark it [ShowSpinner].)
            if (controllerMethod.Attributes.Any(attr => attr.Name == "HttpGet") &&
                ReturnsBareScalar(controllerMethod.ReturnType, spiderlyEnumNames))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// True when the return type resolves to a <b>single</b> numeric, boolean, or date value.
        /// A collection at any nesting level is not a bare scalar and must be excluded explicitly: for a
        /// wrapped collection like <c>Task&lt;List&lt;int&gt;&gt;</c> or <c>ActionResult&lt;List&lt;int&gt;&gt;</c>
        /// the outer wrapper node isn't itself a collection, so <see cref="AngularTypeMapper.GetAngularType"/>
        /// unwraps straight to the inner scalar ("number") instead of "number[]". We therefore walk the whole
        /// generic chain and bail on any collection node. DTOs resolve to their type name, so they never match.
        /// </summary>
        private static bool ReturnsBareScalar(string returnType, ImmutableArray<string> spiderlyEnumNames)
        {
            SpiderlyTypeRef parsed = SpiderlyTypeRef.Parse(returnType);

            if (parsed == null)
                return false;

            // A collection anywhere in the chain (Task<List<int>>, ActionResult<int[]>, ValueTask<List<int>>, …)
            // is not a bare scalar — the outer wrapper hides it from GetAngularType's own collection handling.
            for (SpiderlyTypeRef node = parsed; node != null; node = node.ElementType)
                if (node.IsCollection)
                    return false;

            string angularType = AngularTypeMapper.GetAngularType(returnType, spiderlyEnumNames);

            return angularType == "number" || angularType == "boolean" || angularType == "Date";
        }

        /// <summary>
        /// A controller method is an endpoint the Angular client should call unless it opted out:
        /// [NonAction] methods are excluded from ASP.NET routing (e.g. a consumer suppressing a
        /// generated base action by overriding it) and carry no Http* attribute — without this
        /// skip, GetHttpType throws and the WHOLE api.service.generated.ts silently stops
        /// regenerating.
        /// </summary>
        internal static bool IsEndpointMethod(SpiderlyMethod controllerMethod) =>
            !controllerMethod.HasUIDoNotGenerateAttribute()
            && !controllerMethod.Attributes.Any(attr => attr.Name == "NonAction");

        internal static HttpTypeCodes GetHttpType(SpiderlyMethod controllerMethod)
        {
            if (controllerMethod.Attributes.Any(attr => attr.Name == "HttpGet"))
            {
                return HttpTypeCodes.Get;
            }
            else if (controllerMethod.Attributes.Any(attr => attr.Name == "HttpPost"))
            {
                return HttpTypeCodes.Post;
            }
            else if (controllerMethod.Attributes.Any(attr => attr.Name == "HttpPut"))
            {
                return HttpTypeCodes.Put;
            }
            else if (controllerMethod.Attributes.Any(attr => attr.Name == "HttpDelete"))
            {
                return HttpTypeCodes.Delete;
            }
            else
            {
                throw new NotImplementedException(
                    $"Controller action '{controllerMethod.Name}' has no HttpGet/HttpPost/HttpPut/HttpDelete attribute. " +
                    "Add one, or mark the method [NonAction] if it is not an endpoint.");
            }
        }

        private static string GetBaseAngularControllerMethods(SpiderlyClass entity, List<SpiderlyClass> allEntities, HashSet<string> alreadyAddedMethods)
        {
            if (entity.IsManyToMany()) // TODO FT: Do something with M2M entities
                return null;

            return $$"""
{{GetBaseTableDataAngularControllerMethod(entity, alreadyAddedMethods)}}

{{GetBaseExportListToExcelAngularControllerMethod(entity, alreadyAddedMethods)}}

{{GetBaseGetListAngularControllerMethod(entity, alreadyAddedMethods)}}

{{GetBaseGetMainUIFormAngularControllerMethod(entity, alreadyAddedMethods)}}

{{GetBaseGetAngularControllerMethod(entity, alreadyAddedMethods)}}

{{GetBaseGetListForAutocompleteAngularControllerMethods(entity, alreadyAddedMethods)}}

{{GetBaseGetListForDropdownAngularControllerMethods(entity, alreadyAddedMethods)}}

{{string.Join("\n\n", GetBaseOrderedOneToManyAngularControllerMethods(entity, alreadyAddedMethods))}}

{{string.Join("\n\n", GetBaseManyToManyAngularControllerMethods(entity, allEntities, alreadyAddedMethods))}}

{{string.Join("\n\n", GetBaseComplexManyToManyListAngularControllerMethods(entity, allEntities, alreadyAddedMethods))}}

{{GetBaseSaveAngularControllerMethod(entity, alreadyAddedMethods)}}

{{string.Join("\n\n", GetBaseUploadBlobAngularControllerMethods(entity, alreadyAddedMethods))}}

{{string.Join("\n\n", GetBaseUploadEditorImageAngularControllerMethods(entity, alreadyAddedMethods))}}

{{GetBaseDeleteAngularControllerMethods(entity, alreadyAddedMethods)}}

{{GetBaseDeleteListAngularControllerMethods(entity, alreadyAddedMethods)}}

""";
        }

        private static string GetCustomFromFormControllerMethod(SpiderlyMethod controllerMethod, string controllerName, List<SpiderlyClass> DTOList, ImmutableArray<string> spiderlyEnumNames)
        {
            SpiderParameter parameter = controllerMethod.Parameters.Single();
            SpiderlyClass parameterType = DTOList.Where(x => x.Name == parameter.Type.Raw).SingleOrDefault();
            string angularReturnType = AngularTypeMapper.GetAngularType(controllerMethod.ReturnType, spiderlyEnumNames);

            return $$"""
    {{controllerMethod.Name.FirstCharToLower()}} = (dto: {{parameter.Type.Raw.Replace("DTO", "")}}): Observable<{{angularReturnType}}> => { 
        let formData = new FormData();
{{string.Join("\n", GetFormDataAppends(parameterType))}}
        return this.http.post(`${this.config.apiUrl}/{{controllerName}}/{{controllerMethod.Name}}`, formData, this.config.httpOptions);
    }
""";
        }

        private static List<string> GetFormDataAppends(SpiderlyClass dto)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in dto.Properties)
            {
                if (property.Type.Raw == "List<IFormFile>")
                {
                    result.Add($$"""
        dto.{{property.Name.FirstCharToLower()}}.forEach((file: File) => {
            formData.append('{{property.Name}}', file);
        });
""");
                }
                else if (property.Type.Raw == "IFormFile")
                {
                    result.Add($$"""
        formData.append('{{property.Name}}', dto.{{property.Name.FirstCharToLower()}});
""");
                }
                else
                {
                    result.Add($$"""
        formData.append('{{property.Name}}', dto.{{property.Name.FirstCharToLower()}}.toString());
""");
                }

            }

            return result;
        }

        #endregion

        #region Generated Angular Controller Methods

        #region Ordered One To Many

        private static List<string> GetBaseOrderedOneToManyAngularControllerMethods(SpiderlyClass entity, HashSet<string> alreadyAddedMethods)
        {
            List<string> result = new();

            List<SpiderlyProperty> uiOrderedOneToManyProperties = Helpers.GetUIOrderedOneToManyProperties(entity);

            foreach (SpiderlyProperty property in uiOrderedOneToManyProperties)
            {
                result.Add(GetBaseOrderedOneToManyAngularControllerMethod(property, entity, alreadyAddedMethods));
            }

            return result;
        }

        private static string GetBaseOrderedOneToManyAngularControllerMethod(SpiderlyProperty uiOrderedOneToManyProperty, SpiderlyClass entity, HashSet<string> alreadyAddedMethods)
        {
            string methodName = $"GetOrdered{uiOrderedOneToManyProperty.Name}For{entity.Name}";

            if (alreadyAddedMethods.Contains(methodName))
                return null;

            Dictionary<string, string> getAndDeleteParameter = new Dictionary<string, string> { { "id", "number" } };

            return GetAngularControllerMethod(
                methodName, getAndDeleteParameter, $"{Helpers.ExtractTypeFromGenericType(uiOrderedOneToManyProperty.Type)}[]", HttpTypeCodes.Get, entity.ControllerName, Settings.HttpOptionsBase
            );
        }

        #endregion

        #region Many To Many

        private static List<string> GetBaseManyToManyAngularControllerMethods(SpiderlyClass entity, List<SpiderlyClass> allEntities, HashSet<string> alreadyAddedMethods)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.Properties)
            {
                if (property.IsMultiSelectControlType() ||
                    property.IsMultiAutocompleteControlType())
                {
                    result.Add(GetBaseManyToManyMultiControlTypesAngularControllerMethod(property, entity, alreadyAddedMethods));
                }
                else if (property.HasSimpleManyToManyTableLazyLoadAttribute())
                {
                    result.Add(GetBaseManyToManyTableDataAngularControllerMethod(property, entity, allEntities, alreadyAddedMethods));
                    result.Add(GetBaseManyToManyTableDataExportAngularControllerMethod(property, entity, alreadyAddedMethods));
                    result.Add(GetBaseSimpleManyToManyTableLazyLoadAngularControllerMethod(property, entity, alreadyAddedMethods));
                }
                else if (property.HasComplexManyToManyReadonlyTableAttribute())
                {
                    result.Add(GetBaseManyToManyTableDataAngularControllerMethod(property, entity, allEntities, alreadyAddedMethods));
                    result.Add(GetBaseManyToManyTableDataExportAngularControllerMethod(property, entity, alreadyAddedMethods));
                }
            }

            return result;
        }

        #region Simple Many To Many Table Lazy Load

        private static string GetBaseSimpleManyToManyTableLazyLoadAngularControllerMethod(SpiderlyProperty property, SpiderlyClass entity, HashSet<string> alreadyAddedMethods)
        {
            string methodName = $"LazyLoadSelected{property.Name}IdsFor{entity.Name}";

            if (alreadyAddedMethods.Contains(methodName))
                return null;

            Dictionary<string, string> postAndPutParameter = new Dictionary<string, string> { { "filterDTO", "Filter" } };

            return GetAngularControllerMethod(methodName, postAndPutParameter, "LazyLoadSelectedIdsResult", HttpTypeCodes.Post, entity.ControllerName, Settings.HttpOptionsSkipSpinner);
        }

        private static string GetBaseManyToManyTableDataAngularControllerMethod(SpiderlyProperty property, SpiderlyClass entity, List<SpiderlyClass> entities, HashSet<string> alreadyAddedMethods)
        {
            string methodName = $"GetPaginated{property.Name}ListFor{entity.Name}";

            if (alreadyAddedMethods.Contains(methodName))
                return null;

            Dictionary<string, string> postAndPutParameter = new Dictionary<string, string> { { "filterDTO", "Filter" } };

            SpiderlyClass extractedEntity = Helpers.GetEntityByPropertyType(property, entities);

            return GetAngularControllerMethod(methodName, postAndPutParameter, $"PaginatedResult<{extractedEntity.Name}>", HttpTypeCodes.Post, entity.ControllerName, Settings.HttpOptionsSkipSpinner);
        }

        private static string GetBaseManyToManyTableDataExportAngularControllerMethod(SpiderlyProperty property, SpiderlyClass entity, HashSet<string> alreadyAddedMethods)
        {
            string methodName = $"Export{property.Name}ListToExcelFor{entity.Name}";

            if (alreadyAddedMethods.Contains(methodName))
                return null;

            Dictionary<string, string> postAndPutParameter = new Dictionary<string, string> { { "filterDTO", "Filter" } };

            return GetAngularControllerMethod(methodName, postAndPutParameter, "any", HttpTypeCodes.Post, entity.ControllerName, Settings.HttpOptionsBlob);
        }

        #endregion

        #region Multi Control Types

        private static string GetBaseManyToManyMultiControlTypesAngularControllerMethod(SpiderlyProperty property, SpiderlyClass entity, HashSet<string> alreadyAddedMethods)
        {
            string methodName = $"Get{property.Name}NamebookListFor{entity.Name}";

            if (alreadyAddedMethods.Contains(methodName))
                return null;

            Dictionary<string, string> getAndDeleteParameter = new Dictionary<string, string> { { "id", "number" } };

            return GetAngularControllerMethod(
                methodName, getAndDeleteParameter, "Namebook[]", HttpTypeCodes.Get, entity.ControllerName, Settings.HttpOptionsSkipSpinner
            );
        }

        #endregion

        #endregion

        #region Complex Many To Many List

        private static List<string> GetBaseComplexManyToManyListAngularControllerMethods(SpiderlyClass entity, List<SpiderlyClass> allEntities, HashSet<string> alreadyAddedMethods)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.GetComplexManyToManyListProperties())
            {
                SpiderlyClass junctionEntity = allEntities.Single(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type));

                string methodName = $"GetDefault{property.Name}For{entity.Name}";

                if (alreadyAddedMethods.Contains(methodName))
                    continue;

                result.Add(GetAngularControllerMethod(
                    methodName, null, $"{junctionEntity.Name}[]", HttpTypeCodes.Get, entity.ControllerName, Settings.HttpOptionsSkipSpinner
                ));
            }

            return result;
        }

        #endregion

        private static string GetBaseDeleteAngularControllerMethods(SpiderlyClass entity, HashSet<string> alreadyAddedMethods)
        {
            if (entity.IsReadonlyObject())
                return null;

            string methodName = $"Delete{entity.Name}";

            if (alreadyAddedMethods.Contains(methodName))
                return null;

            Dictionary<string, string> getAndDeleteParameters = new Dictionary<string, string> { { "id", "number" } };

            return GetAngularControllerMethod(methodName, getAndDeleteParameters, "any", HttpTypeCodes.Delete, entity.ControllerName, Settings.HttpOptionsBase);
        }

        private static string GetBaseDeleteListAngularControllerMethods(SpiderlyClass entity, HashSet<string> alreadyAddedMethods)
        {
            if (entity.IsReadonlyObject())
                return null;

            string methodName = $"Delete{entity.Name}List";

            if (alreadyAddedMethods.Contains(methodName))
                return null;

            Dictionary<string, string> postAndPutParameter = new Dictionary<string, string> { { "ids", "number[]" } };

            return GetAngularControllerMethod(methodName, postAndPutParameter, "any", HttpTypeCodes.Post, entity.ControllerName, Settings.HttpOptionsBase);
        }

        private static List<string> GetBaseUploadBlobAngularControllerMethods(SpiderlyClass entity, HashSet<string> alreadyAddedMethods)
        {
            List<string> result = new();

            List<SpiderlyProperty> blobProperies = Helpers.GetBlobProperties(entity.Properties);

            foreach (SpiderlyProperty property in blobProperies)
            {
                result.Add(GetBaseUploadBlobAngularControllerMethod(property, entity, alreadyAddedMethods));
            }

            return result;
        }

        private static string GetBaseUploadBlobAngularControllerMethod(SpiderlyProperty blobProperty, SpiderlyClass entity, HashSet<string> alreadyAddedMethods)
        {
            string methodName = $"Upload{blobProperty.Name}For{entity.Name}";

            if (alreadyAddedMethods.Contains(methodName))
                return null;

            Dictionary<string, string> postAndPutParameter = new Dictionary<string, string> { { "file", "FormData" } };

            return GetAngularControllerMethod(methodName, postAndPutParameter, "string", HttpTypeCodes.Post, entity.ControllerName, Settings.HttpOptionsText);
        }

        private static List<string> GetBaseUploadEditorImageAngularControllerMethods(SpiderlyClass entity, HashSet<string> alreadyAddedMethods)
        {
            List<string> result = new();

            List<SpiderlyProperty> editorProperties = Helpers.GetEditorImageProperties(entity.Properties);

            foreach (SpiderlyProperty property in editorProperties)
            {
                string methodName = $"Upload{property.Name}ImageFor{entity.Name}";

                if (alreadyAddedMethods.Contains(methodName))
                    continue;

                Dictionary<string, string> postParameter = new Dictionary<string, string> { { "file", "FormData" } };

                result.Add(GetAngularControllerMethod(methodName, postParameter, "EditorImageUploadResult", HttpTypeCodes.Post, entity.ControllerName, Settings.HttpOptionsBase));
            }

            return result;
        }

        private static string GetBaseSaveAngularControllerMethod(SpiderlyClass entity, HashSet<string> alreadyAddedMethods)
        {
            if (entity.IsReadonlyObject())
                return null;

            string methodName = $"Save{entity.Name}";

            if (alreadyAddedMethods.Contains(methodName))
                return null;

            Dictionary<string, string> postAndPutParameter = new Dictionary<string, string> { { "saveBodyDTO", $"{entity.Name}SaveBody" } };

            return GetAngularControllerMethod(methodName, postAndPutParameter, $"{entity.Name}SaveBody", HttpTypeCodes.Put, entity.ControllerName, Settings.HttpOptionsBase);
        }

        private static string GetBaseGetListForAutocompleteAngularControllerMethods(SpiderlyClass entity, HashSet<string> alreadyAddedMethods)
        {
            StringBuilder sb = new();

            foreach (SpiderlyProperty property in entity.Properties)
            {
                if (property.ShouldGenerateAutocompleteControllerMethod())
                {
                    string methodName = $"Get{property.Name}AutocompleteListFor{entity.Name}";

                    if (alreadyAddedMethods.Contains(methodName))
                        continue;

                    Dictionary<string, string> getAndDeleteParameters = new()
                    {
                        { "limit", "number" },
                        { "filter", "string" },
                    };

                    sb.AppendLine(GetAngularControllerMethod(methodName, getAndDeleteParameters, "Namebook[]", HttpTypeCodes.Get, entity.ControllerName, Settings.HttpOptionsSkipSpinner));
                }
            }

            return sb.ToString();
        }

        private static string GetBaseGetListForDropdownAngularControllerMethods(SpiderlyClass entity, HashSet<string> alreadyAddedMethods)
        {
            StringBuilder sb = new();

            foreach (SpiderlyProperty property in entity.Properties)
            {
                if (property.ShouldGenerateDropdownControllerMethod())
                {
                    string methodName = $"Get{property.Name}DropdownListFor{entity.Name}";

                    if (alreadyAddedMethods.Contains(methodName))
                        continue;

                    Dictionary<string, string> getAndDeleteParameters = new();

                    sb.AppendLine(GetAngularControllerMethod(methodName, getAndDeleteParameters, "Namebook[]", HttpTypeCodes.Get, entity.ControllerName, Settings.HttpOptionsSkipSpinner));
                }
            }

            return sb.ToString();
        }

        private static string GetBaseGetMainUIFormAngularControllerMethod(SpiderlyClass entity, HashSet<string> alreadyAddedMethods)
        {
            string methodName = $"Get{entity.Name}MainUIFormDTO";

            if (alreadyAddedMethods.Contains(methodName))
                return null;

            Dictionary<string, string> getAndDeleteParameters = new() { { "id", "number" } };

            return GetAngularControllerMethod(methodName, getAndDeleteParameters, returnType: $"{entity.Name}MainUIForm", HttpTypeCodes.Get, entity.ControllerName, Settings.HttpOptionsBase);
        }

        private static string GetBaseGetAngularControllerMethod(SpiderlyClass entity, HashSet<string> alreadyAddedMethods)
        {
            string methodName = $"Get{entity.Name}";

            if (alreadyAddedMethods.Contains(methodName))
                return null;

            Dictionary<string, string> getAndDeleteParameters = new() { { "id", "number" } };

            return GetAngularControllerMethod(methodName, getAndDeleteParameters, $"{entity.Name}", HttpTypeCodes.Get, entity.ControllerName, Settings.HttpOptionsBase);
        }

        private static string GetBaseGetListAngularControllerMethod(SpiderlyClass entity, HashSet<string> alreadyAddedMethods)
        {
            string methodName = $"Get{entity.Name}List";

            if (alreadyAddedMethods.Contains(methodName))
                return null;

            return GetAngularControllerMethod(methodName, null, $"{entity.Name}[]", HttpTypeCodes.Get, entity.ControllerName, Settings.HttpOptionsBase);
        }

        private static string GetBaseExportListToExcelAngularControllerMethod(SpiderlyClass entity, HashSet<string> alreadyAddedMethods)
        {
            string methodName = $"Export{entity.Name}ListToExcel";

            if (alreadyAddedMethods.Contains(methodName))
                return null;

            Dictionary<string, string> postAndPutParameter = new Dictionary<string, string> { { "filterDTO", "Filter" } };

            return GetAngularControllerMethod(methodName, postAndPutParameter, "any", HttpTypeCodes.Post, entity.ControllerName, Settings.HttpOptionsBlob);
        }

        private static string GetBaseTableDataAngularControllerMethod(SpiderlyClass entity, HashSet<string> alreadyAddedMethods)
        {
            string methodName = $"GetPaginated{entity.Name}List";

            if (alreadyAddedMethods.Contains(methodName))
                return null;

            Dictionary<string, string> postAndPutParameter = new Dictionary<string, string> { { "filterDTO", "Filter" } };

            return GetAngularControllerMethod(methodName, postAndPutParameter, $"PaginatedResult<{entity.Name}>", HttpTypeCodes.Post, entity.ControllerName, Settings.HttpOptionsSkipSpinner);
        }

        #endregion

        #region Helpers

        private static string GetAngularControllerMethod(
            string methodName,
            Dictionary<string, string> inputParameters,
            string returnType,
            HttpTypeCodes httpType,
            string controllerName,
            string httpOptions
        )
        {
            return $$"""
    {{methodName.FirstCharToLower()}} = ({{GetInputParameters(inputParameters)}}): Observable<{{returnType}}> => { 
{{GetGetAndDeleteParameters(inputParameters, httpType)}}
        return this.http.{{httpType.ToString().FirstCharToLower()}}{{GetReturnTypeAfterHttpType(returnType)}}(`${this.config.apiUrl}/{{controllerName}}/{{methodName}}${params}`{{GetPostAndPutParameters(inputParameters, httpType)}}{{httpOptions}});
    }
""";
        }

        private static string GetInputParameters(Dictionary<string, string> inputParameters)
        {
            if (inputParameters == null)
                return null;

            return string.Join(", ", inputParameters.Select(x => $"{x.Key}: {x.Value}"));
        }

        private static string GetReturnTypeAfterHttpType(string returnType)
        {
            if (returnType == "string")
                return null;

            if (returnType == "any")
                return null;

            return $"<{returnType}>";
        }

        private static string GetGetAndDeleteParameters(Dictionary<string, string> getAndDeleteParams, HttpTypeCodes httpType)
        {
            if (
                (httpType != HttpTypeCodes.Get && httpType != HttpTypeCodes.Delete) ||
                (getAndDeleteParams == null || getAndDeleteParams.Count == 0)
            )
            {
                return """
        const params = '';
""";
            }

            List<KeyValuePair<string, string>> nonNullableParams = getAndDeleteParams.Where(x => x.Value.EndsWith("null") == false).ToList();
            List<KeyValuePair<string, string>> nullableParams = getAndDeleteParams.Where(x => x.Value.EndsWith("null")).ToList();

            return $$"""
        let params = `?{{string.Join("&", nonNullableParams.Select(x => $"{x.Key}=${{{x.Key}}}"))}}`;

{{string.Join("\n", nullableParams.Select(x => $$"""
        if ({{x.Key}}) {
            params += `&{{x.Key}}=${{{x.Key}}}`;
        }
"""))}}
""";
        }

        private static string GetPostAndPutParameters(Dictionary<string, string> postAndPutParameter, HttpTypeCodes httpType)
        {
            if (httpType != HttpTypeCodes.Post && httpType != HttpTypeCodes.Put)
                return null;

            if (postAndPutParameter == null || postAndPutParameter.Count == 0)
                return ", {}";

            return $", {string.Join(", ", postAndPutParameter.Select(p => p.Key))}";
        }

        #endregion
    }
}
