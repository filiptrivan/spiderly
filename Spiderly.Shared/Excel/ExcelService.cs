using MiniExcelLibs;
using MiniExcelLibs.Attributes;
using MiniExcelLibs.OpenXml;
using System.IO.Compression;
using System.Reflection;
using System.Xml.Linq;
using Spiderly.Shared.Resources;
using Spiderly.Shared.Extensions;

namespace Spiderly.Shared.Excel
{
    public class ExcelService
    {
        public async Task<byte[]> FillReportTemplateAsync<T>(IList<T> data, string[] excelPropertiesToExclude, Func<string, string> getTranslation)
            where T : class
        {
            Type type = typeof(T);
            PropertyInfo[] propertiesToInclude = GetMembersToInclude(excelPropertiesToExclude, type);
            bool hasDateColumns = propertiesToInclude.Any(p => p.PropertyType == typeof(DateTime) || p.PropertyType == typeof(DateTime?));

            IEnumerable<Dictionary<string, object>> rows = StreamRows(data, propertiesToInclude);

            DynamicExcelColumn[] columns = BuildColumnConfig(propertiesToInclude, getTranslation);

            OpenXmlConfiguration config = new()
            {
                DynamicColumns = columns,
                TableStyles = TableStyles.None,
                AutoFilter = false
            };

            MemoryStream stream = new();
            await MiniExcel.SaveAsAsync(stream, rows, configuration: config);

            if (hasDateColumns)
                ApplyBuiltInDateFormat(stream);

            return stream.ToArray();
        }

        private static IEnumerable<Dictionary<string, object>> StreamRows<T>(IList<T> data, PropertyInfo[] propertiesToInclude) where T : class
        {
            int count = data != null ? data.Count : 0;

            for (int i = 0; i < count; i++)
            {
                Dictionary<string, object> row = new(propertiesToInclude.Length);

                foreach (PropertyInfo prop in propertiesToInclude)
                {
                    row[prop.Name] = prop.GetValue(data[i], null);
                }

                yield return row;
            }

            if (count >= SettingsProvider.Current.ExcelExportMaxRows && propertiesToInclude.Length > 0)
            {
                yield return new Dictionary<string, object>
                {
                    [propertiesToInclude[0].Name] = $"Showing first {count} records. Apply filters to narrow results."
                };
            }
        }

        private static DynamicExcelColumn[] BuildColumnConfig(PropertyInfo[] propertiesToInclude, Func<string, string> getTranslation)
        {
            DynamicExcelColumn[] columns = new DynamicExcelColumn[propertiesToInclude.Length];

            for (int i = 0; i < propertiesToInclude.Length; i++)
            {
                PropertyInfo prop = propertiesToInclude[i];

                DynamicExcelColumn col = new(prop.Name)
                {
                    Index = i,
                    Name = GetHeaderTranslation(getTranslation, prop.Name),
                    Width = 22
                };

                if (prop.PropertyType == typeof(DateTime) || prop.PropertyType == typeof(DateTime?))
                {
                    // Placeholder format — gets replaced with built-in format ID 14 (locale-dependent short date) in ApplyBuiltInDateFormat
                    col.Format = "yyyy-MM-dd";
                }

                columns[i] = col;
            }

            return columns;
        }

        /// <summary>
        /// Post-processes the xlsx stream to replace MiniExcel's custom date number format
        /// with Excel's built-in format ID 14 (short date), which renders locale-dependent
        /// (e.g. "2/26/2026" on US, "26.2.2026." on Serbian, "26.02.2026" on German).
        /// </summary>
        private static void ApplyBuiltInDateFormat(MemoryStream stream)
        {
            stream.Position = 0;

            using (ZipArchive archive = new(stream, ZipArchiveMode.Update, leaveOpen: true))
            {
                ZipArchiveEntry stylesEntry = archive.GetEntry("xl/styles.xml");
                if (stylesEntry == null)
                    return;

                XDocument doc;
                using (Stream entryStream = stylesEntry.Open())
                {
                    doc = XDocument.Load(entryStream);
                }

                XNamespace ns = doc.Root.Name.Namespace;
                XElement numFmts = doc.Root.Element(ns + "numFmts");
                if (numFmts == null)
                    return;

                List<string> dateFormatIds = new();

                foreach (XElement numFmt in numFmts.Elements(ns + "numFmt").ToList())
                {
                    string formatCode = numFmt.Attribute("formatCode")?.Value;
                    if (formatCode == "yyyy-MM-dd")
                    {
                        dateFormatIds.Add(numFmt.Attribute("numFmtId").Value);
                        numFmt.Remove();
                    }
                }

                if (dateFormatIds.Count == 0)
                    return;

                int remaining = numFmts.Elements(ns + "numFmt").Count();
                if (remaining == 0)
                    numFmts.Remove();
                else
                    numFmts.SetAttributeValue("count", remaining);

                XElement cellXfs = doc.Root.Element(ns + "cellXfs");
                if (cellXfs != null)
                {
                    foreach (XElement xf in cellXfs.Elements(ns + "xf"))
                    {
                        string fmtId = xf.Attribute("numFmtId")?.Value;
                        if (dateFormatIds.Contains(fmtId))
                        {
                            xf.SetAttributeValue("numFmtId", "14");
                        }
                    }
                }

                stylesEntry.Delete();
                ZipArchiveEntry newEntry = archive.CreateEntry("xl/styles.xml");
                using (Stream entryStream = newEntry.Open())
                {
                    doc.Save(entryStream);
                }
            }
        }

        private static PropertyInfo[] GetMembersToInclude(string[] excelPropertiesToExclude, Type type)
        {
            PropertyInfo[] memberInfos = type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                // uzmi svaki property koji nema isto ime kao parametar iz customAttributeDataList
                .Where(prop => excelPropertiesToExclude.Contains(prop.Name) == false)
                .ToArray();

            return memberInfos;
        }

        private static string GetHeaderTranslation(Func<string, string> getTranslation, string propertyName)
        {
            return
               getTranslation(propertyName) ??
               SharedTerms.ResourceManager.GetTranslation(propertyName) ??
               propertyName;
        }
    }
}
