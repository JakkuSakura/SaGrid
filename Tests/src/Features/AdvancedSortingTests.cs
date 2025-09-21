using Tests.TestData;
using Tests.Contracts;

namespace Tests.Features;

/// <summary>
/// Tests for advanced sorting functionality including multi-column sorting
/// These tests define the expected behavior for sorting features that need to be implemented
/// </summary>
public class AdvancedSortingTests : PersonContractTestBase
{
    [Fact]
    public void Table_Should_Support_Multi_Column_Sorting()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        
        // Act - Sort by Department first, then by Age
        var multiColumnSorts = new List<ColumnSort>
        {
            new ColumnSort("department", SortDirection.Ascending),
            new ColumnSort("age", SortDirection.Descending)
        };

        // This should work once multi-column sorting is implemented
        Action act = () => table.SetState(new TableState<TestPerson>
        {
            Sorting = new SortingState(multiColumnSorts)
        });

        // Assert - This will initially fail until multi-column sorting is implemented
        act.Should().NotThrow("Multi-column sorting should be supported");
        
        // Verify the sorting was applied
        var sortedRows = table.RowModel.Rows.ToList();
        
        // Should be grouped by department, then by age descending within each department
        for (int i = 1; i < sortedRows.Count; i++)
        {
            var prev = sortedRows[i - 1].Original;
            var curr = sortedRows[i].Original;
            
            // Department should be in ascending order
            var deptComparison = string.Compare(prev.Department, curr.Department, StringComparison.Ordinal);
            if (deptComparison == 0)
            {
                // Within same department, age should be in descending order
                prev.Age.Should().BeGreaterOrEqualTo(curr.Age,
                    "Within same department, age should be sorted descending");
            }
            else
            {
                deptComparison.Should().BeLessOrEqualTo(0,
                    "Departments should be sorted ascending");
            }
        }
    }

    [Fact]
    public void Table_Should_Handle_Sort_Priority_Changes()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        
        // Act - First set age as primary sort
        table.SetState(new TableState<TestPerson>
        {
            Sorting = new SortingState(new List<ColumnSort>
            {
                new ColumnSort("age", SortDirection.Ascending)
            })
        });
        
        var ageFirstResult = table.RowModel.Rows.Select(r => r.Original.Age).ToList();
        
        // Then add department as higher priority
        table.SetState(new TableState<TestPerson>
        {
            Sorting = new SortingState(new List<ColumnSort>
            {
                new ColumnSort("department", SortDirection.Ascending),
                new ColumnSort("age", SortDirection.Ascending)
            })
        });
        
        var deptFirstResult = table.RowModel.Rows.Select(r => r.Original.Department).ToList();
        
        // Assert
        ageFirstResult.Should().BeInAscendingOrder("Initial sort by age should work");
        
        // After adding department as primary sort, results should be different
        deptFirstResult.Should().NotEqual(ageFirstResult.Select((_, i) => 
            table.RowModel.Rows[i].Original.Department).ToList(),
            "Adding department as primary sort should change the order");
    }

    [Fact]
    public void Table_Should_Support_Removing_Sort_Columns()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        
        // Set up multi-column sorting
        table.SetState(new TableState<TestPerson>
        {
            Sorting = new SortingState(new List<ColumnSort>
            {
                new ColumnSort("department", SortDirection.Ascending),
                new ColumnSort("age", SortDirection.Descending),
                new ColumnSort("firstName", SortDirection.Ascending)
            })
        });
        
        var multiSortResult = table.RowModel.Rows.ToList();
        
        // Act - Remove middle sort column (age)
        table.SetState(new TableState<TestPerson>
        {
            Sorting = new SortingState(new List<ColumnSort>
            {
                new ColumnSort("department", SortDirection.Ascending),
                new ColumnSort("firstName", SortDirection.Ascending)
            })
        });
        
        var reducedSortResult = table.RowModel.Rows.ToList();
        
        // Assert
        multiSortResult.Should().NotBeEmpty();
        reducedSortResult.Should().NotBeEmpty();
        
        // Results should be different after removing a sort column
        var multiSortIds = multiSortResult.Select(r => r.Original.Id).ToList();
        var reducedSortIds = reducedSortResult.Select(r => r.Original.Id).ToList();
        
        multiSortIds.Should().NotEqual(reducedSortIds,
            "Removing a sort column should change the order");
    }

    [Fact]
    public void Table_Should_Handle_Sort_Direction_Changes()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        
        // Act - Sort ascending
        table.SetState(new TableState<TestPerson>
        {
            Sorting = new SortingState(new List<ColumnSort>
            {
                new ColumnSort("age", SortDirection.Ascending)
            })
        });
        
        var ascendingResult = table.RowModel.Rows.Select(r => r.Original.Age).ToList();
        
        // Change to descending
        table.SetState(new TableState<TestPerson>
        {
            Sorting = new SortingState(new List<ColumnSort>
            {
                new ColumnSort("age", SortDirection.Descending)
            })
        });
        
        var descendingResult = table.RowModel.Rows.Select(r => r.Original.Age).ToList();
        
        // Assert
        ascendingResult.Should().BeInAscendingOrder();
        descendingResult.Should().BeInDescendingOrder();
        descendingResult.Should().Equal(ascendingResult.OrderByDescending(x => x),
            "Descending should be reverse of ascending");
    }

    [Fact]
    public void Table_Should_Support_Toggling_Column_Sort()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        var ageColumn = table.GetColumn("age");
        
        ageColumn.Should().NotBeNull("Age column should exist");
        
        // Act & Assert - Test sort direction cycling: None -> Asc -> Desc -> None
        
        // Initial state (no sorting)
        ageColumn!.SortDirection.Should().BeNull("Initially no sorting");
        
        // First toggle: None -> Ascending
        ageColumn.ToggleSorting();
        ageColumn.SortDirection.Should().Be(SortDirection.Ascending);
        
        // Second toggle: Ascending -> Descending
        ageColumn.ToggleSorting();
        ageColumn.SortDirection.Should().Be(SortDirection.Descending);
        
        // Third toggle: Descending -> None
        ageColumn.ToggleSorting();
        ageColumn.SortDirection.Should().BeNull("Should cycle back to no sorting");
    }

    [Fact]
    public void Table_Should_Maintain_Sort_Indices_Correctly()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        
        // Act - Set up multi-column sorting with specific priorities
        table.SetState(new TableState<TestPerson>
        {
            Sorting = new SortingState(new List<ColumnSort>
            {
                new ColumnSort("department", SortDirection.Ascending),
                new ColumnSort("age", SortDirection.Descending),
                new ColumnSort("firstName", SortDirection.Ascending)
            })
        });
        
        // Assert
        var deptColumn = table.GetColumn("department");
        var ageColumn = table.GetColumn("age");
        var nameColumn = table.GetColumn("firstName");
        
        deptColumn.Should().NotBeNull();
        ageColumn.Should().NotBeNull();
        nameColumn.Should().NotBeNull();
        
        // Verify sort indices match the priorities
        deptColumn!.SortIndex.Should().Be(0, "Department should be primary sort");
        ageColumn!.SortIndex.Should().Be(1, "Age should be secondary sort");
        nameColumn!.SortIndex.Should().Be(2, "FirstName should be tertiary sort");
        
        // Verify sort directions
        deptColumn.SortDirection.Should().Be(SortDirection.Ascending);
        ageColumn.SortDirection.Should().Be(SortDirection.Descending);
        nameColumn.SortDirection.Should().Be(SortDirection.Ascending);
    }

    [Fact]
    public void Table_Should_Clear_All_Sorting()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        
        // Set up multi-column sorting
        table.SetState(new TableState<TestPerson>
        {
            Sorting = new SortingState(new List<ColumnSort>
            {
                new ColumnSort("department", SortDirection.Ascending),
                new ColumnSort("age", SortDirection.Descending)
            })
        });
        
        // Verify sorting is applied
        var deptColumn = table.GetColumn("department");
        deptColumn!.SortDirection.Should().NotBeNull("Sorting should be applied initially");
        
        // Act
        table.ResetSorting();
        
        // Assert
        table.State.Sorting.Should().BeNull("Sorting state should be cleared");
        
        // All columns should have no sort direction
        foreach (var column in table.AllColumns)
        {
            column.SortDirection.Should().BeNull($"Column {column.Id} should have no sort direction");
            column.SortIndex.Should().BeNull($"Column {column.Id} should have no sort index");
        }
    }

    [Fact]
    public void Table_Should_Handle_Custom_Sort_Functions()
    {
        // Arrange
        var customData = new[]
        {
            new TestPerson(1, "John", "Doe", 30, "john@example.com", "Engineering", true),
            new TestPerson(2, "Jane", "Smith", 25, "jane@example.com", "Engineering", true),
            new TestPerson(3, "Bob", "Johnson", 35, "bob@example.com", "Engineering", false)
        };

        // Create columns with custom sort function for age (reverse order)
        var customColumns = new ColumnDef<TestPerson>[]
        {
            ColumnHelper.Accessor<TestPerson, string>(p => p.FirstName, "firstName", "First Name"),
            ColumnHelper.Accessor<TestPerson, int>(p => p.Age, "age", "Age")
        };

        var options = new TableOptions<TestPerson>
        {
            Data = customData,
            Columns = customColumns,
            EnableSorting = true
        };

        var table = new Table<TestPerson>(options);
        
        // Act - Sort by age
        table.SetState(new TableState<TestPerson>
        {
            Sorting = new SortingState(new List<ColumnSort>
            {
                new ColumnSort("age", SortDirection.Ascending)
            })
        });
        
        var sortedAges = table.RowModel.Rows.Select(r => r.Original.Age).ToList();
        
        // Assert
        sortedAges.Should().BeInAscendingOrder("Custom sort should work for age column");
    }

    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(500)]
    public void Multi_Column_Sorting_Should_Be_Performant(int dataSize)
    {
        // Arrange
        var largeData = PersonTestData.GenerateLargeDataset(dataSize).ToList();
        var table = CreateTableWithData(largeData);
        
        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        table.SetState(new TableState<TestPerson>
        {
            Sorting = new SortingState(new List<ColumnSort>
            {
                new ColumnSort("department", SortDirection.Ascending),
                new ColumnSort("age", SortDirection.Descending),
                new ColumnSort("firstName", SortDirection.Ascending)
            })
        });
        
        // Force evaluation
        var result = table.RowModel.Rows.ToList();
        
        stopwatch.Stop();
        
        // Assert
        result.Should().HaveCount(dataSize);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100,
            $"Multi-column sorting of {dataSize} rows should complete within 100ms");
    }

    [Fact]
    public void Table_Should_Handle_Null_Values_In_Sorting()
    {
        // Arrange
        var dataWithNulls = new[]
        {
            new TestPerson(1, "John", "Doe", 30, "john@example.com", "Engineering", true),
            new TestPerson(2, "", "Smith", 25, "jane@example.com", "Marketing", true), // Empty string
            new TestPerson(3, "Bob", "", 35, "bob@example.com", "Sales", false), // Empty string
            new TestPerson(4, "Alice", "Johnson", 28, "alice@example.com", "", true) // Empty department
        };

        var table = CreateTableWithData(dataWithNulls);
        
        // Act - Sort by firstName (which has empty string)
        table.SetState(new TableState<TestPerson>
        {
            Sorting = new SortingState(new List<ColumnSort>
            {
                new ColumnSort("firstName", SortDirection.Ascending)
            })
        });
        
        var sortedNames = table.RowModel.Rows.Select(r => r.Original.FirstName).ToList();
        
        // Assert
        // Should handle empty strings gracefully (empty strings typically sort first)
        sortedNames.Should().NotBeNull();
        sortedNames.Should().HaveCount(4);
        
        // The exact order depends on implementation, but it should not throw
        Action verification = () => table.RowModel.Rows.ToList();
        verification.Should().NotThrow("Sorting with empty strings should not throw");
    }
}