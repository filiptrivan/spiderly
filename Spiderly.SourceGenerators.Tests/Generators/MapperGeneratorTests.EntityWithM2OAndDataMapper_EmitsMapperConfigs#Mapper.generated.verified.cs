//HintName: Mapper.generated.cs
using Mapster;
using Microsoft.AspNetCore.Http;
using TestApp.Business.DTO;
using TestApp.Business.Entities;

namespace TestApp.Business.DataMappers
{
    public static partial class Mapper
    {

        #region Category

        public static TypeAdapterConfig CategoryDTOToEntityConfig()
        {
            TypeAdapterConfig config = new();

            config
                .NewConfig<CategoryDTO, Category>()

                ;

            return config;
        }

        public static TypeAdapterConfig CategoryToDTOConfig()
        {
            TypeAdapterConfig config = new();

            config
                .NewConfig<Category, CategoryDTO>()
                
                ;

            return config;
        }

        public static TypeAdapterConfig CategoryProjectToConfig()
        {
            TypeAdapterConfig config = new();

            config
                .NewConfig<Category, CategoryDTO>()
                
                ;

            return config;
        }

        public static TypeAdapterConfig CategoryExcelProjectToConfig()
        {
            TypeAdapterConfig config = new();

            config
                .NewConfig<Category, CategoryDTO>()
                
                ;

            return config;
        }

        #endregion


        #region Product

        public static TypeAdapterConfig ProductDTOToEntityConfig()
        {
            TypeAdapterConfig config = new();

            config
                .NewConfig<ProductDTO, Product>()

                ;

            return config;
        }

        public static TypeAdapterConfig ProductToDTOConfig()
        {
            TypeAdapterConfig config = new();

            config
                .NewConfig<Product, ProductDTO>()
                .Map(dest => dest.CategoryId, src => src.Category.Id)
				.Map(dest => dest.CategoryDisplayName, src => src.Category.Name)
                ;

            return config;
        }

        public static TypeAdapterConfig ProductProjectToConfig()
        {
            TypeAdapterConfig config = new();

            config
                .NewConfig<Product, ProductDTO>()
                .Map(dest => dest.CategoryId, src => src.Category.Id)
				.Map(dest => dest.CategoryDisplayName, src => src.Category.Name)
                ;

            return config;
        }

        public static TypeAdapterConfig ProductExcelProjectToConfig()
        {
            TypeAdapterConfig config = new();

            config
                .NewConfig<Product, ProductDTO>()
                .Map(dest => dest.CategoryId, src => src.Category.Id)
				.Map(dest => dest.CategoryDisplayName, src => src.Category.Name)
                ;

            return config;
        }

        #endregion

    }
}
