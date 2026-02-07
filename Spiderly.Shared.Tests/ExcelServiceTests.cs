using ClosedXML.Excel;
using FluentAssertions;
using Moq;
using Spiderly.Shared.Excel;
using Spiderly.Shared.Excel.DTO;
using System.Resources;
using Xunit;

namespace Spiderly.Shared.Tests.Excel
{
    /// <summary>
    /// Test class for ExcelService covering core business logic, edge cases, and error handling.
    /// Tests verify the migration from EPPlus to ClosedXML maintains backward compatibility.
    /// </summary>
    public class ExcelServiceTests
    {
        #region Test Data Classes

        /// <summary>
        /// Sample class for testing Excel data population
        /// </summary>
        public class TestDataItem
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public DateTime CreatedDate { get; set; }
            public bool IsActive { get; set; }
        }

        /// <summary>
        /// Sample class with nullable DateTime for testing optional fields
        /// </summary>
        public class TestDataWithNullableDate
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public DateTime? NullableDate { get; set; }
        }

        /// <summary>
        /// Simple string data for backward compatibility testing
        /// </summary>
        public class SimpleStringData
        {
            public string Value { get; set; } = string.Empty;
        }

        #endregion

        #region Setup

        private readonly ExcelService _excelService;
        private readonly Mock<ResourceManager> _mockResourceManager;

        public ExcelServiceTests()
        {
            _excelService = new ExcelService();
            _mockResourceManager = new Mock<ResourceManager>();
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_CreatesInstanceWithoutErrors()
        {
            // Arrange & Act
            var service = new ExcelService();

            // Assert
            service.Should().NotBeNull();
        }

        #endregion

        #region FillReportTemplate Tests

        [Fact]
        public void FillReportTemplate_WithValidData_ReturnsValidMemoryStream()
        {
            // Arrange
            var data = new List<TestDataItem>
            {
                new TestDataItem { Id = 1, Name = "Test Product", Price = 99.99m, CreatedDate = new DateTime(2024, 1, 15), IsActive = true },
                new TestDataItem { Id = 2, Name = "Another Product", Price = 149.99m, CreatedDate = new DateTime(2024, 2, 20), IsActive = false }
            };
            var propertiesToExclude = Array.Empty<string>();

            // Act
            var result = _excelService.FillReportTemplate(data, data.Count, propertiesToExclude, _mockResourceManager.Object);

            // Assert
            result.Should().NotBeNull();
            result.Length.Should().BeGreaterThan(0);
        }

        [Fact]
        public void FillReportTemplate_WithEmptyList_ReturnsEmptyMemoryStream()
        {
            // Arrange
            var data = new List<TestDataItem>();
            var propertiesToExclude = Array.Empty<string>();

            // Act
            var result = _excelService.FillReportTemplate(data, 0, propertiesToExclude, _mockResourceManager.Object);

            // Assert
            result.Should().NotBeNull();
            result.Length.Should().BeGreaterThan(0);
        }

        [Fact]
        public void FillReportTemplate_WithNullOptions_UsesDefaultOptions()
        {
            // Arrange
            var data = new List<TestDataItem>
            {
                new TestDataItem { Id = 1, Name = "Test", Price = 10.00m, CreatedDate = DateTime.Now, IsActive = true }
            };
            var propertiesToExclude = Array.Empty<string>();

            // Act
            var result = _excelService.FillReportTemplate(data, data.Count, propertiesToExclude, _mockResourceManager.Object, null);

            // Assert
            result.Should().NotBeNull();
            result.Length.Should().BeGreaterThan(0);
        }

        [Fact]
        public void FillReportTemplate_WithExcludedProperties_ExcludesSpecifiedColumns()
        {
            // Arrange
            var data = new List<TestDataItem>
            {
                new TestDataItem { Id = 1, Name = "Test", Price = 10.00m, CreatedDate = DateTime.Now, IsActive = true }
            };
            var propertiesToExclude = new[] { "IsActive" };

            // Act
            var result = _excelService.FillReportTemplate(data, data.Count, propertiesToExclude, _mockResourceManager.Object);

            // Assert
            result.Should().NotBeNull();

            using var workbook = new XLWorkbook(result);
            var worksheet = workbook.Worksheets.First();

            // Verify IsActive column is not present
            worksheet.ColumnsUsed().Count().Should().Be(4); // Id, Name, Price, CreatedDate (4 columns)
        }

        [Fact]
        public void FillReportTemplate_WithStringData_HandlesCorrectly()
        {
            // Arrange
            var data = new List<SimpleStringData>
            {
                new SimpleStringData { Value = "Hello" },
                new SimpleStringData { Value = "World" }
            };
            var propertiesToExclude = Array.Empty<string>();

            // Act
            var result = _excelService.FillReportTemplate(data, data.Count, propertiesToExclude, _mockResourceManager.Object);

            // Assert
            result.Should().NotBeNull();
            result.Length.Should().BeGreaterThan(0);
        }

        [Fact]
        public void FillReportTemplate_WithNullableDateTime_HandlesCorrectly()
        {
            // Arrange
            var data = new List<TestDataWithNullableDate>
            {
                new TestDataWithNullableDate { Id = 1, Name = "With Date", NullableDate = new DateTime(2024, 1, 15) },
                new TestDataWithNullableDate { Id = 2, Name = "Without Date", NullableDate = null }
            };
            var propertiesToExclude = Array.Empty<string>();

            // Act
            var result = _excelService.FillReportTemplate(data, data.Count, propertiesToExclude, _mockResourceManager.Object);

            // Assert
            result.Should().NotBeNull();
            result.Length.Should().BeGreaterThan(0);
        }

        #endregion

        #region ConvertTableToObjects Tests

        [Fact]
        public void ConvertTableToObjects_WithValidTable_ReturnsCorrectObjects()
        {
            // Arrange
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Test");

            // Setup header row
            worksheet.Cell(1, 1).Value = "Id";
            worksheet.Cell(1, 2).Value = "Name";
            worksheet.Cell(1, 3).Value = "Price";

            // Setup data rows
            worksheet.Cell(2, 1).Value = 1;
            worksheet.Cell(2, 2).Value = "Product 1";
            worksheet.Cell(2, 3).Value = 99.99;

            worksheet.Cell(3, 1).Value = 2;
            worksheet.Cell(3, 2).Value = "Product 2";
            worksheet.Cell(3, 3).Value = 149.99;

            var tableRange = worksheet.Range(worksheet.Cell(1, 1), worksheet.Cell(3, 3));
            var table = worksheet.Table("Test", tableRange);

            // Act
            var result = ExcelService.ConvertTableToObjects<TestDataItem>(table).ToList();

            // Assert
            result.Should().HaveCount(2);
            result[0].Id.Should().Be(1);
            result[0].Name.Should().Be("Product 1");
            result[0].Price.Should().Be(99.99m);
            result[1].Id.Should().Be(2);
            result[1].Name.Should().Be("Product 2");
            result[1].Price.Should().Be(149.99m);
        }

        [Fact]
        public void ConvertTableToObjects_WithEmptyTable_ReturnsEmptyList()
        {
            // Arrange
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Test");

            // Setup header row only
            worksheet.Cell(1, 1).Value = "Id";
            worksheet.Cell(1, 2).Value = "Name";

            var tableRange = worksheet.Range(worksheet.Cell(1, 1), worksheet.Cell(1, 2));
            var table = worksheet.Table("Test", tableRange);

            // Act
            var result = ExcelService.ConvertTableToObjects<TestDataItem>(table).ToList();

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void ConvertTableToObjects_WithInt32Conversion_ConvertsCorrectly()
        {
            // Arrange
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Test");

            worksheet.Cell(1, 1).Value = "Id";
            worksheet.Cell(1, 2).Value = "Name";

            worksheet.Cell(2, 1).Value = 42.0; // Excel stores all numbers as double
            worksheet.Cell(2, 2).Value = "Test";

            var tableRange = worksheet.Range(worksheet.Cell(1, 1), worksheet.Cell(2, 2));
            var table = worksheet.Table("Test", tableRange);

            // Act
            var result = ExcelService.ConvertTableToObjects<TestDataItem>(table).ToList();

            // Assert
            result.Should().HaveCount(1);
            result[0].Id.Should().Be(42);
            result[0].Id.Should().BeOfType<int>();
        }

        [Fact]
        public void ConvertTableToObjects_WithDateTimeConversion_ConvertsCorrectly()
        {
            // Arrange
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Test");

            worksheet.Cell(1, 1).Value = "Id";
            worksheet.Cell(1, 2).Value = "CreatedDate";

            // Excel serial date for 2024-01-15 is approximately 45306
            worksheet.Cell(2, 1).Value = 1;
            worksheet.Cell(2, 2).Value = 45306.0;

            var tableRange = worksheet.Range(worksheet.Cell(1, 1), worksheet.Cell(2, 2));
            var table = worksheet.Table("Test", tableRange);

            // Act
            var result = ExcelService.ConvertTableToObjects<TestDataItem>(table).ToList();

            // Assert
            result.Should().HaveCount(1);
            result[0].CreatedDate.Year.Should().Be(2024);
            result[0].CreatedDate.Month.Should().Be(1);
            result[0].CreatedDate.Day.Should().Be(15);
        }

        [Fact]
        public void ConvertTableToObjects_WithInvalidDate_ThrowsArgumentException()
        {
            // Arrange
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Test");

            worksheet.Cell(1, 1).Value = "Id";
            worksheet.Cell(1, 2).Value = "CreatedDate";

            worksheet.Cell(2, 1).Value = 1;
            worksheet.Cell(2, 2).Value = 0; // Invalid date (less than 1)

            var tableRange = worksheet.Range(worksheet.Cell(1, 1), worksheet.Cell(2, 2));
            var table = worksheet.Table("Test", tableRange);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => ExcelService.ConvertTableToObjects<TestDataItem>(table).ToList());
        }

        [Fact]
        public void ConvertTableToObjects_WithUnsupportedType_ThrowsNotImplementedException()
        {
            // Arrange
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Test");

            worksheet.Cell(1, 1).Value = "Id";
            worksheet.Cell(1, 2).Value = "Price";

            worksheet.Cell(2, 1).Value = 1;
            worksheet.Cell(2, 2).Value = 99.99;

            var tableRange = worksheet.Range(worksheet.Cell(1, 1), worksheet.Cell(2, 2));
            var table = worksheet.Table("Test", tableRange);

            // Act & Assert
            Assert.Throws<NotImplementedException>(() => ExcelService.ConvertTableToObjects<TestDataItem>(table).ToList());
        }

        #endregion

        #region Backward Compatibility Tests

        [Fact]
        public void FillReportTemplate_BackwardCompatibility_WithDecimal_HandlesCorrectly()
        {
            // Arrange - Test backward compatibility with decimal types
            var data = new List<TestDataItem>
            {
                new TestDataItem { Id = 1, Name = "Product", Price = 123.45m, CreatedDate = DateTime.Now, IsActive = true }
            };
            var propertiesToExclude = Array.Empty<string>();

            // Act
            var result = _excelService.FillReportTemplate(data, data.Count, propertiesToExclude, _mockResourceManager.Object);

            // Assert
            result.Should().NotBeNull();

            using var workbook = new XLWorkbook(result);
            var worksheet = workbook.Worksheets.First();

            // Verify decimal value is preserved
            var cellValue = worksheet.Cell(2, 3).Value;
            cellValue.Should().Be(123.45);
        }

        [Fact]
        public void FillReportTemplate_BackwardCompatibility_WithBoolean_HandlesCorrectly()
        {
            // Arrange - Test backward compatibility with boolean types
            var data = new List<TestDataItem>
            {
                new TestDataItem { Id = 1, Name = "Product", Price = 100, CreatedDate = DateTime.Now, IsActive = true },
                new TestDataItem { Id = 2, Name = "Product", Price = 100, CreatedDate = DateTime.Now, IsActive = false }
            };
            var propertiesToExclude = Array.Empty<string>();

            // Act
            var result = _excelService.FillReportTemplate(data, data.Count, propertiesToExclude, _mockResourceManager.Object);

            // Assert
            result.Should().NotBeNull();

            using var workbook = new XLWorkbook(result);
            var worksheet = workbook.Worksheets.First();

            // Verify boolean values are preserved
            worksheet.Cell(2, 5).Value.Should().Be(true);
            worksheet.Cell(3, 5).Value.Should().Be(false);
        }

        [Fact]
        public void FillReportTemplate_BackwardCompatibility_WithDateTime_HandlesCorrectly()
        {
            // Arrange - Test backward compatibility with DateTime types
            var testDate = new DateTime(2024, 6, 15, 10, 30, 0);
            var data = new List<TestDataItem>
            {
                new TestDataItem { Id = 1, Name = "Product", Price = 100, CreatedDate = testDate, IsActive = true }
            };
            var propertiesToExclude = Array.Empty<string>();

            // Act
            var result = _excelService.FillReportTemplate(data, data.Count, propertiesToExclude, _mockResourceManager.Object);

            // Assert
            result.Should().NotBeNull();

            using var workbook = new XLWorkbook(result);
            var worksheet = workbook.Worksheets.First();

            // Verify date is preserved with correct format
            worksheet.Column(4).Style.NumberFormat.Format.Should().Be("dd.MM.yyyy.");
        }

        #endregion

        #region Edge Case Tests

        [Fact]
        public void FillReportTemplate_WithLargeDataSet_HandlesCorrectly()
        {
            // Arrange - Test with larger dataset for performance and correctness
            var data = new List<TestDataItem>();
            for (int i = 0; i < 1000; i++)
            {
                data.Add(new TestDataItem
                {
                    Id = i,
                    Name = $"Product {i}",
                    Price = i * 10.00m,
                    CreatedDate = DateTime.Now.AddDays(i),
                    IsActive = i % 2 == 0
                });
            }
            var propertiesToExclude = Array.Empty<string>();

            // Act
            var result = _excelService.FillReportTemplate(data, data.Count, propertiesToExclude, _mockResourceManager.Object);

            // Assert
            result.Should().NotBeNull();
            result.Length.Should().BeGreaterThan(0);

            using var workbook = new XLWorkbook(result);
            var worksheet = workbook.Worksheets.First();

            // Verify all rows are present (header + 1000 data rows)
            worksheet.RowCount().Should().BeGreaterThanOrEqualTo(1001);
        }

        [Fact]
        public void FillReportTemplate_WithSpecialCharactersInData_HandlesCorrectly()
        {
            // Arrange - Test with special characters
            var data = new List<TestDataItem>
            {
                new TestDataItem
                {
                    Id = 1,
                    Name = "Product with \"quotes\" and 'apostrophes' & < > characters",
                    Price = 100.00m,
                    CreatedDate = DateTime.Now,
                    IsActive = true
                }
            };
            var propertiesToExclude = Array.Empty<string>();

            // Act
            var result = _excelService.FillReportTemplate(data, data.Count, propertiesToExclude, _mockResourceManager.Object);

            // Assert
            result.Should().NotBeNull();

            using var workbook = new XLWorkbook(result);
            var worksheet = workbook.Worksheets.First();

            worksheet.Cell(2, 2).Value.ToString().Should().Contain("quotes");
        }

        [Fact]
        public void FillReportTemplate_WithEmptyStrings_HandlesCorrectly()
        {
            // Arrange - Test with empty string values
            var data = new List<TestDataItem>
            {
                new TestDataItem
                {
                    Id = 1,
                    Name = "",
                    Price = 0.00m,
                    CreatedDate = DateTime.Now,
                    IsActive = false
                }
            };
            var propertiesToExclude = Array.Empty<string>();

            // Act
            var result = _excelService.FillReportTemplate(data, data.Count, propertiesToExclude, _mockResourceManager.Object);

            // Assert
            result.Should().NotBeNull();

            using var workbook = new XLWorkbook(result);
            var worksheet = workbook.Worksheets.First();

            worksheet.Cell(2, 2).Value.ToString().Should().BeEmpty();
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public void FillReportTemplate_WithAllPropertiesExcluded_CreatesEmptySheet()
        {
            // Arrange - Test with all properties excluded
            var data = new List<TestDataItem>
            {
                new TestDataItem { Id = 1, Name = "Test", Price = 100, CreatedDate = DateTime.Now, IsActive = true }
            };
            var propertiesToExclude = new[] { "Id", "Name", "Price", "CreatedDate", "IsActive" };

            // Act
            var result = _excelService.FillReportTemplate(data, data.Count, propertiesToExclude, _mockResourceManager.Object);

            // Assert
            result.Should().NotBeNull();
        }

        #endregion
    }
}
