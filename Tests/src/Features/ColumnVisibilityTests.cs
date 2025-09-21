using Tests.TestData;
using Tests.Contracts;

namespace Tests.Features;

/// <summary>
/// Tests for column visibility functionality
/// These tests define the expected behavior for column visibility features that need to be implemented
/// </summary>
public class ColumnVisibilityTests : PersonContractTestBase
{
    [Fact]
    public void Table_Should_Support_Hiding_Individual_Columns()
    {
        // Arrange
        var table = CreateTable();
        var ageColumn = table.GetColumn("age");
        ageColumn.Should().NotBeNull();
        
        var initialVisibleCount = table.VisibleLeafColumns.Count;
        
        // Act
        ageColumn!.ToggleVisibility();
        
        // Assert
        ageColumn.IsVisible.Should().BeFalse("Column should be hidden after toggle");
        table.VisibleLeafColumns.Should().HaveCount(initialVisibleCount - 1, "Visible column count should decrease");
        table.VisibleLeafColumns.Should().NotContain(ageColumn, "Hidden column should not be in visible list");
    }

    [Fact]
    public void Table_Should_Support_Showing_Hidden_Columns()
    {
        // Arrange
        var table = CreateTable();
        var ageColumn = table.GetColumn("age");
        
        // Hide column first
        ageColumn!.ToggleVisibility();
        ageColumn.IsVisible.Should().BeFalse();
        
        var hiddenVisibleCount = table.VisibleLeafColumns.Count;
        
        // Act - Show column again
        ageColumn.ToggleVisibility();
        
        // Assert
        ageColumn.IsVisible.Should().BeTrue("Column should be visible after second toggle");
        table.VisibleLeafColumns.Should().HaveCount(hiddenVisibleCount + 1, "Visible column count should increase");
        table.VisibleLeafColumns.Should().Contain(ageColumn, "Shown column should be in visible list");
    }

    [Fact]
    public void Table_Should_Support_Setting_Column_Visibility_State()
    {
        // Arrange
        var table = CreateTable();
        var initialVisibleCount = table.VisibleLeafColumns.Count;
        
        // Act - Hide multiple columns via state
        table.SetState(new TableState<TestPerson>
        {
            ColumnVisibility = new ColumnVisibilityState(new Dictionary<string, bool>
            {
                { "age", false },
                { "email", false }
            })
        });
        
        // Assert
        var ageColumn = table.GetColumn("age");
        var emailColumn = table.GetColumn("email");
        
        ageColumn!.IsVisible.Should().BeFalse("Age column should be hidden");
        emailColumn!.IsVisible.Should().BeFalse("Email column should be hidden");
        table.VisibleLeafColumns.Should().HaveCount(initialVisibleCount - 2, "Two columns should be hidden");
    }

    [Fact]
    public void Table_Should_Reset_Column_Visibility()
    {
        // Arrange
        var table = CreateTable();
        var originalVisibleCount = table.VisibleLeafColumns.Count;
        
        // Hide some columns
        table.GetColumn("age")!.ToggleVisibility();
        table.GetColumn("email")!.ToggleVisibility();
        table.VisibleLeafColumns.Should().HaveCount(originalVisibleCount - 2);
        
        // Act
        table.ResetColumnVisibility();
        
        // Assert
        table.VisibleLeafColumns.Should().HaveCount(originalVisibleCount, "All columns should be visible after reset");
        table.State.ColumnVisibility.Should().BeNull("Column visibility state should be cleared");
        
        // All columns should be visible
        foreach (var column in table.AllColumns)
        {
            column.IsVisible.Should().BeTrue($"Column {column.Id} should be visible after reset");
        }
    }

    [Fact]
    public void Table_Should_Handle_All_Columns_Hidden_Gracefully()
    {
        // Arrange
        var table = CreateTable();
        
        // Act - Try to hide all columns
        foreach (var column in table.AllLeafColumns)
        {
            column.ToggleVisibility();
        }
        
        // Assert
        // Implementation should handle this gracefully - might keep at least one column visible
        // or allow all hidden but handle rendering appropriately
        Action renderAction = () => table.VisibleLeafColumns.ToList();
        renderAction.Should().NotThrow("Should handle all columns hidden gracefully");
    }

