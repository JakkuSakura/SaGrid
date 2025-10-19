using System;
using System.Collections.Generic;
using System.Linq;
using Examples.Models;
using SaGrid.Core;

namespace Examples.Examples;

internal static class ExampleData
{
    public const double DefaultTableWidth = 960;

    private static readonly string[] Departments = { "Engineering", "Marketing", "Sales", "HR", "Finance", "Operations", "Support" };
    private static readonly string[] FirstNames = { "Alice", "Bob", "Charlie", "Diana", "Eve", "Frank", "Grace", "Henry", "Jane", "John" };
    private static readonly string[] LastNames = { "Anderson", "Brown", "Davis", "Garcia", "Johnson", "Jones", "Miller", "Smith", "Taylor", "Williams" };

    public static IEnumerable<Person> GenerateDataset(int count, int seed = 42)
    {
        var random = new Random(seed);
        for (int i = 1; i <= count; i++)
        {
            var firstName = FirstNames[random.Next(FirstNames.Length)];
            var lastName = LastNames[random.Next(LastNames.Length)];

            yield return new Person(
                i,
                $"{firstName}{i}",
                $"{lastName}{i}",
                random.Next(22, 65),
                $"{firstName.ToLower()}.{lastName.ToLower()}{i}@example.com",
                Departments[random.Next(Departments.Length)],
                random.NextDouble() < 0.75);
        }
    }

    public static List<Person> GenerateSmallDataset(int count) => GenerateDataset(count).ToList();

    public static List<Person> GenerateLargeDataset(int count) => GenerateDataset(count).ToList();

    public static List<ColumnDef<Person>> CreateDefaultColumns() => new()
    {
        ColumnHelper.Accessor<Person, int>(p => p.Id, id: "id", header: "ID", width: ColumnWidthDefinition.Fixed(80)),
        ColumnHelper.Accessor<Person, string>(p => p.FirstName, id: "firstName", header: "First Name", width: ColumnWidthDefinition.Fixed(140)),
        ColumnHelper.Accessor<Person, string>(p => p.LastName, id: "lastName", header: "Last Name", width: ColumnWidthDefinition.Fixed(140)),
        ColumnHelper.Accessor<Person, int>(p => p.Age, id: "age", header: "Age", width: ColumnWidthDefinition.Fixed(80)),
        ColumnHelper.Accessor<Person, string>(p => p.Email, id: "email", header: "Email", width: ColumnWidthDefinition.Star(3)),
        ColumnHelper.Accessor<Person, string>(p => p.Department, id: "department", header: "Department", width: ColumnWidthDefinition.Star(2)),
        ColumnHelper.Accessor<Person, bool>(p => p.IsActive, id: "isActive", header: "Active", width: ColumnWidthDefinition.Fixed(100))
    };
}
