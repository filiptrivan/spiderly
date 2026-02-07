using ClosedXML.Excel;
using System.Globalization;
using System.Reflection;
using System.ComponentModel;
using Spiderly.Shared.Excel.DTO;
using System.Resources;
using Spiderly.Shared.Resources;
using Spiderly.Shared.Extensions;

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

        public MemoryStream FillReportTemplate<T>(IList<T> data, int count, string[] excelPropertiesToExclude, ResourceManager resourceManager, ExcelReportOptionsDTO options = null)
            where T : class
        {
            if (options == null)
                options = new ExcelReportOptionsDTO();

            MemoryStream outputStream = new MemoryStream();

            using (XLWorkbook workbook = new XLWorkbook())
            {
                if (data != null && count > 0)
                {
                    var worksheet = workbook.Worksheets.Add(options.DataSheetName);
                    Type type = typeof(T);
                    PropertyInfo[] propertiesToInclude = GetMembersToInclude(excelPropertiesToExclude, type);

                    LoadFromCollectionOverride(data, count, type, worksheet, propertiesToInclude, resourceManager);
                }
                else
                {
                    // ClosedXML requires at least one worksheet - add a placeholder
                    workbook.Worksheets.Add(options.DataSheetName);
                }
                workbook.SaveAs(outputStream);
            }

            outputStream.Position = 0;
            return outputStream;
        }

        private static PropertyInfo[] GetMembersToInclude(string[] excelPropertiesToExclude, Type type)
        {
            PropertyInfo[] memberInfos = type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(prop => excelPropertiesToExclude.Contains(prop.Name) == false)
                .ToArray();

            return memberInfos;
        }

        private static void LoadFromCollectionOverride<T>(IList<T> data, int count, Type typeofT, IXLWorksheet worksheet, PropertyInfo[] propertiesToInclude, ResourceManager resourceManager)
        {
            int cellRow = 0;
            int cellCol = 0;
            for (int headerIndex = 0; headerIndex < propertiesToInclude.Length; headerIndex++)
            {
                cellCol = headerIndex + 1;

                string propertyName = propertiesToInclude[headerIndex].Name;
                worksheet.Cell(1, cellCol).Value = GetHeaderTranslation(resourceManager, propertyName);

                worksheet.Cell(1, cellCol).Style.Fill.PatternType = XLFillPatternValues.Solid;
                worksheet.Cell(1, cellCol).Style.Fill.BackgroundColor = XLColor.FromHtml("#F0F0F0");
                worksheet.Cell(1, cellCol).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                worksheet.Column(cellCol).Width = 22;

                for (int dataIndex = 0; dataIndex < count; dataIndex++)
                {
                    cellRow = dataIndex + 2;
                    object cellValue = propertiesToInclude[headerIndex].GetValue(data[dataIndex], null);
                    
                    // Handle DateTime separately to preserve the date value in Excel
                    if (propertiesToInclude[headerIndex].PropertyType == typeof(DateTime) || 
                        propertiesToInclude[headerIndex].PropertyType == typeof(DateTime?))
                    {
                        if (cellValue == null)
                            worksheet.Cell(cellRow, cellCol).Value = "";
                        else
                            worksheet.Cell(cellRow, cellCol).Value = (DateTime)cellValue;
                    }
                    else
                    {
                        worksheet.Cell(cellRow, cellCol).Value = (cellValue ?? "").ToString();
                    }
                }

                if (propertiesToInclude[headerIndex].PropertyType == typeof(DateTime) || propertiesToInclude[headerIndex].PropertyType == typeof(DateTime?))
                {
                    // Apply culture-invariant date format for consistent Excel output
                    worksheet.Column(cellCol).Style.NumberFormat.Format = "dd.MM.yyyy.";
                }
            }
        }

        private static string GetHeaderTranslation(ResourceManager resourceManager, string propertyName)
        {
            return 
               resourceManager.GetTranslation(propertyName) ?? 
               SharedTerms.ResourceManager.GetTranslation(propertyName) ?? 
               propertyName;
        }

        /// <summary>
        /// Helper method to convert a value to the target type using TypeDescriptor for proper type conversion
        /// </summary>
        private static object ConvertValueToType(object value, Type targetType)
        {
            if (value == null)
            {
                return targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null 
                    ? Activator.CreateInstance(targetType) 
                    : null;
            }

            var valueType = value.GetType();

            // If the value is already the target type, return it directly
            if (valueType == targetType || targetType.IsAssignableFrom(valueType))
                return value;

            // Use TypeDescriptor for proper conversion
            var converter = TypeDescriptor.GetConverter(targetType);
            if (converter.CanConvertFrom(valueType))
                return converter.ConvertFrom(value)!;

            // Try converting from string
            if (converter.CanConvertFrom(typeof(string)))
            {
                var stringValue = value.ToString();
                if (!string.IsNullOrEmpty(stringValue))
                    return converter.ConvertFrom(stringValue)!;
            }

            // Fallback to Convert.ChangeType for numeric conversions
            try
            {
                return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            }
            catch
            {
                throw new InvalidOperationException($"Cannot convert value of type '{valueType.Name}' to type '{targetType.Name}'.");
            }
        }

        /// <summary>
        /// Convert table to objects using ClosedXML
        /// </summary>
        public static IEnumerable<T> ConvertTableToObjects<T>(IXLTable table) where T : new()
        {
            //DateTime Conversion - Excel stores dates as serial numbers
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

            //Get the properties of T
            var tprops = (new T())
                .GetType()
                .GetProperties()
                .ToList();

            //Get the cells based on the table address
            var start = table.RangeAddress.FirstAddress;
            var end = table.RangeAddress.LastAddress;
            var cells = new List<IXLCell>();

            //Have to use for loops instead of worksheet.Cells to protect against empties
            for (var r = start.RowNumber; r <= end.RowNumber; r++)
                for (var c = start.ColumnNumber; c <= end.ColumnNumber; c++)
                    cells.Add(table.Worksheet.Cell(r, c));

            var groups = cells
                .GroupBy(cell => cell.Address.RowNumber)
                .ToList();

            //Check if we have enough rows for header and data
            if (groups.Count < 2)
            {
                // Return empty list if not enough data (only header or empty)
                return new List<T>();
            }

            //Assume first row has the column names
            var headerGroup = groups.First();
            var headerCells = headerGroup.ToList();
            var columnCount = headerCells.Count;

            //Assume the second row represents column data types
            var dataTypesRow = groups.Skip(1).First();
            var dataTypeCells = dataTypesRow.ToList();

            //Get column names from header - only include columns that match properties
            var colnames = headerCells
                .Select((hcell, idx) => new { Name = hcell.Value.ToString(), index = idx })
                .Where(o => tprops.Select(p => p.Name).Contains(o.Name))
                .ToList();

            //Everything after the header is data
            var rowvalues = groups
                .Skip(1) //Exclude header
                .Select(cg => cg.Select(c => c.Value).ToList());

            //Create the collection container
            var collection = rowvalues
                .Select(row =>
                {
                    var tnew = new T();
                    
                    foreach (var colname in colnames)
                    {
                        // Check if the column index exists in both the header and the row
                        if (colname.index >= columnCount || colname.index >= row.Count)
                            continue;
                            
                        var val = row[colname.index];
                        var dataType = val.Type;
                        var prop = tprops.FirstOrDefault(p => p.Name == colname.Name);
                        
                        if (prop == null)
                            continue;

                        // Get the target type from the property, not from the data row
                        // This ensures we convert data values to the correct property type
                        var targetType = prop.PropertyType;
                        
                        //Extract values based on XLDataType
                        try
                        {
                            switch (dataType)
                            {
                                case XLDataType.Number:
                                    var numVal = (double)val;
                                    
                                    // Check if property type is DateTime (Excel date serial number)
                                    if (targetType == typeof(DateTime))
                                    {
                                        prop.SetValue(tnew, convertDateTime(numVal));
                                    }
                                    else if (targetType == typeof(DateTime?))
                                    {
                                        prop.SetValue(tnew, convertDateTime(numVal));
                                    }
                                    else
                                    {
                                        // Use helper method for proper type conversion
                                        var convertedValue = ConvertValueToType(numVal, targetType);
                                        prop.SetValue(tnew, convertedValue);
                                    }
                                    break;
                                case XLDataType.Boolean:
                                    var boolVal = (bool)val;
                                    if (targetType == typeof(bool))
                                        prop.SetValue(tnew, boolVal);
                                    else if (targetType == typeof(bool?))
                                        prop.SetValue(tnew, boolVal);
                                    else
                                        throw new NotImplementedException(String.Format("Type '{0}' not implemented yet!", targetType.Name));
                                    break;
                                case XLDataType.DateTime:
                                    var dateVal = (DateTime)val;
                                    if (targetType == typeof(DateTime) || targetType == typeof(DateTime?))
                                        prop.SetValue(tnew, dateVal);
                                    else
                                        throw new NotImplementedException(String.Format("Type '{0}' not implemented yet!", targetType.Name));
                                    break;
                            default:
                                //String, Empty, or Error type
                                var strVal = val.ToString();
                                
                                // Handle all target types from string values using TypeDescriptor
                                var stringConvertedValue = ConvertValueToType(strVal, targetType);
                                prop.SetValue(tnew, stringConvertedValue);
                                break;
                            }
                        }
                        catch (Exception ex) when (ex is NotImplementedException || ex is InvalidOperationException)
                        {
                            // Re-throw known exceptions
                            throw;
                        }
                        catch (Exception ex)
                        {
                            // Wrap other exceptions for clarity
                            throw new InvalidOperationException(
                                $"Failed to set property '{prop.Name}' with value from Excel cell: {ex.Message}", ex);
                        }
                    }

                    return tnew;
                });


            //Send it back
            return collection;
        }
    }
}
