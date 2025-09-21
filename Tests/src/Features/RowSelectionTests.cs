using Tests.TestData;
using Tests.Contracts;

namespace Tests.Features;

/// <summary>
/// Tests for row selection functionality
/// These tests define the expected behavior for row selection features that need to be implemented
/// </summary>
public class RowSelectionTests : PersonContractTestBase
{
    [Fact]
    public void Table_Should_Support_Single_Row_Selection()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.SmallDataset);
        var firstRow = table.RowModel.Rows.First();
        
        // Act
        firstRow.ToggleSelected();
        
        // Assert
        firstRow.IsSelected.Should().BeTrue("Row should be selected after toggle");
        
        var selectedRows = table.GetSelectedRowModel();
        selectedRows.Should().ContainSingle("Should have exactly one selected row");
        selectedRows.First().Should().Be(firstRow);
    }

    [Fact]
    public void Table_Should_Support_Multiple_Row_Selection()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.SmallDataset);
        var rows = table.RowModel.Rows.Take(3).ToList();
        
        // Act - Select multiple rows
        foreach (var row in rows)
        {
            row.ToggleSelected();
        }
        
        // Assert
        rows.Should().AllSatisfy(row => row.IsSelected.Should().BeTrue());
        
        var selectedRows = table.GetSelectedRowModel();
        selectedRows.Should().HaveCount(3, "Should have three selected rows");
        selectedRows.Should().BeEquivalentTo(rows);
    }

    [Fact]
    public void Table_Should_Deselect_Row_On_Second_Toggle()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.SmallDataset);
        var row = table.RowModel.Rows.First();
        
        // Act - Select then deselect
        row.ToggleSelected();
        row.IsSelected.Should().BeTrue();
        
        row.ToggleSelected();
        
        // Assert
        row.IsSelected.Should().BeFalse("Row should be deselected after second toggle");
        table.GetSelectedRowModel().Should().BeEmpty("No rows should be selected");
    }

    [Fact]
    public void Table_Should_Support_Select_All_Rows()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.SmallDataset);
        var allRows = table.RowModel.Rows.ToList();
        
        // Act
        table.SelectAllRows();
        
        // Assert
        allRows.Should().AllSatisfy(row => row.IsSelected.Should().BeTrue());
        table.GetSelectedRowModel().Should().HaveCount(allRows.Count);
        table.GetIsAllRowsSelected().Should().BeTrue();
    }

    [Fact]
    public void Table_Should_Support_Deselect_All_Rows()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.SmallDataset);
        
        // Select all rows first
        table.SelectAllRows();
        table.GetSelectedRowModel().Should().NotBeEmpty();
        
        // Act
        table.DeselectAllRows();
        
        // Assert
        table.RowModel.Rows.Should().AllSatisfy(row => row.IsSelected.Should().BeFalse());
        table.GetSelectedRowModel().Should().BeEmpty();
        table.GetIsAllRowsSelected().Should().BeFalse();
    }

    [Fact]
    public void Table_Should_Support_Toggle_All_Rows()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.SmallDataset);
        
        // Act - First toggle (select all)
        table.ToggleAllRowsSelected();
        
        // Assert
        table.GetIsAllRowsSelected().Should().BeTrue();
        table.GetSelectedRowModel().Should().HaveCount(PersonTestData.SmallDataset.Count);
        
        // Act - Second toggle (deselect all)
        table.ToggleAllRowsSelected();
        
        // Assert
        table.GetIsAllRowsSelected().Should().BeFalse();
        table.GetSelectedRowModel().Should().BeEmpty();
    }

    [Fact]
    public void Table_Should_Handle_Partial_Selection_State()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.SmallDataset);
        var rows = table.RowModel.Rows.ToList();
        
        // Act - Select only some rows
        rows.Take(2).ToList().ForEach(row => row.ToggleSelected());
        
        // Assert
        table.GetIsAllRowsSelected().Should().BeFalse("Not all rows are selected");
        table.GetIsSomeRowsSelected().Should().BeTrue("Some rows are selected");
        table.GetSelectedRowModel().Should().HaveCount(2);
    }

    [Fact]
    public void Table_Should_Maintain_Selection_State()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.SmallDataset);
        var firstRow = table.RowModel.Rows.First();
        var rowId = firstRow.Id;
        
        // Act - Select row
        firstRow.ToggleSelected();
        
        // Re-access the row (simulating state persistence)
        var sameRow = table.RowModel.Rows.First(r => r.Id == rowId);
        
        // Assert
        sameRow.IsSelected.Should().BeTrue("Selection state should be maintained");
    }

    [Fact]
    public void Table_Should_Support_Selection_With_Filtering()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        
        // Select some rows first
        var initialRows = table.RowModel.Rows.Take(5).ToList();
        initialRows.ForEach(row => row.ToggleSelected());
        
        // Act - Apply filter
        table.SetState(new TableState<TestPerson>
        {
            ColumnFilters = new ColumnFiltersState(new List<ColumnFilter>
            {
                new ColumnFilter("department", "Engineering")
            })
        });
        
        // Assert - Selected rows should still be tracked even if not visible
        var selectedRows = table.GetSelectedRowModel();
        selectedRows.Should().NotBeEmpty("Selection should be maintained through filtering");
        
        // Only initially selected rows that match filter should be visible and selected
        var visibleSelectedRows = table.RowModel.Rows.Where(r => r.IsSelected).ToList();
        visibleSelectedRows.Should().OnlyContain(row => row.Original.Department == "Engineering");
    }

    [Fact]
    public void Table_Should_Support_Selection_With_Sorting()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        var initialRows = table.RowModel.Rows.Take(3).ToList();
        var selectedIds = initialRows.Select(r => r.Original.Id).ToList();
        
        // Select rows
        initialRows.ForEach(row => row.ToggleSelected());
        
        // Act - Apply sorting
        table.SetState(new TableState<TestPerson>
        {
            Sorting = new SortingState(new List<ColumnSort>
            {
                new ColumnSort("age", SortDirection.Descending)
            })
        });
        
        // Assert - Selected rows should maintain selection regardless of new order
        var selectedRows = table.GetSelectedRowModel();
        selectedRows.Should().HaveCount(3);
        
        var selectedRowIds = selectedRows.Select(r => r.Original.Id).ToList();
        selectedRowIds.Should().BeEquivalentTo(selectedIds, "Same rows should be selected");
    }

    [Fact]
    public void Table_Should_Support_Selection_With_Pagination()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        
        // Select rows on first page
        var firstPageRows = table.RowModel.Rows.Take(5).ToList();
        firstPageRows.ForEach(row => row.ToggleSelected());
        
        // Act - Enable pagination
        table.SetState(new TableState<TestPerson>
        {
            Pagination = new PaginationState { PageIndex = 0, PageSize = 10 }
        });
        
        // Assert - Selection should be maintained
        var selectedRows = table.GetSelectedRowModel();
        selectedRows.Should().HaveCount(5, "Selection should be maintained with pagination");
        
        // Navigate to different page and back
        table.NextPage();
        table.PreviousPage();
        
        // Should still have same selections
        table.GetSelectedRowModel().Should().HaveCount(5);
    }

    [Fact]
    public void Table_Should_Clear_Selection_On_Reset()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.SmallDataset);
        table.SelectAllRows();
        table.GetSelectedRowModel().Should().NotBeEmpty();
        
        // Act
        table.ResetRowSelection();
        
        // Assert
        table.GetSelectedRowModel().Should().BeEmpty("All selections should be cleared");
        table.RowModel.Rows.Should().AllSatisfy(row => row.IsSelected.Should().BeFalse());
        table.GetIsAllRowsSelected().Should().BeFalse();
    }

    [Fact]
    public void Table_Should_Support_Programmatic_Row_Selection()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.SmallDataset);
        var targetRow = table.RowModel.Rows.Skip(1).First();
        var rowId = targetRow.Id;
        
        // Act - Select row programmatically
        table.SetRowSelection(rowId, true);
        
        // Assert
        var row = table.GetRow(rowId);
        row!.IsSelected.Should().BeTrue("Row should be selected programmatically");
        table.GetSelectedRowModel().Should().ContainSingle();
        
        // Act - Deselect programmatically
        table.SetRowSelection(rowId, false);
        
        // Assert
        row.IsSelected.Should().BeFalse("Row should be deselected programmatically");
        table.GetSelectedRowModel().Should().BeEmpty();
    }

    [Fact]
    public void Table_Should_Support_Range_Selection()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        var rows = table.RowModel.Rows.ToList();
        
        // Act - Select range from index 2 to 5
        table.SelectRowRange(2, 5);
        
        // Assert
        var selectedRows = table.GetSelectedRowModel();
        selectedRows.Should().HaveCount(4, "Should select 4 rows (inclusive range)");
        
        for (int i = 2; i <= 5; i++)
        {
            rows[i].IsSelected.Should().BeTrue($"Row at index {i} should be selected");
        }
        
        // Rows outside range should not be selected
        rows[1].IsSelected.Should().BeFalse();
        rows[6].IsSelected.Should().BeFalse();
    }

    [Fact]
    public void Table_Should_Handle_Invalid_Row_Selection()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.SmallDataset);
        
        // Act & Assert - Try to select non-existent row
        Action act = () => table.SetRowSelection("invalid-row-id", true);
        act.Should().NotThrow("Setting selection for invalid row should be handled gracefully");
        
        table.GetSelectedRowModel().Should().BeEmpty("Invalid selection should not affect state");
    }

    [Fact]
    public void Table_Should_Provide_Selection_Count_Information()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.SmallDataset);
        var totalRows = table.RowModel.Rows.Count;
        
        // Initially no selection
        table.GetSelectedRowCount().Should().Be(0);
        table.GetTotalRowCount().Should().Be(totalRows);
        
        // Select some rows
        table.RowModel.Rows.Take(3).ToList().ForEach(row => row.ToggleSelected());
        
        // Assert counts
        table.GetSelectedRowCount().Should().Be(3);
        table.GetTotalRowCount().Should().Be(totalRows);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public void Row_Selection_Should_Be_Performant(int dataSize)
    {
        // Arrange
        var largeData = PersonTestData.GenerateLargeDataset(dataSize).ToList();
        var table = CreateTableWithData(largeData);
        
        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Select every 5th row
        var rowsToSelect = table.RowModel.Rows.Where((r, i) => i % 5 == 0).ToList();
        rowsToSelect.ForEach(row => row.ToggleSelected());
        
        var selectedCount = table.GetSelectedRowModel().Count;
        
        stopwatch.Stop();
        
        // Assert
        selectedCount.Should().Be(rowsToSelect.Count);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(50,
            $"Selection of {rowsToSelect.Count} rows from {dataSize} should complete within 50ms");
    }

    [Fact]
    public void Table_Should_Support_Selection_State_Serialization()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.SmallDataset);
        var rows = table.RowModel.Rows.Take(2).ToList();
        rows.ForEach(row => row.ToggleSelected());
        
        // Act - Get current selection state
        var selectionState = table.State.RowSelection;
        
        // Create new table with same data and apply selection state
        var newTable = CreateTableWithData(PersonTestData.SmallDataset);
        newTable.SetState(new TableState<TestPerson>
        {
            RowSelection = selectionState
        });
        
        // Assert
        var newSelectedRows = newTable.GetSelectedRowModel();
        newSelectedRows.Should().HaveCount(2, "Selection state should be restored");
        
        var originalIds = rows.Select(r => r.Id).ToList();
        var newIds = newSelectedRows.Select(r => r.Id).ToList();
        newIds.Should().BeEquivalentTo(originalIds, "Same rows should be selected");
    }
}