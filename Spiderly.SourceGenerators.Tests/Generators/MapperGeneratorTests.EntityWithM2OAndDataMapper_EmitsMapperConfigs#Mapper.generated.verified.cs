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
            TypeAdapterConfig config = NewStrictConfig();

            config
                .NewConfig<CategoryDTO, Category>()

                ;

            CustomizeCategoryDTOToEntityConfig(config);

            return config;
        }

        /// <summary>
        /// Optional strongly-typed extension seam for CategoryDTOToEntityConfig — implement this partial method
        /// in your hand-written Mapper class to add compiler-checked custom mappings (null-guard
        /// optional navigations: <c>config.ForType&lt;X, XDTO&gt;().Map(dest => dest.Y, src => src.Nav != null ? src.Nav.Y : null)</c>).
        /// </summary>
        static partial void CustomizeCategoryDTOToEntityConfig(TypeAdapterConfig config);

        public static TypeAdapterConfig CategoryToDTOConfig()
        {
            TypeAdapterConfig config = NewStrictConfig();

            config
                .NewConfig<Category, CategoryDTO>()
                
                ;

            CustomizeCategoryToDTOConfig(config);

            return config;
        }

        /// <summary>
        /// Optional strongly-typed extension seam for CategoryToDTOConfig — implement this partial method
        /// in your hand-written Mapper class to add compiler-checked custom mappings (null-guard
        /// optional navigations: <c>config.ForType&lt;X, XDTO&gt;().Map(dest => dest.Y, src => src.Nav != null ? src.Nav.Y : null)</c>).
        /// </summary>
        static partial void CustomizeCategoryToDTOConfig(TypeAdapterConfig config);

        public static TypeAdapterConfig CategoryProjectToConfig()
        {
            TypeAdapterConfig config = NewStrictConfig();

            config
                .NewConfig<Category, CategoryDTO>()
                
                ;

            CustomizeCategoryProjectToConfig(config);

            return config;
        }

        /// <summary>
        /// Optional strongly-typed extension seam for CategoryProjectToConfig — implement this partial method
        /// in your hand-written Mapper class to add compiler-checked custom mappings (null-guard
        /// optional navigations: <c>config.ForType&lt;X, XDTO&gt;().Map(dest => dest.Y, src => src.Nav != null ? src.Nav.Y : null)</c>).
        /// </summary>
        static partial void CustomizeCategoryProjectToConfig(TypeAdapterConfig config);

        public static TypeAdapterConfig CategoryExcelProjectToConfig()
        {
            TypeAdapterConfig config = NewStrictConfig();

            config
                .NewConfig<Category, CategoryDTO>()
                
                ;

            CustomizeCategoryExcelProjectToConfig(config);

            return config;
        }

        /// <summary>
        /// Optional strongly-typed extension seam for CategoryExcelProjectToConfig — implement this partial method
        /// in your hand-written Mapper class to add compiler-checked custom mappings (null-guard
        /// optional navigations: <c>config.ForType&lt;X, XDTO&gt;().Map(dest => dest.Y, src => src.Nav != null ? src.Nav.Y : null)</c>).
        /// </summary>
        static partial void CustomizeCategoryExcelProjectToConfig(TypeAdapterConfig config);

        #endregion


        #region Product

        public static TypeAdapterConfig ProductDTOToEntityConfig()
        {
            TypeAdapterConfig config = NewStrictConfig();

            config
                .NewConfig<ProductDTO, Product>()

                ;

            CustomizeProductDTOToEntityConfig(config);

            return config;
        }

        /// <summary>
        /// Optional strongly-typed extension seam for ProductDTOToEntityConfig — implement this partial method
        /// in your hand-written Mapper class to add compiler-checked custom mappings (null-guard
        /// optional navigations: <c>config.ForType&lt;X, XDTO&gt;().Map(dest => dest.Y, src => src.Nav != null ? src.Nav.Y : null)</c>).
        /// </summary>
        static partial void CustomizeProductDTOToEntityConfig(TypeAdapterConfig config);

        public static TypeAdapterConfig ProductToDTOConfig()
        {
            TypeAdapterConfig config = NewStrictConfig();

            config
                .NewConfig<Product, ProductDTO>()
                .Map(dest => dest.CategoryId, src => src.Category.Id)
				.Map(dest => dest.CategoryDisplayName, src => src.Category.Name)
                ;

            CustomizeProductToDTOConfig(config);

            return config;
        }

        /// <summary>
        /// Optional strongly-typed extension seam for ProductToDTOConfig — implement this partial method
        /// in your hand-written Mapper class to add compiler-checked custom mappings (null-guard
        /// optional navigations: <c>config.ForType&lt;X, XDTO&gt;().Map(dest => dest.Y, src => src.Nav != null ? src.Nav.Y : null)</c>).
        /// </summary>
        static partial void CustomizeProductToDTOConfig(TypeAdapterConfig config);

        public static TypeAdapterConfig ProductProjectToConfig()
        {
            TypeAdapterConfig config = NewStrictConfig();

            config
                .NewConfig<Product, ProductDTO>()
                .Map(dest => dest.CategoryId, src => src.Category.Id)
				.Map(dest => dest.CategoryDisplayName, src => src.Category.Name)
                ;

            CustomizeProductProjectToConfig(config);

            return config;
        }

        /// <summary>
        /// Optional strongly-typed extension seam for ProductProjectToConfig — implement this partial method
        /// in your hand-written Mapper class to add compiler-checked custom mappings (null-guard
        /// optional navigations: <c>config.ForType&lt;X, XDTO&gt;().Map(dest => dest.Y, src => src.Nav != null ? src.Nav.Y : null)</c>).
        /// </summary>
        static partial void CustomizeProductProjectToConfig(TypeAdapterConfig config);

        public static TypeAdapterConfig ProductExcelProjectToConfig()
        {
            TypeAdapterConfig config = NewStrictConfig();

            config
                .NewConfig<Product, ProductDTO>()
                .Map(dest => dest.CategoryId, src => src.Category.Id)
				.Map(dest => dest.CategoryDisplayName, src => src.Category.Name)
                ;

            CustomizeProductExcelProjectToConfig(config);

            return config;
        }

        /// <summary>
        /// Optional strongly-typed extension seam for ProductExcelProjectToConfig — implement this partial method
        /// in your hand-written Mapper class to add compiler-checked custom mappings (null-guard
        /// optional navigations: <c>config.ForType&lt;X, XDTO&gt;().Map(dest => dest.Y, src => src.Nav != null ? src.Nav.Y : null)</c>).
        /// </summary>
        static partial void CustomizeProductExcelProjectToConfig(TypeAdapterConfig config);

        #endregion


        /// <summary>
        /// A Mapster config with the convention-FLATTENING member strategy stripped: an unmapped
        /// DTO property stays at its default instead of silently resolving through a same-named
        /// navigation chain (e.g. dest.ShippingTierIsBulky -> src.ShippingTier.IsBulky), where an
        /// optional navigation's LEFT JOIN NULL crashes EF's shaper on a non-nullable member.
        /// Deliberate custom mappings go through the Customize* partial hooks instead.
        /// </summary>
        private static TypeAdapterConfig NewStrictConfig()
        {
            TypeAdapterConfig config = new();

            foreach (TypeAdapterRule rule in config.Rules)
                rule.Settings.ValueAccessingStrategies.Remove(ValueAccessingStrategy.FlattenMember);

            return config;
        }
    }
}