    [Fact]
    public void Table_Should_Maintain_Column_Order_With_Visibility_Changes()
    {
        // Arrange
        var table = CreateTable();
        var originalOrder = table.VisibleLeafColumns.Select(c => c.Id).ToList();
        
        // Hide middle column
        var middleColumnId = originalOrder[originalOrder.Count / 2];
        table.GetColumn(middleColumnId)!.ToggleVisibility();
        
        var afterHideOrder = table.VisibleLeafColumns.Select(c => c.Id).ToList();
        
        // Show column again
        table.GetColumn(middleColumnId)!.ToggleVisibility();
        var afterShowOrder = table.VisibleLeafColumns.Select(c => c.Id).ToList();
        
        // Assert
        afterShowOrder.Should().BeEquivalentTo(originalOrder, "Column order should be restored");
    }

    [Fact]
    public void Table_Should_Support_Column_Visibility_With_Filtering()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        
        // Hide a column and apply filter
        table.GetColumn("email")!.ToggleVisibility();
        table.SetState(new TableState<TestPerson>
        {
            ColumnFilters = new ColumnFiltersState(new List<ColumnFilter>
            {
                new ColumnFilter("department", "Engineering")
            }),
            ColumnVisibility = new ColumnVisibilityState(new Dictionary<string, bool>
            {
                { "email", false }
            })
        });
        
        // Assert
        var emailColumn = table.GetColumn("email");
        emailColumn!.IsVisible.Should().BeFalse("Email column should remain hidden");
        
