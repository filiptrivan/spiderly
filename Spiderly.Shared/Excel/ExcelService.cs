using ClosedXML.Excel;
using System.Globalization;
using System.Reflection;
using Spiderly.Shared.Excel.DTO;
using System.Resources;
using Spiderly.Shared.Resources;
using Spiderly.Shared.Extensions;
using System;

namespace Spiderly.Shared.Excel
{
    public class ExcelService
    {
        private string _excelTemplatesFullPath;
        public string ExcelTemplatesFullPath
        {
            get
            {
                if (_excelTemplatesFullPath == null)
                {
                    _excelTemplatesFullPath = "Excel\\ExcelTemplates";
                    if (!Path.IsPathRooted(_excelTemplatesFullPath))
                    {
                        _excelTemplatesFullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _excelTemplatesFullPath);
                    }
                    if (!Directory.Exists(_excelTemplatesFullPath))
                    {
                        throw new DirectoryNotFoundException(string.Format(CultureInfo.CurrentCulture, "Excel templates path \"{0}\" does not exist.", _excelTemplatesFullPath));
                    }
                }
                return _excelTemplatesFullPath;
            }
        }

        public MemoryStream LoadExcelToMemoryStream(string excelName)
        {
            string fileName = $"{excelName}.xlsx";
            string filePath = Path.Combine(ExcelTemplatesFullPath, fileName);
            FileStream fileStream = File.Open(filePath, FileMode.Open);
            MemoryStream mem = new MemoryStream();
            fileStream.CopyTo(mem);
            return mem;
        }

        public MemoryStream FillReportTemplate<T>(IList<T> data, int count, string[] excelPropertiesToExclude, Func<string, string> getTranslation, ExcelReportOptionsDTO options = null)
            where T : class
        {
            if (options == null)
                options = new ExcelReportOptionsDTO();

            MemoryStream outputStream = new MemoryStream();

            using (XLWorkbook workbook = new XLWorkbook())
            {
                IXLWorksheet sheet = workbook.Worksheets.Add(options.DataSheetName);
                Type type = typeof(T);
                PropertyInfo[] propertiesToInclude = GetMembersToInclude(excelPropertiesToExclude, type);

                LoadFromCollectionOverride(data, count, type, sheet, propertiesToInclude, getTranslation);
                workbook.SaveAs(outputStream);
            }

            outputStream.Position = 0;
            return outputStream;
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

        private static void LoadFromCollectionOverride<T>(IList<T> data, int count, Type typeofT, IXLWorksheet sheet, PropertyInfo[] propertiesToInclude, Func<string, string> getTranslation)
        {
            int cellRow = 0;
            int cellCol = 0;
            for (int headerIndex = 0; headerIndex < propertiesToInclude.Length; headerIndex++)
            {
                cellCol = headerIndex + 1;

                string propertyName = propertiesToInclude[headerIndex].Name;
                sheet.Cell(1, cellCol).Value = GetHeaderTranslation(getTranslation, propertyName);

                sheet.Cell(1, cellCol).Style.Fill.BackgroundColor = XLColor.FromHtml("#F0F0F0");
                sheet.Cell(1, cellCol).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                sheet.Column(cellCol).Width = 22;

                if (data != null && count > 0)
                {
                    for (int dataIndex = 0; dataIndex < count; dataIndex++)
                    {
                        cellRow = dataIndex + 2;
                        if (typeofT==typeof(string) || typeofT==typeof(decimal) || typeofT==typeof(DateTime) || typeofT.IsPrimitive)
                        {
                            sheet.Cell(cellRow, cellCol).SetValue(XLCellValue.FromObject(data[dataIndex]));
                        }
                        else
                        {
                            sheet.Cell(cellRow, cellCol).SetValue(XLCellValue.FromObject(propertiesToInclude[headerIndex].GetValue(data[dataIndex], null)));
                        }
                    }
                }

                if (propertiesToInclude[headerIndex].PropertyType==typeof(DateTime) || propertiesToInclude[headerIndex].PropertyType==typeof(DateTime?))
                {
                    sheet.Column(cellCol).Style.NumberFormat.Format = "dd.MM.yyyy."; // TODO FT: make this with locale
                }
            }
        }

        private static string GetHeaderTranslation(Func<string, string> getTranslation, string propertyName)
        {
            return
               getTranslation(propertyName) ??
               SharedTerms.ResourceManager.GetTranslation(propertyName) ??
               propertyName;
        }

        /// <summary>
        /// https://stackoverflow.com/questions/36637882/epplus-read-excel-table
        /// </summary>
        public static IEnumerable<T> ConvertTableToObjects<T>(IXLTable table) where T : new()
        {
            var convertDateTime = new Func<double, DateTime>(excelDate =>
            {
                if (excelDate < 1)
                    throw new ArgumentException("Excel dates cannot be smaller than 0.");

                var dateOfReference = new DateTime(1900, 1, 1);

                if (excelDate > 60d)
                    excelDate = excelDate - 2;
                else
                    excelDate = excelDate - 1;
                return dateOfReference.AddDays(excelDate);
            });

            var tprops = (new T())
                .GetType()
                .GetProperties()
                .ToList();

            IXLRangeAddress address = table.RangeAddress;
            int startRow = address.FirstAddress.RowNumber;
            int endRow = address.LastAddress.RowNumber;
            int startCol = address.FirstAddress.ColumnNumber;
            int endCol = address.LastAddress.ColumnNumber;

            List<IXLCell> cells = new List<IXLCell>();

            for (int r = startRow; r <= endRow; r++)
                for (int c = startCol; c <= endCol; c++)
                    cells.Add(table.Worksheet.Cell(r, c));

            var groups = cells
                .GroupBy(cell => cell.Address.RowNumber)
                .ToList();

            var types = groups
                .Skip(1)
                .First()
                .Select(rcell => rcell.Value.Type == XLDataType.Number ? typeof(double) : typeof(string))
                .ToList();

            var colnames = groups
                .First()
                .Select((hcell, idx) => new { Name = hcell.GetString(), index = idx })
                .Where(o => tprops.Select(p => p.Name).Contains(o.Name))
                .ToList();

            var rowvalues = groups
                .Skip(1)
                .Select(cg => cg.Select(c => (object)(c.Value.Type == XLDataType.Number ? c.GetDouble() : c.GetString())).ToList());

            var collection = rowvalues
                .Select(row =>
                {
                    var tnew = new T();
                    colnames.ForEach(colname =>
                    {
                        var val = row[colname.index];
                        var type = types[colname.index];
                        var prop = tprops.First(p => p.Name == colname.Name);

                        if (type == typeof(double))
                        {
                            if (!string.IsNullOrWhiteSpace(val?.ToString()))
                            {
                                var unboxedVal = (double)val;

                                if (prop.PropertyType == typeof(Int32))
                                    prop.SetValue(tnew, (int)unboxedVal);
                                else if (prop.PropertyType == typeof(double))
                                    prop.SetValue(tnew, unboxedVal);
                                else if (prop.PropertyType == typeof(DateTime))
                                    prop.SetValue(tnew, convertDateTime(unboxedVal));
                                else
                                    throw new NotImplementedException(String.Format("Type '{0}' not implemented yet!", prop.PropertyType.Name));
                            }
                        }
                        else
                        {
                            prop.SetValue(tnew, val);
                        }
                    });

                    return tnew;
                });


            return collection;
        }
    }
}
