using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Spiderly.SourceGenerators.Angular
{
    /// <summary>
    /// Generates Angular entity and DTO classes (`{your-app-name}\Frontend\src\app\business\entities\entities.generated.ts`)
    /// based on corresponding C# classes within the '.Entities' and '.DTO' namespaces.
    /// This generator simplifies frontend data modeling by automatically creating TypeScript interfaces/classes
    /// that mirror your backend data structures.
    /// </summary>
    [Generator]
    public class NgEntitiesGenerator : IIncrementalGenerator
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
            if (classes.Count == 0)
                return;

            if (!config.IsGeneratorEnabled(nameof(NgEntitiesGenerator)))
                return;

            List<SpiderlyClass> currentProjectClasses = SpiderlyClassFactory.GetSpiderlyClasses(classes, referencedProjectClasses);
            List<SpiderlyClass> allClasses = currentProjectClasses.Concat(referencedProjectClasses).ToList();
            List<SpiderlyClass> currentProjectDTOClasses = SpiderlyClassFactory.GetDTOClasses(currentProjectClasses, allClasses);

            // ...\Backend\PlayertyLoyals.Business -> ...\Frontend\src\app\business\entities\entities.generated.ts
            string rootPath = callingProjectDirectory.GetRootPath();
            string outputPath = Path.Combine(rootPath, "Frontend", "src", "app", "business", "entities", "entities.generated.ts");

            StringBuilder sb = new();
            StringBuilder sbImports = new();
            sbImports.Append($$"""
import { BaseEntity, Filter, FilterRule, FilterSortMeta, Namebook } from 'spiderly';
{{string.Join("\n", GetEnumPropertyImports(currentProjectDTOClasses))}}

""");

            foreach (IGrouping<string, SpiderlyClass> DTOClassGroup in currentProjectDTOClasses.GroupBy(x => x.Name)) // Grouping because UserDTO.generated and UserDTO
            {
                List<SpiderlyProperty> DTOProperties = new();

                foreach (SpiderlyClass DTOClass in DTOClassGroup) // It can only be 2 here
                    DTOProperties.AddRange(DTOClass.Properties);

                List<string> angularPropertyDefinitions = GetAllAngularPropertyDefinitions(DTOProperties);
                string angularClassIdentifier = DTOClassGroup.Key.Replace("DTO", "");

                sbImports.Append(string.Join("\n", AngularTypeMapper.GetAngularImports(DTOProperties)));

                sb.AppendLine($$"""


export class {{angularClassIdentifier}} extends BaseEntity
{
    {{string.Join("\n\t", angularPropertyDefinitions)}}

    constructor(
    {
        {{string.Join(",\n\t\t", DTOProperties.Select(x => x.Type.IsEnumerable() ? $"{x.Name.FirstCharToLower()} = []" : x.Name.FirstCharToLower()))}}
    }:{
        {{string.Join("\n\t\t", angularPropertyDefinitions)}}     
    } = {}
    ) {
        super(); 

        {{string.Join("\n\t\t", GetAngularPropertyAssignments(DTOProperties))}}
    }

    static schema = {
{{GetSchemaProperties(DTOProperties)}}
    } as const;

    static typeName = '{{angularClassIdentifier}}' as const;
}
""");

            }

            sbImports.Append(sb);

            Helpers.WriteToTheFile(sbImports.ToString(), outputPath);
        }

        private static List<string> GetAllAngularPropertyDefinitions(List<SpiderlyProperty> DTOProperties)
        {
            List<string> result = new();

            foreach (SpiderlyProperty DTOProp in DTOProperties)
            {
                string DTOPropLowerCase = DTOProp.Name.FirstCharToLower();

                string angularDataType = AngularTypeMapper.GetAngularType(DTOProp.Type);
                result.Add($"{DTOPropLowerCase}?: {angularDataType};");
            }

            return result;
        }

        private static List<string> GetAngularPropertyAssignments(List<SpiderlyProperty> DTOProperties)
        {
            List<string> result = new();

            foreach (SpiderlyProperty DTOProp in DTOProperties)
            {
                string DTOPropLowerCase = DTOProp.Name.FirstCharToLower();
                result.Add($"this.{DTOPropLowerCase} = {DTOPropLowerCase};");
            }

            return result;
        }

        private static string GetSchemaProperties(List<SpiderlyProperty> DTOProperties)
        {
            StringBuilder result = new();

            foreach (SpiderlyProperty DTOProp in DTOProperties)
            {
                string DTOPropLowerCase = DTOProp.Name.FirstCharToLower();
                string angularDataType = AngularTypeMapper.GetAngularType(DTOProp.Type);

                result.AppendLine($$"""
        {{DTOPropLowerCase}}: {
            type: '{{angularDataType}}',
            {{(DTOProp.Type.IsManyToOneType() || DTOProp.Type.IsOneToManyType() ? $"get nestedConstructor() {{ return {angularDataType.Replace("[]", "")}; }}," : "")}}
            {{(DTOProp.IsSaveBodyMainDTO ? $"isSaveBodyMainDTO: true," : "")}}
        },
""");
            }

            return result.ToString();
        }

        private static List<string> GetEnumPropertyImports(List<SpiderlyClass> DTOClasses)
        {
            List<string> result = new();

            foreach (IGrouping<string, SpiderlyClass> DTOClassGroup in DTOClasses.GroupBy(x => x.Name)) // Grouping because UserDTO.generated and UserDTO
            {
                List<SpiderlyProperty> DTOProperties = new List<SpiderlyProperty>();

                foreach (SpiderlyClass DTOClass in DTOClassGroup) // It can only be 2 here
                    DTOProperties.AddRange(DTOClass.Properties);

                foreach (SpiderlyProperty property in DTOProperties.Where(x => x.Type.IsEnum()))
                {
                    if (result.Contains(property.Name) == false)
                    {
                        result.Add($$"""
import { {{property.Type}} } from "../enums/enums.generated";
""");
                    }
                }
            }

            return result;
        }


    }
}
