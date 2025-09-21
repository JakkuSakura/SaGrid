using Tests.TestData;
using Tests.Contracts;

namespace Tests.Features;

/// <summary>
/// Tests for column filtering functionality
/// These tests define the expected behavior for column filtering features that need to be implemented
/// </summary>
public class ColumnFilteringTests : PersonContractTestBase
{
    [Fact]
    public void Table_Should_Support_Individual_Column_Filters()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        
        // Act - Filter by department
        table.SetState(new TableState<TestPerson>
        {
            ColumnFilters = new ColumnFiltersState(new List<ColumnFilter>
            {
                new ColumnFilter("department", "Engineering")
            })
        });
        
        var filteredRows = table.RowModel.Rows.ToList();
        
        // Assert
        filteredRows.Should().NotBeEmpty("Filtering should return matching rows");
        filteredRows.Should().AllSatisfy(row => 
            row.Original.Department.Should().Be("Engineering", "All filtered rows should match the filter"));
    }

    [Fact]
    public void Table_Should_Support_Multiple_Column_Filters()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        
        // Act - Filter by department AND age range
        table.SetState(new TableState<TestPerson>
        {
            ColumnFilters = new ColumnFiltersState(new List<ColumnFilter>
            {
                new ColumnFilter("department", "Engineering"),
                new ColumnFilter("age", 30) // Assuming exact match for now
            })
        });
        
        var filteredRows = table.RowModel.Rows.ToList();
        
        // Assert
        filteredRows.Should().AllSatisfy(row =>
        {
            row.Original.Department.Should().Be("Engineering");
            row.Original.Age.Should().Be(30);
        });
    }

    [Fact]
    public void Table_Should_Support_String_Contains_Filtering()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        
        // Act - Filter by partial name match
        table.SetState(new TableState<TestPerson>
        {
            ColumnFilters = new ColumnFiltersState(new List<ColumnFilter>
            {
                new ColumnFilter("firstName", "Jo") // Should match John, Joan, etc.
            })
        });
        
        var filteredRows = table.RowModel.Rows.ToList();
        
        // Assert
        filteredRows.Should().NotBeEmpty("Should find names containing 'Jo'");
        filteredRows.Should().AllSatisfy(row =>
            row.Original.FirstName.Should().Contain("Jo", "All results should contain the filter text"));
    }

    [Fact]
    public void Table_Should_Support_Case_Insensitive_Filtering()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        
        // Act - Filter with different case
        table.SetState(new TableState<TestPerson>
        {
            ColumnFilters = new ColumnFiltersState(new List<ColumnFilter>
            {
                new ColumnFilter("department", "engineering") // lowercase
            })
        });
        
        var filteredRows = table.RowModel.Rows.ToList();
        
        // Assert
        filteredRows.Should().NotBeEmpty("Case insensitive filtering should work");
        filteredRows.Should().AllSatisfy(row =>
            row.Original.Department.Should().BeEquivalentTo("Engineering", "Should match regardless of case"));
    }

    [Fact]
    public void Table_Should_Support_Numeric_Range_Filtering()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        
        // Act - Filter by age range (assuming range object or tuple)
        table.SetState(new TableState<TestPerson>
        {
            ColumnFilters = new ColumnFiltersState(new List<ColumnFilter>
            {
                new ColumnFilter("age", new { min = 25, max = 35 }) // Age between 25-35
            })
        });
        
        var filteredRows = table.RowModel.Rows.ToList();
        
        // Assert
        filteredRows.Should().NotBeEmpty("Should find people in age range");
        filteredRows.Should().AllSatisfy(row =>
        {
            row.Original.Age.Should().BeGreaterOrEqualTo(25, "Age should be >= 25");
            row.Original.Age.Should().BeLessOrEqualTo(35, "Age should be <= 35");
        });
    }

    [Fact]
    public void Table_Should_Support_Boolean_Filtering()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        
        // Act - Filter by active status
        table.SetState(new TableState<TestPerson>
        {
            ColumnFilters = new ColumnFiltersState(new List<ColumnFilter>
            {
                new ColumnFilter("isActive", true)
            })
        });
        
        var filteredRows = table.RowModel.Rows.ToList();
        
        // Assert
        filteredRows.Should().NotBeEmpty("Should find active users");
        filteredRows.Should().AllSatisfy(row =>
            row.Original.IsActive.Should().BeTrue("All filtered rows should be active"));
    }

    [Fact]
    public void Table_Should_Clear_Individual_Column_Filters()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        
        // Set initial filters
        table.SetState(new TableState<TestPerson>
        {
            ColumnFilters = new ColumnFiltersState(new List<ColumnFilter>
            {
                new ColumnFilter("department", "Engineering"),
                new ColumnFilter("age", 30)
            })
        });
        
        var filteredCount = table.RowModel.Rows.Count;
        
        // Act - Clear one filter using column API
        var departmentColumn = table.GetColumn("department");
        departmentColumn!.SetFilterValue(null);
        
        var afterClearCount = table.RowModel.Rows.Count;
        
        // Assert
        afterClearCount.Should().BeGreaterThan(filteredCount, 
            "Clearing a filter should increase the number of visible rows");
        
        // Age filter should still be active
        table.State.ColumnFilters!.Filters.Should().ContainSingle(f => f.Id == "age");
        table.State.ColumnFilters!.Filters.Should().NotContain(f => f.Id == "department");
    }

    [Fact]
    public void Table_Should_Clear_All_Column_Filters()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        
        // Set multiple filters
        table.SetState(new TableState<TestPerson>
        {
            ColumnFilters = new ColumnFiltersState(new List<ColumnFilter>
            {
                new ColumnFilter("department", "Engineering"),
                new ColumnFilter("age", 30),
                new ColumnFilter("isActive", true)
            })
        });
        
        var originalRowCount = PersonTestData.MediumDataset.Count;
        var filteredCount = table.RowModel.Rows.Count;
        
        // Act
        table.ResetColumnFilters();
        
        var afterResetCount = table.RowModel.Rows.Count;
        
        // Assert
        afterResetCount.Should().Be(originalRowCount, "All rows should be visible after reset");
        table.State.ColumnFilters.Should().BeNull("Column filters state should be cleared");
    }

    [Fact]
    public void Table_Should_Update_Filter_State_Through_Column_API()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        var departmentColumn = table.GetColumn("department");
        
        departmentColumn.Should().NotBeNull();
        
        // Act
        departmentColumn!.SetFilterValue("Marketing");
        
        // Assert
        departmentColumn.IsFiltered.Should().BeTrue("Column should show as filtered");
        departmentColumn.FilterValue.Should().Be("Marketing");
        
        var filteredRows = table.RowModel.Rows.ToList();
        filteredRows.Should().AllSatisfy(row =>
            row.Original.Department.Should().Be("Marketing"));
    }

    [Fact]
    public void Table_Should_Handle_Empty_Filter_Values()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        var originalCount = table.RowModel.Rows.Count;
        
        // Act - Set empty string filter
        table.SetState(new TableState<TestPerson>
        {
            ColumnFilters = new ColumnFiltersState(new List<ColumnFilter>
            {
                new ColumnFilter("firstName", "")
            })
        });
        
        var afterEmptyFilterCount = table.RowModel.Rows.Count;
        
        // Assert
        afterEmptyFilterCount.Should().Be(originalCount, 
            "Empty string filter should not reduce row count");
    }

    [Fact]
    public void Table_Should_Handle_Null_Filter_Values()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        var originalCount = table.RowModel.Rows.Count;
        
        // Act - Set null filter (should be same as not having filter)
        table.SetState(new TableState<TestPerson>
        {
            ColumnFilters = new ColumnFiltersState(new List<ColumnFilter>
            {
                new ColumnFilter("firstName", null)
            })
        });
        
        var afterNullFilterCount = table.RowModel.Rows.Count;
        
        // Assert
        afterNullFilterCount.Should().Be(originalCount,
            "Null filter should not reduce row count");
    }

    [Fact]
    public void Table_Should_Support_Custom_Filter_Functions()
    {
        // Arrange - This test assumes we can define custom filter functions in column definitions
        var customColumns = new ColumnDef<TestPerson>[]
        {
            ColumnHelper.Accessor<TestPerson, string>(p => p.FirstName, "firstName", "First Name"),
            ColumnHelper.Accessor<TestPerson, int>(p => p.Age, "age", "Age")
            // Custom filter function would be defined here in the real implementation
        };

        var options = new TableOptions<TestPerson>
        {
            Data = PersonTestData.MediumDataset,
            Columns = customColumns,
            EnableColumnFilters = true
        };

        var table = new Table<TestPerson>(options);
        
        // Act - Apply custom filter
        table.SetState(new TableState<TestPerson>
        {
            ColumnFilters = new ColumnFiltersState(new List<ColumnFilter>
            {
                new ColumnFilter("age", "adult") // Custom filter: "adult" means age >= 18
            })
        });
        
        var filteredRows = table.RowModel.Rows.ToList();
        
        // Assert
        filteredRows.Should().NotBeEmpty("Custom filter should work");
        filteredRows.Should().AllSatisfy(row =>
            row.Original.Age.Should().BeGreaterOrEqualTo(18, "Custom 'adult' filter should show only adults"));
    }

    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(500)]
    public void Column_Filtering_Should_Be_Performant(int dataSize)
    {
        // Arrange
        var largeData = PersonTestData.GenerateLargeDataset(dataSize).ToList();
        var table = CreateTableWithData(largeData);
        
        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        table.SetState(new TableState<TestPerson>
        {
            ColumnFilters = new ColumnFiltersState(new List<ColumnFilter>
            {
                new ColumnFilter("department", "Engineering"),
                new ColumnFilter("isActive", true)
            })
        });
        
        // Force evaluation
        var result = table.RowModel.Rows.ToList();
        
        stopwatch.Stop();
        
        // Assert
        result.Should().NotBeEmpty("Should find matching records");
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(50,
            $"Column filtering of {dataSize} rows should complete within 50ms");
    }

    [Fact]
    public void Table_Should_Combine_Column_Filters_With_Sorting()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        
        // Act - Apply both filtering and sorting
        table.SetState(new TableState<TestPerson>
        {
            ColumnFilters = new ColumnFiltersState(new List<ColumnFilter>
            {
                new ColumnFilter("department", "Engineering")
            }),
            Sorting = new SortingState(new List<ColumnSort>
            {
                new ColumnSort("age", SortDirection.Ascending)
            })
        });
        
        var result = table.RowModel.Rows.ToList();
        
        // Assert
        result.Should().NotBeEmpty("Should have filtered and sorted results");
        result.Should().AllSatisfy(row =>
            row.Original.Department.Should().Be("Engineering", "All rows should match filter"));
        
        var ages = result.Select(r => r.Original.Age).ToList();
        ages.Should().BeInAscendingOrder("Results should be sorted by age");
    }

    [Fact]
    public void Table_Should_Handle_Filter_Changes_Efficiently()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        
        // Act - Apply multiple filter changes
        table.SetState(new TableState<TestPerson>
        {
            ColumnFilters = new ColumnFiltersState(new List<ColumnFilter>
            {
                new ColumnFilter("department", "Engineering")
            })
        });
        
        var engineeringCount = table.RowModel.Rows.Count;
        
        // Change filter
        table.SetState(new TableState<TestPerson>
        {
            ColumnFilters = new ColumnFiltersState(new List<ColumnFilter>
            {
                new ColumnFilter("department", "Marketing")
            })
        });
        
        var marketingCount = table.RowModel.Rows.Count;
        
        // Assert
        engineeringCount.Should().BeGreaterThan(0, "Should find engineering records");
        marketingCount.Should().BeGreaterThan(0, "Should find marketing records");
        engineeringCount.Should().NotBe(marketingCount, "Different filters should yield different counts");
    }
}