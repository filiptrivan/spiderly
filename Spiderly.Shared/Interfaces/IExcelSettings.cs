namespace Spiderly.Shared.Interfaces
{
    /// <summary>
    /// Read-only view of the Excel export settings. Implemented by <see cref="Settings"/> and injected
    /// (via the generated <c>EntityServiceDependencies</c> bundle for services, and resolved from the
    /// service provider in generated controllers), so generated export code depends on configuration
    /// rather than the global mutable <c>SettingsProvider</c> static.
    /// </summary>
    public interface IExcelSettings
    {
        /// <summary>MIME content type returned for exported Excel files.</summary>
        string ExcelContentType { get; }

        /// <summary>Maximum number of rows included in a single Excel export.</summary>
        int ExcelExportMaxRows { get; }
    }
}
