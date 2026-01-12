using Spiderly.Shared.Attributes;

namespace Spiderly.Security.GeneratorSettings
{
    public class GeneratorSettings
    {
        [Output("false")]
        public bool NgBaseDetailsGenerator { get; set; }

        [Output("false")]
        public bool NgTranslatesGenerator { get; set; }

        [Output("false")]
        public bool TranslationsGenerator { get; set; }

        [Output("false")]
        public bool NgEntitiesGenerator { get; set; }

        [Output("false")]
        public bool NgValidatorsGenerator { get; set; }

        [Output("false")]
        public bool PermissionCodesGenerator { get; set; }

        [Output("false")]
        public bool MapperlyGenerator { get; set; }

        [Output("true")]
        public bool FluentValidationGenerator { get; set; }

        [Output("false")]
        public bool ExcelPropertiesGenerator { get; set; }

        [Output("false")]
        public bool EntitiesToDTOGenerator { get; set; }

        [Output("false")]
        public bool NgControllersGenerator { get; set; }

        [Output("false")]
        public bool PaginatedResultGenerator { get; set; }

        [Output("false")]
        public bool ControllerGenerator { get; set; }

        [Output("false")]
        public bool AuthorizationServicesGenerator { get; set; }

        [Output("false")]
        public bool ServicesGenerator { get; set; }

        [Output("false")]
        public bool NgEnumsGenerator { get; set; }
    }
}