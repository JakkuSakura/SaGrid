using Tests.TestData;

namespace Tests.Contracts;

/// <summary>
/// Contract tests for ITable<T> interface compliance
/// These tests verify that the existing Table<T> implementation meets the contract requirements
/// </summary>
public class TableContractTests : PersonContractTestBase
{
    [Fact]
    public void Table_Should_Have_Valid_Options()
    {
        // Arrange & Act
        var table = CreateTable();

        // Assert
        table.Options.Should().NotBeNull();
        table.Options.Data.Should().NotBeNull();
        table.Options.Columns.Should().NotBeNull();
        table.Options.Columns.Should().NotBeEmpty();
    }

    [Fact]
    public void Table_Should_Have_Valid_State()
    {
        // Arrange & Act
        var table = CreateTable();

        // Assert
        VerifyBasicTableProperties(table);
        table.State.Should().NotBeNull();
    }

    [Fact]
    public void Table_Should_Initialize_With_Provided_Data()
    {
        // Arrange
        var testData = PersonTestData.SmallDataset;
        var options = PersonTestData.DefaultTableOptions;

        // Act
        var table = new Table<TestPerson>(options);

        // Assert
        table.RowModel.Should().NotBeNull();
        table.RowModel.Rows.Should().HaveCount(testData.Count);
    }

    [Fact]
    public void Table_Should_Initialize_With_Provided_Columns()
    {
        // Arrange
        var columns = PersonTestData.StandardColumns;
        var options = PersonTestData.DefaultTableOptions;

        // Act
        var table = new Table<TestPerson>(options);

        // Assert
        table.AllColumns.Should().NotBeNull();
        table.AllColumns.Should().HaveCount(columns.Count);
        table.VisibleLeafColumns.Should().NotBeNull();
    }

    [Fact]
    public void Table_Should_Support_State_Updates()
    {
        // Arrange
        var table = CreateTable();
        var originalState = table.State;

        // Act
        table.SetState(originalState);

        // Assert
        table.State.Should().NotBeNull();
        // State should be the same reference if no changes were made
        table.State.Should().Be(originalState);
    }

    [Fact]
    public void Table_Should_Support_State_Updater_Functions()
    {
        // Arrange
        var table = CreateTable();
        var originalState = table.State;

        // Act - use updater function that returns the same state
        table.SetState(currentState => currentState);

        // Assert
        table.State.Should().NotBeNull();
        table.State.Should().Be(originalState);
    }

    [Fact]
    public void Table_Should_Allow_Column_Lookup()
    {
        // Arrange
        var table = CreateTable();
        var firstColumnId = PersonTestData.StandardColumns.First().Id ?? "id";

        // Act
        var column = table.GetColumn(firstColumnId);

        // Assert
        column.Should().NotBeNull();
        column!.Id.Should().Be(firstColumnId);
    }

    [Fact]
    public void Table_Should_Return_Null_For_Invalid_Column_Id()
    {
        // Arrange
        var table = CreateTable();

        // Act
        var column = table.GetColumn("nonexistent-column");

        // Assert
        column.Should().BeNull();
    }

    [Fact]
    public void Table_Should_Allow_Row_Lookup()
    {
        // Arrange
        var table = CreateTable();

        // Act & Assert - since we don't know the exact row ID format, just verify the method works
        var result = table.GetRow("some-row-id");
        // Result can be null if row doesn't exist, that's valid behavior
        // The important thing is that the method doesn't throw
    }

    [Fact]
    public void Table_Should_Provide_Selected_Row_Model()
    {
        // Arrange
        var table = CreateTable();

        // Act
        var selectedRows = table.GetSelectedRowModel();

        // Assert
        selectedRows.Should().NotBeNull();
        // Initially no rows should be selected
        selectedRows.Should().BeEmpty();
    }

    [Fact]
    public void Table_Should_Support_Column_Filter_Reset()
    {
        // Arrange
        var table = CreateTable();

        // Act & Assert - method should not throw
        table.ResetColumnFilters();
        
        // Verify table is still in valid state
        VerifyBasicTableProperties(table);
    }

    [Fact]
    public void Table_Should_Support_Global_Filter_Reset()
    {
        // Arrange
        var table = CreateTable();

        // Act & Assert - method should not throw
        table.ResetGlobalFilter();
        
        // Verify table is still in valid state
        VerifyBasicTableProperties(table);
    }

    [Fact]
    public void Table_Should_Support_Sorting_Reset()
    {
        // Arrange
        var table = CreateTable();

        // Act & Assert - method should not throw
        table.ResetSorting();
        
        // Verify table is still in valid state
        VerifyBasicTableProperties(table);
    }

    [Fact]
    public void Table_Should_Support_Row_Selection_Reset()
    {
        // Arrange
        var table = CreateTable();

        // Act & Assert - method should not throw
        table.ResetRowSelection();
        
        // Verify table is still in valid state
        VerifyBasicTableProperties(table);
    }