        var filteredRows = table.RowModel.Rows.ToList();
        filteredRows.Should().AllSatisfy(row => 
            row.Original.Department.Should().Be("Engineering", "Filtering should work with hidden columns"));
    }

    [Fact]
    public void Table_Should_Support_Column_Visibility_With_Sorting()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        
        // Hide a column and apply sorting
        table.GetColumn("email")!.ToggleVisibility();
        table.SetState(new TableState<TestPerson>
        {
            Sorting = new SortingState(new List<ColumnSort>
            {
                new ColumnSort("age", SortDirection.Ascending)
            }),
            ColumnVisibility = new ColumnVisibilityState(new Dictionary<string, bool>
            {
                { "email", false }
            })
        });
        
        // Assert
        var emailColumn = table.GetColumn("email");
        emailColumn!.IsVisible.Should().BeFalse("Email column should remain hidden");
        
        var sortedRows = table.RowModel.Rows.ToList();
        var ages = sortedRows.Select(r => r.Original.Age).ToList();
        ages.Should().BeInAscendingOrder("Sorting should work with hidden columns");
    }

    [Fact]
    public void Table_Should_Support_Bulk_Column_Visibility_Changes()
    {
        // Arrange
        var table = CreateTable();
        var columnsToHide = new[] { "age", "email", "department" };
        
        // Act - Hide multiple columns at once
        var visibilityState = new Dictionary<string, bool>();
        foreach (var columnId in columnsToHide)
        {
            visibilityState[columnId] = false;
        }
        
        table.SetColumnVisibility(visibilityState);
        
        // Assert
        foreach (var columnId in columnsToHide)
        {
            var column = table.GetColumn(columnId);
            column!.IsVisible.Should().BeFalse($"Column {columnId} should be hidden");
        }
        
        var visibleColumns = table.VisibleLeafColumns.Select(c => c.Id).ToList();
        visibleColumns.Should().NotContain(columnsToHide, "Hidden columns should not be in visible list");
    }

    [Fact]
    public void Table_Should_Provide_Column_Visibility_Information()
    {
        // Arrange
        var table = CreateTable();
        var totalColumns = table.AllLeafColumns.Count;
        
        // Hide some columns
        table.GetColumn("age")!.ToggleVisibility();
        table.GetColumn("email")!.ToggleVisibility();
        
        // Assert
        table.GetVisibleColumnCount().Should().Be(totalColumns - 2, "Visible column count should be correct");
        table.GetTotalColumnCount().Should().Be(totalColumns, "Total column count should remain the same");
        table.GetHiddenColumnCount().Should().Be(2, "Hidden column count should be correct");
    }

    [Fact]
    public void Table_Should_Support_Column_Visibility_Presets()
    {
        // Arrange
        var table = CreateTable();
        
        // Define visibility presets
        var minimalView = new Dictionary<string, bool>
        {
            { "id", true },
            { "firstName", true },
            { "lastName", true },
            { "age", false },
            { "email", false },
            { "department", false },
            { "isActive", false }
        };
        
        var fullView = new Dictionary<string, bool>
        {
            { "id", true },
            { "firstName", true },
            { "lastName", true },
            { "age", true },
            { "email", true },
            { "department", true },
            { "isActive", true }
        };
        
        // Act & Assert - Apply minimal view
        table.SetColumnVisibility(minimalView);
        table.VisibleLeafColumns.Should().HaveCount(3, "Minimal view should show 3 columns");
        
        // Apply full view
        table.SetColumnVisibility(fullView);
        table.VisibleLeafColumns.Should().HaveCount(7, "Full view should show all columns");
    }

    [Fact]
    public void Table_Should_Handle_Invalid_Column_Visibility_Settings()
    {
        // Arrange
        var table = CreateTable();
        
        // Act & Assert - Try to set visibility for non-existent column
        var invalidVisibility = new Dictionary<string, bool>
        {
            { "nonExistentColumn", false },
            { "age", false } // Valid column
        };
        
        Action setVisibility = () => table.SetColumnVisibility(invalidVisibility);
        setVisibility.Should().NotThrow("Should handle invalid column IDs gracefully");
        
        // Valid column should still be affected
        table.GetColumn("age")!.IsVisible.Should().BeFalse("Valid column should be hidden");
    }

    [Fact]
    public void Table_Should_Persist_Column_Visibility_State()
    {
        // Arrange
        var table = CreateTable();
        
        // Hide some columns
        table.GetColumn("age")!.ToggleVisibility();
        table.GetColumn("email")!.ToggleVisibility();
        
        // Get current state
        var visibilityState = table.State.ColumnVisibility;
        
        // Create new table and apply state
        var newTable = CreateTable();
        newTable.SetState(new TableState<TestPerson>
        {
            ColumnVisibility = visibilityState
        });
        
        // Assert
        newTable.GetColumn("age")!.IsVisible.Should().BeFalse("Age column should be hidden in new table");
        newTable.GetColumn("email")!.IsVisible.Should().BeFalse("Email column should be hidden in new table");
        newTable.VisibleLeafColumns.Should().HaveCount(table.VisibleLeafColumns.Count, 
            "New table should have same visible column count");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Table_Should_Handle_Various_Numbers_Of_Hidden_Columns(int columnsToHide)
    {
        // Arrange
        var table = CreateTable();
        var allColumns = table.AllLeafColumns.ToList();
        var totalColumns = allColumns.Count;
        var columnsToHideList = allColumns.Take(Math.Min(columnsToHide, totalColumns - 1)).ToList();
        
        // Act
        foreach (var column in columnsToHideList)
        {
            column.ToggleVisibility();
        }
        
        // Assert
        table.VisibleLeafColumns.Should().HaveCount(totalColumns - columnsToHideList.Count,
            $"Should hide exactly {columnsToHideList.Count} columns");
        
        foreach (var hiddenColumn in columnsToHideList)
        {
            hiddenColumn.IsVisible.Should().BeFalse($"Column {hiddenColumn.Id} should be hidden");
        }
    }

    [Fact]
    public void Table_Should_Support_Toggle_All_Columns_Visibility()
    {
        // Arrange
        var table = CreateTable();
        var totalColumns = table.AllLeafColumns.Count;
        
        // Act - Hide all columns
        table.ToggleAllColumnsVisible(false);
        
        // Assert
        table.AllLeafColumns.Should().AllSatisfy(column => 
            column.IsVisible.Should().BeFalse($"Column {column.Id} should be hidden"));
        
        // Act - Show all columns
        table.ToggleAllColumnsVisible(true);
        
        // Assert
        table.AllLeafColumns.Should().AllSatisfy(column => 
            column.IsVisible.Should().BeTrue($"Column {column.Id} should be visible"));
        table.VisibleLeafColumns.Should().HaveCount(totalColumns, "All columns should be visible");
    }
}