using Microsoft.Extensions.Localization;
using MiniExcelLibs;
using MiniExcelLibs.Attributes;
using MiniExcelLibs.OpenXml;
using Spiderly.Shared.Localization;
using System.ComponentModel.DataAnnotations;
using System.IO.Compression;
using System.Reflection;
using System.Xml.Linq;

namespace Spiderly.Shared.Excel
{
    public class ExcelService
    {
        public async Task<byte[]> FillReportTemplateAsync<T>(
            IAsyncEnumerable<T> data,
            string[] excelPropertiesToExclude,
            IStringLocalizer localizer,
            CancellationToken cancellationToken = default)
            where T : class
        {
            Type type = typeof(T);
            PropertyInfo[] allProperties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            DynamicExcelColumn[] columns = BuildColumnConfig(allProperties, excelPropertiesToExclude, localizer);

            OpenXmlConfiguration config = new()
            {
                DynamicColumns = columns,
                TableStyles = TableStyles.None,
                AutoFilter = false
            };

            using MemoryStream stream = new();
            await MiniExcel.SaveAsAsync(stream, data, configuration: config, cancellationToken: cancellationToken);

            return stream.ToArray();
        }

        private static DynamicExcelColumn[] BuildColumnConfig(
            PropertyInfo[] allProperties,
            string[] excelPropertiesToExclude,
            IStringLocalizer localizer)
        {
            HashSet<string> excludedSet = new(excelPropertiesToExclude, StringComparer.Ordinal);
            DynamicExcelColumn[] columns = new DynamicExcelColumn[allProperties.Length];
            int visibleIndex = 0;

            for (int i = 0; i < allProperties.Length; i++)
            {
                PropertyInfo prop = allProperties[i];
                bool excluded = excludedSet.Contains(prop.Name);

                DynamicExcelColumn col = new(prop.Name) { Ignore = excluded };

                if (!excluded)
                {
                    col.Index = visibleIndex++;
                    col.Name = localizer.Translate(prop.Name);
                    col.Width = GetColumnWidth(prop);

                    Type underlying = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                    if (underlying == typeof(DateOnly))
                        col.Format = "yyyy-MM-dd";
                    else if (underlying == typeof(TimeOnly))
                        col.Format = "HH:mm";
                }

                columns[i] = col;
            }

            return columns;
        }

        private static double GetColumnWidth(PropertyInfo prop)
        {
            Type type = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

            if (type == typeof(bool)) return 10;
            if (type == typeof(byte) || type == typeof(short) || type == typeof(int) || type == typeof(long)) return 14;
            if (type == typeof(decimal) || type == typeof(double) || type == typeof(float)) return 16;
            if (type == typeof(DateTime) || type == typeof(DateOnly)) return 18;
            if (type == typeof(TimeOnly)) return 12;

            StringLengthAttribute strLen = prop.GetCustomAttribute<StringLengthAttribute>();
            if (strLen != null)
            {
                if (strLen.MaximumLength <= 50) return 20;
                if (strLen.MaximumLength <= 200) return 30;
                return 40;
            }

            return 22;
        }
    }
}
