namespace Spiderly.Shared
{
    /// <summary>
    /// Excel export options. Bound from the <c>AppSettings:Spiderly.Shared</c> configuration section and
    /// injected (via the generated <c>EntityServiceDependencies</c> bundle for services, and resolved
    /// from the service provider in generated controllers) as
    /// <see cref="Microsoft.Extensions.Options.IOptions{T}"/>.
    /// </summary>
    public class ExcelOptions
    {
        /// <summary>MIME content type returned for exported Excel files.</summary>
        public string ExcelContentType { get; set; } = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        /// <summary>Maximum number of rows included in a single Excel export.</summary>
        public int ExcelExportMaxRows { get; set; } = 100_000;
    }
}