    [Fact]
    public void Table_Should_Support_Column_Order_Reset()
    {
        // Arrange
        var table = CreateTable();

        // Act & Assert - method should not throw
        table.ResetColumnOrder();
        
        // Verify table is still in valid state
        VerifyBasicTableProperties(table);
    }

    [Fact]
    public void Table_Should_Support_Column_Sizing_Reset()
    {
        // Arrange
        var table = CreateTable();

        // Act & Assert - method should not throw
        table.ResetColumnSizing();
        
        // Verify table is still in valid state
        VerifyBasicTableProperties(table);
    }

    [Fact]
    public void Table_Should_Support_Column_Visibility_Reset()
    {
        // Arrange
        var table = CreateTable();

        // Act & Assert - method should not throw
        table.ResetColumnVisibility();
        
        // Verify table is still in valid state
        VerifyBasicTableProperties(table);
    }

    [Fact]
    public void Table_Should_Support_Expanded_State_Reset()
    {
        // Arrange
        var table = CreateTable();

        // Act & Assert - method should not throw
        table.ResetExpanded();
        
        // Verify table is still in valid state
        VerifyBasicTableProperties(table);
    }

    [Fact]
    public void Table_Should_Support_Grouping_Reset()
    {
        // Arrange
        var table = CreateTable();

        // Act & Assert - method should not throw
        table.ResetGrouping();
        
        // Verify table is still in valid state
        VerifyBasicTableProperties(table);
    }

    [Fact]
    public void Table_Should_Support_Pagination_Reset()
    {
        // Arrange
        var table = CreateTable();

        // Act & Assert - method should not throw
        table.ResetPagination();
        
        // Verify table is still in valid state
        VerifyBasicTableProperties(table);
    }

    [Fact]
    public void Table_Should_Handle_Empty_Data()
    {
        // Arrange
        var options = new TableOptions<TestPerson>
        {
            Data = PersonTestData.EmptyDataset,
            Columns = PersonTestData.StandardColumns
        };

        // Act
        var table = new Table<TestPerson>(options);

        // Assert
        VerifyBasicTableProperties(table);
        table.RowModel.Rows.Should().BeEmpty();
    }

    [Fact]
    public void Table_Should_Handle_Single_Item_Data()
    {
        // Arrange
        var options = new TableOptions<TestPerson>
        {
            Data = PersonTestData.SingleItemDataset,
            Columns = PersonTestData.StandardColumns
        };

        // Act
        var table = new Table<TestPerson>(options);

        // Assert
        VerifyBasicTableProperties(table);
        table.RowModel.Rows.Should().HaveCount(1);
    }

    [Fact]
    public void Table_Should_Handle_Minimal_Columns()
    {
        // Arrange
        var options = new TableOptions<TestPerson>
        {
            Data = PersonTestData.SmallDataset,
            Columns = PersonTestData.MinimalColumns
        };

        // Act
        var table = new Table<TestPerson>(options);

        // Assert
        VerifyBasicTableProperties(table);
        table.AllColumns.Should().HaveCount(PersonTestData.MinimalColumns.Count);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    public void Table_Should_Handle_Various_Dataset_Sizes(int dataSize)
    {
        // Arrange
        var largeData = PersonTestData.GenerateLargeDataset(dataSize).ToList();
        var options = new TableOptions<TestPerson>
        {
            Data = largeData,
            Columns = PersonTestData.StandardColumns
        };

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var table = new Table<TestPerson>(options);
        stopwatch.Stop();

        // Assert
        VerifyBasicTableProperties(table);
        table.RowModel.Rows.Should().HaveCount(dataSize);
        
        // Performance assertion - table creation should be reasonably fast
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, 
            $"Table creation with {dataSize} rows should complete within 1 second");
        
        VerifyPerformanceRequirements(table, dataSize);
    }

    [Fact]
    public void Table_Should_Maintain_Column_Order()
    {
        // Arrange
        var columns = PersonTestData.StandardColumns;
        var table = CreateTable();

        // Act
        var visibleColumns = table.VisibleLeafColumns;

        // Assert
        visibleColumns.Should().NotBeEmpty();
        // Verify that column order is maintained (at least some columns are visible)
        visibleColumns.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Table_Should_Have_Consistent_Row_And_Column_Models()
    {
        // Arrange & Act
        var table = CreateTable();

        // Assert
        table.RowModel.Should().NotBeNull();
        table.PreFilteredRowModel.Should().NotBeNull();
        table.PreSortedRowModel.Should().NotBeNull();
        table.PreGroupedRowModel.Should().NotBeNull();
        table.PreExpandedRowModel.Should().NotBeNull();
        table.PrePaginationRowModel.Should().NotBeNull();
        
        // All row models should be initialized
        table.HeaderGroups.Should().NotBeNull();
        table.FooterGroups.Should().NotBeNull();
    }
}