using SaGrid.Core;

namespace Tests.TestData;

/// <summary>
/// Centralized test data generators for Person-based tests
/// Provides consistent, repeatable test data across all test scenarios
/// </summary>
public static class PersonTestData
{
    /// <summary>
    /// Small dataset for basic functionality tests
    /// </summary>
    public static IReadOnlyList<TestPerson> SmallDataset { get; } = new TestPerson[]
    {
        new(1, "John", "Doe", 30, "john.doe@example.com", "Engineering", true),
        new(2, "Jane", "Smith", 25, "jane.smith@example.com", "Marketing", true),
        new(3, "Bob", "Johnson", 35, "bob.johnson@example.com", "Sales", false),
        new(4, "Alice", "Williams", 28, "alice.williams@example.com", "Engineering", true),
        new(5, "Charlie", "Brown", 32, "charlie.brown@example.com", "HR", true)
    };

    /// <summary>
    /// Medium dataset for sorting and filtering tests
    /// </summary>
    public static IReadOnlyList<TestPerson> MediumDataset { get; } = GenerateMediumDataset();

    /// <summary>
    /// Standard column definitions for Person data
    /// </summary>
    public static IReadOnlyList<ColumnDef<TestPerson>> StandardColumns { get; } = new ColumnDef<TestPerson>[]
    {
        ColumnHelper.Accessor<TestPerson, int>(p => p.Id, "id", "ID"),
        ColumnHelper.Accessor<TestPerson, string>(p => p.FirstName, "firstName", "First Name"),
        ColumnHelper.Accessor<TestPerson, string>(p => p.LastName, "lastName", "Last Name"),
        ColumnHelper.Accessor<TestPerson, int>(p => p.Age, "age", "Age"),
        ColumnHelper.Accessor<TestPerson, string>(p => p.Email, "email", "Email"),
        ColumnHelper.Accessor<TestPerson, string>(p => p.Department, "department", "Department"),
        ColumnHelper.Accessor<TestPerson, bool>(p => p.IsActive, "isActive", "Active")
    };

    /// <summary>
    /// Minimal column set for basic tests
    /// </summary>
    public static IReadOnlyList<ColumnDef<TestPerson>> MinimalColumns { get; } = new ColumnDef<TestPerson>[]
    {
        ColumnHelper.Accessor<TestPerson, int>(p => p.Id, "id", "ID"),
        ColumnHelper.Accessor<TestPerson, string>(p => p.FirstName, "firstName", "First Name")
    };

    /// <summary>
    /// Sortable columns for sorting tests
    /// </summary>
    public static IReadOnlyList<ColumnDef<TestPerson>> SortableColumns { get; } = new ColumnDef<TestPerson>[]
    {
        ColumnHelper.Accessor<TestPerson, string>(p => p.FirstName, "firstName", "First Name"),
        ColumnHelper.Accessor<TestPerson, string>(p => p.LastName, "lastName", "Last Name"),
        ColumnHelper.Accessor<TestPerson, int>(p => p.Age, "age", "Age"),
        ColumnHelper.Accessor<TestPerson, string>(p => p.Department, "department", "Department")
    };

    /// <summary>
    /// Create default table options with small dataset
    /// </summary>
    public static TableOptions<TestPerson> DefaultTableOptions => new()
    {
        Data = SmallDataset,
        Columns = StandardColumns,
        EnableSorting = true,
        EnableGlobalFilter = false,
        EnableColumnFilters = false,
        EnablePagination = false
    };

    /// <summary>
    /// Create table options with sorting enabled
    /// </summary>
    public static TableOptions<TestPerson> SortingEnabledOptions => new()
    {
        Data = MediumDataset,
        Columns = SortableColumns,
        EnableSorting = true,
        EnableGlobalFilter = false,
        EnableColumnFilters = false,
        EnablePagination = false
    };

    /// <summary>
    /// Create table options with filtering enabled
    /// </summary>
    public static TableOptions<TestPerson> FilteringEnabledOptions => new()
    {
        Data = MediumDataset,
        Columns = StandardColumns,
        EnableSorting = false,
        EnableGlobalFilter = true,
        EnableColumnFilters = true,
        EnablePagination = false
    };

