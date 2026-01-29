using Microsoft.OpenApi.Models;
using Spiderly.Shared.DTO;
using Swashbuckle.AspNetCore.SwaggerGen;

public class ErrorResponseFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        if (!swaggerDoc.Components.Schemas.ContainsKey(nameof(ApiErrorDTO)))
        {
            context.SchemaGenerator.GenerateSchema(typeof(ApiErrorDTO), context.SchemaRepository);
        }

        OpenApiReference errorSchema = new OpenApiReference
        {
            Type = ReferenceType.Schema,
            Id = nameof(ApiErrorDTO),
        };

        foreach (var path in swaggerDoc.Paths.Values)
        {
            foreach (var operation in path.Operations.Values)
            {
                operation.Responses["default"] = new OpenApiResponse
                {
                    Description = "Error",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Schema = new OpenApiSchema { Reference = errorSchema }
                        }
                    }
                };
            }
        }
    }
}