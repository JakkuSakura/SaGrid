using Tests.TestData;
using Tests.Contracts;

namespace Tests.Features;

/// <summary>
/// Tests for global filtering functionality
/// These tests define the expected behavior for global filtering features that need to be implemented
/// </summary>
public class GlobalFilterTests : PersonContractTestBase
{
    [Fact]
    public void Table_Should_Support_Global_Text_Filter()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        
        // Act - Apply global filter
        table.SetGlobalFilter("John");
        
        var filteredRows = table.RowModel.Rows.ToList();
        
        // Assert
        filteredRows.Should().NotBeEmpty("Should find matching records");
        filteredRows.Should().AllSatisfy(row => 
        {
            var person = row.Original;
            var matchesFilter = person.FirstName.Contains("John", StringComparison.OrdinalIgnoreCase) ||
                               person.LastName.Contains("John", StringComparison.OrdinalIgnoreCase) ||
                               person.Department.Contains("John", StringComparison.OrdinalIgnoreCase) ||
                               person.Email.Contains("John", StringComparison.OrdinalIgnoreCase);
            
            matchesFilter.Should().BeTrue("Row should match global filter in any searchable field");
        });
    }

    [Fact]
    public void Table_Should_Support_Case_Insensitive_Global_Filter()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        
        // Act - Apply lowercase global filter
        table.SetGlobalFilter("engineering");
        
        var filteredRows = table.RowModel.Rows.ToList();
        
        // Assert
        filteredRows.Should().NotBeEmpty("Should find matches regardless of case");
        filteredRows.Should().AllSatisfy(row =>
            row.Original.Department.Should().BeEquivalentTo("Engineering", "Should match case-insensitively"));
    }

    [Fact]
    public void Table_Should_Clear_Global_Filter()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        var originalRowCount = table.RowModel.Rows.Count;
        
        // Apply filter
        table.SetGlobalFilter("Engineering");
        var filteredCount = table.RowModel.Rows.Count;
        filteredCount.Should().BeLessThan(originalRowCount, "Filter should reduce row count");
        
        // Act - Clear filter
        table.SetGlobalFilter("");
        
        // Assert
        table.RowModel.Rows.Should().HaveCount(originalRowCount, "Clearing filter should restore all rows");
        table.State.GlobalFilter.Should().BeNull("Global filter state should be cleared");
    }

    [Fact]
    public void Table_Should_Reset_Global_Filter()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        table.SetGlobalFilter("test");
        
        table.State.GlobalFilter.Should().NotBeNull();
        
        // Act
        table.ResetGlobalFilter();
        
        // Assert
        table.State.GlobalFilter.Should().BeNull("Global filter should be reset");
        table.RowModel.Rows.Should().HaveCount(PersonTestData.MediumDataset.Count, 
            "All rows should be visible after reset");
    }

    [Fact]
    public void Table_Should_Handle_Empty_Global_Filter()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        var originalCount = table.RowModel.Rows.Count;
        
        // Act - Set empty filter
        table.SetGlobalFilter("");
        
        // Assert
        table.RowModel.Rows.Should().HaveCount(originalCount, "Empty filter should show all rows");
    }

    [Fact]
    public void Table_Should_Handle_Null_Global_Filter()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        var originalCount = table.RowModel.Rows.Count;
        
        // Act - Set null filter
        table.SetGlobalFilter(null);
        
        // Assert
        table.RowModel.Rows.Should().HaveCount(originalCount, "Null filter should show all rows");
        table.State.GlobalFilter.Should().BeNull();
    }

    [Fact]
    public void Table_Should_Support_Numeric_Global_Filter()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        
        // Act - Search for age
        table.SetGlobalFilter("25");
        
        var filteredRows = table.RowModel.Rows.ToList();
        
        // Assert
        filteredRows.Should().NotBeEmpty("Should find records with age 25");
        filteredRows.Should().AllSatisfy(row =>
        {
            var person = row.Original;
            var matchesAge = person.Age.ToString().Contains("25");
            var matchesOtherFields = person.FirstName.Contains("25", StringComparison.OrdinalIgnoreCase) ||
                                   person.LastName.Contains("25", StringComparison.OrdinalIgnoreCase) ||
                                   person.Department.Contains("25", StringComparison.OrdinalIgnoreCase) ||
                                   person.Email.Contains("25", StringComparison.OrdinalIgnoreCase);
            
            (matchesAge || matchesOtherFields).Should().BeTrue("Should match numeric or text fields");
        });
    }

    [Fact]
    public void Table_Should_Combine_Global_Filter_With_Column_Filters()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        
        // Act - Apply both global and column filters
        table.SetGlobalFilter("John");
        table.SetState(new TableState<TestPerson>
        {
            GlobalFilter = new GlobalFilterState("John"),
            ColumnFilters = new ColumnFiltersState(new List<ColumnFilter>
            {
                new ColumnFilter("department", "Engineering")
            })
        });
        
        var filteredRows = table.RowModel.Rows.ToList();
        
        // Assert
        filteredRows.Should().NotBeEmpty("Should find records matching both filters");
        filteredRows.Should().AllSatisfy(row =>
        {
            // Must match column filter
            row.Original.Department.Should().Be("Engineering");
            
            // Must match global filter
            var person = row.Original;
            var matchesGlobal = person.FirstName.Contains("John", StringComparison.OrdinalIgnoreCase) ||
                               person.LastName.Contains("John", StringComparison.OrdinalIgnoreCase) ||
                               person.Department.Contains("John", StringComparison.OrdinalIgnoreCase) ||
                               person.Email.Contains("John", StringComparison.OrdinalIgnoreCase);
            matchesGlobal.Should().BeTrue("Should match global filter");
        });
    }

    [Fact]
    public void Table_Should_Combine_Global_Filter_With_Sorting()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        
        // Act - Apply global filter and sorting
        table.SetState(new TableState<TestPerson>
        {
            GlobalFilter = new GlobalFilterState("Engineering"),
            Sorting = new SortingState(new List<ColumnSort>
            {
                new ColumnSort("firstName", SortDirection.Ascending)
            })
        });
        
        var filteredSortedRows = table.RowModel.Rows.ToList();
        
        // Assert
        filteredSortedRows.Should().NotBeEmpty("Should have filtered results");
        
        // Check filtering
        filteredSortedRows.Should().AllSatisfy(row =>
            row.Original.Department.Should().Be("Engineering", "Should match global filter"));
        
        // Check sorting
        var names = filteredSortedRows.Select(r => r.Original.FirstName).ToList();
        names.Should().BeInAscendingOrder("Results should be sorted by first name");
    }

    [Fact]
    public void Table_Should_Support_Custom_Global_Filter_Function()
    {
        // Arrange - This test assumes we can define custom global filter functions
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        
        // Act - Apply custom global filter (e.g., "senior" means age >= 40)
        table.SetGlobalFilter("senior");
        
        var filteredRows = table.RowModel.Rows.ToList();
        
        // Assert
        // In a real implementation with custom filter functions, this might filter by age
        // For now, just ensure it doesn't crash and returns results
        Action filterAction = () => table.SetGlobalFilter("senior");
        filterAction.Should().NotThrow("Custom global filter should be supported");
    }

    [Fact]
    public void Table_Should_Handle_Special_Characters_In_Global_Filter()
    {
        // Arrange
        var specialData = new[]
        {
            new TestPerson(1, "O'Connor", "Smith", 30, "oconnor@test.com", "Engineering", true),
            new TestPerson(2, "José", "García", 25, "jose@test.com", "Marketing", true),
            new TestPerson(3, "Jean-Luc", "Picard", 35, "picard@test.com", "Management", true)
        };
        
        var table = CreateTableWithData(specialData);
        
        // Act & Assert - Test various special characters
        table.SetGlobalFilter("O'Connor");
        table.RowModel.Rows.Should().ContainSingle("Should handle apostrophes");
        
        table.SetGlobalFilter("José");
        table.RowModel.Rows.Should().ContainSingle("Should handle accented characters");
        
        table.SetGlobalFilter("Jean-Luc");
        table.RowModel.Rows.Should().ContainSingle("Should handle hyphens");
    }

    [Theory]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1000)]
    public void Global_Filter_Should_Be_Performant(int dataSize)
    {
        // Arrange
        var largeData = PersonTestData.GenerateLargeDataset(dataSize).ToList();
        var table = CreateTableWithData(largeData);
        
        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        table.SetGlobalFilter("Engineering");
        
        // Force evaluation
        var result = table.RowModel.Rows.ToList();
        
        stopwatch.Stop();
        
        // Assert
        result.Should().NotBeEmpty("Should find matching records");
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100,
            $"Global filtering of {dataSize} rows should complete within 100ms");
    }

    [Fact]
    public void Table_Should_Update_Filter_State_Correctly()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        
        // Act
        table.SetGlobalFilter("test search");
        
        // Assert
        table.State.GlobalFilter.Should().NotBeNull("Global filter state should be set");
        table.State.GlobalFilter!.Value.Should().Be("test search");
        
        // Act - Change filter
        table.SetGlobalFilter("new search");
        
        // Assert
        table.State.GlobalFilter!.Value.Should().Be("new search", "Filter state should be updated");
    }

    [Fact]
    public void Table_Should_Handle_Whitespace_In_Global_Filter()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        var originalCount = table.RowModel.Rows.Count;
        
        // Act & Assert - Test whitespace handling
        table.SetGlobalFilter("   ");
        table.RowModel.Rows.Should().HaveCount(originalCount, "Whitespace-only filter should show all rows");
        
        table.SetGlobalFilter(" Engineering ");
        var trimmedResults = table.RowModel.Rows.Count;
        
        table.SetGlobalFilter("Engineering");
        var noSpaceResults = table.RowModel.Rows.Count;
        
        trimmedResults.Should().Be(noSpaceResults, "Leading/trailing spaces should be handled appropriately");
    }

    [Fact]
    public void Table_Should_Support_Min_Filter_Length()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        var originalCount = table.RowModel.Rows.Count;
        
        // Act - Very short filter (might have minimum length requirement)
        table.SetGlobalFilter("a");
        
        // Assert
        // Implementation might require minimum filter length for performance
        // This test ensures the behavior is consistent
        Action shortFilter = () => table.SetGlobalFilter("a");
        shortFilter.Should().NotThrow("Short filters should be handled gracefully");
    }

    [Fact]
    public void Table_Should_Handle_Global_Filter_With_Pagination()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        
        // Act - Apply global filter and pagination
        table.SetState(new TableState<TestPerson>
        {
            GlobalFilter = new GlobalFilterState("Engineering"),
            Pagination = new PaginationState { PageIndex = 0, PageSize = 5 }
        });
        
        var result = table.RowModel.Rows.ToList();
        
        // Assert
        result.Should().HaveCountLessOrEqualTo(5, "Should respect page size");
        result.Should().AllSatisfy(row =>
            row.Original.Department.Should().Be("Engineering", "Should respect global filter"));
        
        // Page count should be based on filtered data
        var engineeringCount = PersonTestData.MediumDataset.Count(p => p.Department == "Engineering");
        var expectedPageCount = (int)Math.Ceiling((double)engineeringCount / 5);
        table.GetPageCount().Should().Be(expectedPageCount, "Page count should reflect filtered results");
    }
}