    /// <summary>
    /// Create table options with pagination enabled
    /// </summary>
    public static TableOptions<TestPerson> PaginationEnabledOptions => new()
    {
        Data = MediumDataset,
        Columns = StandardColumns,
        EnableSorting = false,
        EnableGlobalFilter = false,
        EnableColumnFilters = false,
        EnablePagination = true,
        State = new TableState<TestPerson>
        {
            Pagination = new PaginationState { PageIndex = 0, PageSize = 5 }
        }
    };

    /// <summary>
    /// Generate large dataset for performance tests
    /// </summary>
    /// <param name="count">Number of records to generate</param>
    /// <param name="seed">Random seed for reproducible results</param>
    public static IEnumerable<TestPerson> GenerateLargeDataset(int count, int seed = 42)
    {
        var random = new Random(seed);
        var departments = new[] { "Engineering", "Marketing", "Sales", "HR", "Finance", "Operations", "Support" };
        var firstNames = new[] { "John", "Jane", "Bob", "Alice", "Charlie", "Diana", "Eve", "Frank", "Grace", "Henry" };
        var lastNames = new[] { "Doe", "Smith", "Johnson", "Williams", "Brown", "Davis", "Miller", "Wilson", "Moore", "Taylor" };

        for (int i = 1; i <= count; i++)
        {
            yield return new TestPerson(
                i,
                firstNames[random.Next(firstNames.Length)] + i,
                lastNames[random.Next(lastNames.Length)],
                random.Next(22, 65),
                $"user{i}@example.com",
                departments[random.Next(departments.Length)],
                random.Next(0, 2) == 1
            );
        }
    }

    /// <summary>
    /// Generate dataset with specific characteristics for edge case testing
    /// </summary>
    public static IEnumerable<TestPerson> GenerateEdgeCaseDataset()
    {
        return new TestPerson[]
        {
            // Empty/null-like values
            new(1, "", "", 0, "", "", false),
            
            // Very long values
            new(2, new string('A', 100), new string('B', 100), 99, 
                new string('a', 50) + "@" + new string('b', 50) + ".com", 
                "Very Long Department Name That Exceeds Normal Length", true),
            
            // Special characters
            new(3, "John-Paul", "O'Connor", 30, "john.paul@test-domain.co.uk", "R&D", true),
            new(4, "José", "García", 25, "josé.garcía@example.com", "Développement", true),
            
            // Edge case ages
            new(5, "Young", "Person", 18, "young@example.com", "Internship", true),
            new(6, "Old", "Person", 100, "old@example.com", "Consulting", false),
            
            // Duplicate names
            new(7, "John", "Smith", 30, "john1@example.com", "Engineering", true),
            new(8, "John", "Smith", 35, "john2@example.com", "Marketing", false),
            
            // Similar emails
            new(9, "Test", "User", 25, "test@example.com", "Testing", true),
            new(10, "Test", "User", 25, "test@example.org", "Testing", true)
        };
    }

    /// <summary>
    /// Create empty dataset for edge case testing
    /// </summary>
    public static IReadOnlyList<TestPerson> EmptyDataset { get; } = Array.Empty<TestPerson>();

    /// <summary>
    /// Create single item dataset for edge case testing
    /// </summary>
    public static IReadOnlyList<TestPerson> SingleItemDataset { get; } = new TestPerson[]
    {
        new(1, "Single", "Item", 30, "single@example.com", "Testing", true)
    };

    private static IReadOnlyList<TestPerson> GenerateMediumDataset()
    {
        return GenerateLargeDataset(50).ToList();
    }
}

/// <summary>
/// Test data record for Person-based tests
/// Immutable record with all properties needed for comprehensive testing
/// </summary>
public record TestPerson(
    int Id,
    string FirstName,
    string LastName,
    int Age,
    string Email,
    string Department,
    bool IsActive
)
{
    /// <summary>
    /// Full name for display purposes
    /// </summary>
    public string FullName => $"{FirstName} {LastName}";

    /// <summary>
    /// Age category for grouping tests
    /// </summary>
    public string AgeCategory => Age switch
    {
        < 25 => "Young",
        >= 25 and < 35 => "Mid",
        >= 35 and < 50 => "Senior",
        _ => "Veteran"
    };

    /// <summary>
    /// Override ToString for better test output
    /// </summary>
    public override string ToString() => $"{FullName} ({Age}) - {Department}";
}