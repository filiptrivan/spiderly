using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Spiderly.SourceGenerators.Shared;
using System.Reflection;
using Spiderly.SourceGenerators.Models;
using System.Runtime.Serialization.Json;
using System.Collections.Immutable;
using System.Data;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators;

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
            IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations = Helpers.GetClassIncrementalValuesProvider(context.SyntaxProvider, new List<NamespaceExtensionCodes>
                {
                    NamespaceExtensionCodes.Controllers,
                });

            IncrementalValueProvider<List<SpiderlyClass>> referencedProjectClasses = Helpers.GetIncrementalValueProviderClassesFromReferencedAssemblies(context,
                new List<NamespaceExtensionCodes>
                {
                    NamespaceExtensionCodes.Entities,
                    NamespaceExtensionCodes.DTO,
                });

            IncrementalValueProvider<string> callingProjectDirectory = context.GetCallingPath();

            var combined = classDeclarations.Collect()
                .Combine(referencedProjectClasses)
                .Combine(callingProjectDirectory);

            context.RegisterImplementationSourceOutput(combined, static (spc, source) =>
            {
                var (classesAndEntities, callingPath) = source;
                var (classes, referencedClasses) = classesAndEntities;

                Execute(classes, referencedClasses, callingPath, spc);
            });
        }

        private static void Execute(IList<ClassDeclarationSyntax> classes, List<SpiderlyClass> referencedProjectClasses, string callingProjectDirectory, SourceProductionContext context)
        {
            if (classes.Count <= 1)
                return; // FT: one because of config settings

            if (callingProjectDirectory.Contains(".WebAPI") == false)
                return;

            // ...\Backend\PlayertyLoyals.Business -> ...\Frontend\src\app\business\services\api\api.service.generated.ts
            string outputPath = callingProjectDirectory.ReplaceEverythingAfter(@"\Backend\", @"\Frontend\src\app\business\services\api\api.service.generated.ts");

            List<SpiderlyClass> currentProjectClasses = Helpers.GetSpiderlyClasses(classes, referencedProjectClasses);

            List<SpiderlyClass> controllerClasses = currentProjectClasses
                .Where(x => x.Namespace.EndsWith($".{NamespaceExtensionCodes.Controllers}"))
                .ToList();

            List<SpiderlyClass> referencedDTOClasses = referencedProjectClasses
                .Where(x => x.Namespace.EndsWith($".{NamespaceExtensionCodes.DTO}"))
                .ToList();

            List<SpiderlyClass> referencedProjectEntities = referencedProjectClasses
                .Where(x => x.Namespace.EndsWith($".{NamespaceExtensionCodes.Entities}"))
                .ToList();

            List<SpiderlyClass> currentAppEntities = referencedProjectEntities
                .Where(x => x.Namespace != "Spiderly.Security.Entities")
                .ToList();

            string result = $$"""
{{string.Join("\n", GetImports(referencedDTOClasses))}}

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

{{string.Join("\n\n", GetAngularHttpMethods(controllerClasses, currentAppEntities, referencedProjectEntities, referencedDTOClasses))}}

}
""";

            Helpers.WriteToTheFile(result, outputPath);
        }

        private static List<string> GetAngularHttpMethods(List<SpiderlyClass> controllerClasses, List<SpiderlyClass> currentAppEntities, List<SpiderlyClass> referencedProjectEntities, List<SpiderlyClass> referencedDTOClasses)
        {
            List<string> result = new();
            HashSet<string> alreadyAddedMethods = new HashSet<string>();

            foreach (SpiderlyClass controllerClass in controllerClasses)
            {
                string controllerName = controllerClass.Name.Replace("Controller", "");

                foreach (SpiderMethod controllerMethod in controllerClass.Methods)
                {
                    if (controllerMethod.HasUIDoNotGenerateAttribute())
                        continue;

                    alreadyAddedMethods.Add(controllerMethod.Name);

                    if (controllerMethod.Parameters.Any(x => x.HasFromFormAttribute()) && controllerMethod.Parameters.Any(x => x.Type == "IFormFile") == false)
                    {
                        result.Add(GetCustomFromFormControllerMethod(controllerMethod, controllerName, referencedDTOClasses));
                    }
                    else
                    {
                        result.Add(GetCustomAngularControllerMethod(controllerMethod, controllerName));
                    }
                }
            }

            foreach (SpiderlyClass entity in currentAppEntities.Where(x => x.HasUIDoNotGenerateAttribute() == false))
            {
                result.Add(GetBaseAngularControllerMethods(entity, referencedProjectEntities, alreadyAddedMethods));
            }

            return result;
        }

        private static List<string> GetImports(List<SpiderlyClass> DTOClasses)
        {
            List<string> result = new();

            result.Add($$"""
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiSecurityService, Filter, PaginatedResult, Namebook, Codebook, LazyLoadSelectedIdsResult, VerificationTokenRequest, AuthResult, ExternalProvider } from 'spiderly';
import { ConfigService } from '../config.service';
""");

            foreach (SpiderlyClass DTOClass in DTOClasses)
            {
                string[] projectNameHelper = DTOClass.Namespace.Split('.');
                string projectName = projectNameHelper[projectNameHelper.Length - 2];

                if (projectName == "Security")
                    continue;

                string ngType = DTOClass.Name.Replace("DTO", "");

                if (Helpers.BaseClassNames.Contains(ngType))
                    continue;

                result.Add($$"""
import { {{ngType}} } from '../../entities/{{projectName.FromPascalToKebabCase()}}-entities.generated';
""");
            }

            return result;
        }

        #region Custom Angular Controller Method

        private static string GetCustomAngularControllerMethod(SpiderMethod controllerMethod, string controllerName)
        {
            string angularReturnType = Helpers.GetAngularType(controllerMethod.ReturnType);

            HttpTypeCodes httpType = GetHttpType(controllerMethod);

            Dictionary<string, string> inputParameters = controllerMethod.Parameters
                .ToDictionary(
                    x => x.Name,
                    x => Helpers.GetAngularType(x.Type)
                );

            string httpOptions = GetHttpOptions(controllerMethod);

            return GetAngularControllerMethod(controllerMethod.Name, inputParameters, angularReturnType, httpType, controllerName, httpOptions);
        }

        private static string GetHttpOptions(SpiderMethod controllerMethod)
        {
            if (Helpers.GetAngularType(controllerMethod.ReturnType) == "string")
                return Settings.HttpOptionsText;

            if (controllerMethod.ReturnType.Contains("IActionResult"))
                return Settings.HttpOptionsBlob;

            bool skipSpinner = controllerMethod.Attributes.Any(attr => attr.Name == "SkipSpinner");

            if (skipSpinner || 
                controllerMethod.ReturnType.Contains("NamebookDTO") || 
                controllerMethod.ReturnType.Contains("CodebookDTO") || 
                controllerMethod.ReturnType.Contains("PaginatedResultDTO") ||
                controllerMethod.ReturnType.Contains("LazyLoadSelectedIdsResultDTO"))
            {
                return Settings.HttpOptionsSkipSpinner;
            }

            return Settings.HttpOptionsBase;    
        }

        private static HttpTypeCodes GetHttpType(SpiderMethod controllerMethod)
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
                throw new NotImplementedException("Http type doesn't exist.");
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

{{GetBaseSaveAngularControllerMethod(entity, alreadyAddedMethods)}}

{{string.Join("\n\n", GetBaseUploadBlobAngularControllerMethods(entity, alreadyAddedMethods))}}

{{GetBaseDeleteAngularControllerMethods(entity, alreadyAddedMethods)}}

""";
        }

        private static string GetCustomFromFormControllerMethod(SpiderMethod controllerMethod, string controllerName, List<SpiderlyClass> DTOList)
        {
            SpiderParameter parameter = controllerMethod.Parameters.Single();
            SpiderlyClass parameterType = DTOList.Where(x => x.Name == parameter.Type).SingleOrDefault();
            string angularReturnType = Helpers.GetAngularType(controllerMethod.ReturnType);

            return $$"""
    {{controllerMethod.Name.FirstCharToLower()}} = (dto: {{parameter.Type.Replace("DTO", "")}}): Observable<{{angularReturnType}}> => { 
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
                if (property.Type == "List<IFormFile>")
                {
                    result.Add($$"""
        dto.{{property.Name.FirstCharToLower()}}.forEach((file: File) => {
            formData.append('{{property.Name}}', file);
        });
""");
                }
                else if (property.Type == "IFormFile")
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
                    result.Add(GetBaseSimpleManyToManyTableDataAngularControllerMethod(property, entity, allEntities, alreadyAddedMethods));
                    result.Add(GetBaseSimpleManyToManyTableDataExportAngularControllerMethod(property, entity, alreadyAddedMethods));
                    result.Add(GetBaseSimpleManyToManyTableLazyLoadAngularControllerMethod(property, entity, alreadyAddedMethods));
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

        private static string GetBaseSimpleManyToManyTableDataAngularControllerMethod(SpiderlyProperty property, SpiderlyClass entity, List<SpiderlyClass> entities, HashSet<string> alreadyAddedMethods)
        {
            string methodName = $"GetPaginated{property.Name}ListFor{entity.Name}";

            if (alreadyAddedMethods.Contains(methodName))
                return null;

            Dictionary<string, string> postAndPutParameter = new Dictionary<string, string> { { "filterDTO", "Filter" } };

            SpiderlyClass extractedEntity = entities.Where(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type)).SingleOrDefault();

            return GetAngularControllerMethod(methodName, postAndPutParameter, $"PaginatedResult<{extractedEntity.Name}>", HttpTypeCodes.Post, entity.ControllerName, Settings.HttpOptionsSkipSpinner);
        }

        private static string GetBaseSimpleManyToManyTableDataExportAngularControllerMethod(SpiderlyProperty property, SpiderlyClass entity, HashSet<string> alreadyAddedMethods)
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

            Dictionary<string, string> postAndPutParameter = new Dictionary<string, string> { { "file", "any" } };

            return GetAngularControllerMethod(methodName, postAndPutParameter, "string", HttpTypeCodes.Post, entity.ControllerName, Settings.HttpOptionsText);
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
                        { $"{entity.Name.FirstCharToLower()}Id?", "number"} 
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

                    Dictionary<string, string> getAndDeleteParameters = new() { { $"{entity.Name.FirstCharToLower()}Id?", "number"} };

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

            return GetAngularControllerMethod(methodName, getAndDeleteParameters, returnType:$"{entity.Name}MainUIForm", HttpTypeCodes.Get, entity.ControllerName, Settings.HttpOptionsBase);
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
        return this.http.{{httpType.ToString().FirstCharToLower()}}{{GetReturnTypeAfterHttpType(returnType)}}(`${this.config.apiUrl}/{{controllerName}}/{{methodName}}{{GetGetAndDeleteParameters(inputParameters, httpType)}}`{{GetPostAndPutParameters(inputParameters, httpType)}}{{httpOptions}});
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

        private static string GetGetAndDeleteParameters(Dictionary<string, string> getAndDeleteParameters, HttpTypeCodes httpType)
        {
            if (httpType != HttpTypeCodes.Get && httpType != HttpTypeCodes.Delete)
                return null;

            if (getAndDeleteParameters == null || getAndDeleteParameters.Count == 0)
                return null;

            return $"?{string.Join("&", getAndDeleteParameters.Select(x => $"{x.Key.Replace("?", "")}=${{{x.Key.Replace("?", "")}}}"))}";
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
