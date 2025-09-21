using SaGrid.Core;
using Tests.TestData;

namespace Tests.Contracts;

/// <summary>
/// Base class for contract tests that verify API compliance and behavior
/// These tests must pass for any implementation of TanStack Table interfaces
/// </summary>
public abstract class ContractTestBase<TData>
{
    protected abstract IEnumerable<TData> CreateTestData();
    protected abstract IEnumerable<ColumnDef<TData>> CreateTestColumns();
    protected virtual TableOptions<TData> CreateDefaultOptions()
    {
        return new TableOptions<TData>
        {
            Data = CreateTestData(),
            Columns = CreateTestColumns().ToList(),
            EnableColumnFilters = true,
            EnableGlobalFilter = true,
            EnableSorting = true,
            EnableRowSelection = true,
            EnablePagination = true
        };
    }

    protected Table<TData> CreateTable(TableOptions<TData>? options = null)
    {
        return new Table<TData>(options ?? CreateDefaultOptions());
    }

    protected Table<TData> CreateTableWithData(IEnumerable<TData> data)
    {
        return new Table<TData>(new TableOptions<TData>
        {
            Data = data,
            Columns = CreateTestColumns().ToList(),
            EnableColumnFilters = true,
            EnableGlobalFilter = true,
            EnableSorting = true,
            EnableRowSelection = true,
            EnablePagination = true
        });
    }

    protected Table<TData> CreateTableWithColumns(IEnumerable<ColumnDef<TData>> columns)
    {
        return new Table<TData>(new TableOptions<TData>
        {
            Data = CreateTestData(),
            Columns = columns.ToList()
        });
    }

    /// <summary>
    /// Verifies that a table has the expected basic properties
    /// </summary>
    protected void VerifyBasicTableProperties(ITable<TData> table)
    {
        table.Should().NotBeNull();
        table.Options.Should().NotBeNull();
        table.State.Should().NotBeNull();
        table.AllColumns.Should().NotBeNull();
        table.AllLeafColumns.Should().NotBeNull();
        table.VisibleLeafColumns.Should().NotBeNull();
        table.HeaderGroups.Should().NotBeNull();
        table.FooterGroups.Should().NotBeNull();
        table.RowModel.Should().NotBeNull();
    }

    /// <summary>
    /// Verifies that table state transitions are immutable
    /// </summary>
    protected void VerifyStateImmutability(ITable<TData> table)
    {
        var originalState = table.State;
        
        // Any state change should create a new state instance
        table.SetState(originalState);
        
        // The state reference should be the same if no actual changes were made
        table.State.Should().Be(originalState);
    }

    /// <summary>
    /// Verifies performance requirements for large datasets
    /// </summary>
    protected void VerifyPerformanceRequirements(ITable<TData> table, int expectedRowCount)
    {
        // Verify the table can handle the expected number of rows
        table.RowModel.Rows.Should().HaveCount(expectedRowCount);
        
        // Basic operations should complete quickly
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Test basic read operations
        _ = table.AllColumns.Count;
        _ = table.VisibleLeafColumns.Count;
        _ = table.RowModel.Rows.Count;
        
        stopwatch.Stop();
        
        // Should complete basic operations in reasonable time
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(50, 
            "Basic table operations should be fast");
    }
}

/// <summary>
/// Specialized base for Person test data - commonly used across tests
/// </summary>
public abstract class PersonContractTestBase : ContractTestBase<TestPerson>
{
    protected override IEnumerable<TestPerson> CreateTestData()
    {
        return new[]
        {
            new TestPerson(1, "John", "Doe", 30, "john.doe@example.com", "Engineering", true),
            new TestPerson(2, "Jane", "Smith", 25, "jane.smith@example.com", "Marketing", true),
            new TestPerson(3, "Bob", "Johnson", 35, "bob.johnson@example.com", "Sales", false),
            new TestPerson(4, "Alice", "Williams", 28, "alice.williams@example.com", "Engineering", true),
            new TestPerson(5, "Charlie", "Brown", 32, "charlie.brown@example.com", "HR", true)
        };
    }

    protected override IEnumerable<ColumnDef<TestPerson>> CreateTestColumns()
    {
        return new ColumnDef<TestPerson>[]
        {
            ColumnHelper.Accessor<TestPerson, int>(p => p.Id, "id", "ID"),
            ColumnHelper.Accessor<TestPerson, string>(p => p.FirstName, "firstName", "First Name"),
            ColumnHelper.Accessor<TestPerson, string>(p => p.LastName, "lastName", "Last Name"),
            ColumnHelper.Accessor<TestPerson, int>(p => p.Age, "age", "Age"),
            ColumnHelper.Accessor<TestPerson, string>(p => p.Email, "email", "Email"),
            ColumnHelper.Accessor<TestPerson, string>(p => p.Department, "department", "Department"),
            ColumnHelper.Accessor<TestPerson, bool>(p => p.IsActive, "isActive", "Active")
        };
    }

    protected IEnumerable<TestPerson> CreateLargeDataset(int count)
    {
        var random = new Random(42); // Fixed seed for reproducible tests
        var departments = new[] { "Engineering", "Marketing", "Sales", "HR", "Finance" };
        
        for (int i = 1; i <= count; i++)
        {
            yield return new TestPerson(
                i,
                $"FirstName{i}",
                $"LastName{i}",
                random.Next(22, 65),
                $"user{i}@example.com",
                departments[random.Next(departments.Length)],
                random.Next(0, 2) == 1
            );
        }
    }
}

