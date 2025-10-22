using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Examples.Models;
using SaGrid.Advanced.Components;
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

    public static List<ColumnDef<Person>> CreateDefaultColumns()
    {
        var idColumn = ColumnHelper.Accessor<Person, int>(p => p.Id, id: "id", header: "ID", width: ColumnWidthDefinition.Fixed(80));
        var firstNameColumn = ColumnHelper.Accessor<Person, string>(p => p.FirstName, id: "firstName", header: "First Name", width: ColumnWidthDefinition.Fixed(140));
        var lastNameColumn = ColumnHelper.Accessor<Person, string>(p => p.LastName, id: "lastName", header: "Last Name", width: ColumnWidthDefinition.Fixed(140));
        var ageColumn = ColumnHelper.Accessor<Person, int>(p => p.Age, id: "age", header: "Age", width: ColumnWidthDefinition.Fixed(80));
        var emailColumn = ColumnHelper.Accessor<Person, string>(p => p.Email, id: "email", header: "Email", width: ColumnWidthDefinition.Star(3));

        var departmentColumn = ColumnHelper.Accessor<Person, string>(
                p => p.Department,
                id: "department",
                header: "Department",
                width: ColumnWidthDefinition.Star(2))
            .WithCustomFilter(context =>
            {
                const string AnyOption = "All Departments";

                string Normalize(object? value) => value?.ToString() ?? AnyOption;

                var distinct = context.Table.PreFilteredRowModel.Rows
                    .Select(row => row.GetCell(context.Column.Id).Value?.ToString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var items = new List<string> { AnyOption };
                items.AddRange(distinct);

                var combo = new ComboBox
                {
                    ItemsSource = items,
                    SelectedItem = Normalize(context.CurrentValue),
                    MinWidth = 140,
                    IsEnabled = context.Column.CanFilter
                };

                combo.SelectionChanged += (_, _) =>
                {
                    if (combo.SelectedItem is string selected)
                    {
                        context.SetFilterValue(selected == AnyOption ? null : selected);
                    }
                };

                return new ColumnFilterRegistration(
                    combo,
                    value =>
                    {
                        var desired = Normalize(value);
                        if (!Equals(combo.SelectedItem, desired))
                        {
                            combo.SelectedItem = desired;
                        }
                    },
                    () => combo.IsKeyboardFocusWithin || combo.IsDropDownOpen);
            });

        var isActiveColumn = ColumnHelper.Accessor<Person, bool>(
                p => p.IsActive,
                id: "isActive",
                header: "Active",
                width: ColumnWidthDefinition.Fixed(100))
            .WithBooleanFilter();

        return new List<ColumnDef<Person>>
        {
            idColumn,
            firstNameColumn,
            lastNameColumn,
            ageColumn,
            emailColumn,
            departmentColumn,
            isActiveColumn
        };
    }
}
