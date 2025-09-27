using System;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using SaGrid;
using SaGrid.Core;
using Tests.TestData;
using Xunit;
using FluentAssertions;

namespace Tests.Features;

public class ClientStarterRenderingTests
{
    [Fact]
    public void VirtualizedRowsControl_ShouldCreateRowControls_WithCells()
    {
        // Arrange
        var data = PersonTestData.GenerateLargeDataset(200).ToList();
        var options = new TableOptions<TestPerson>
        {
            Data = data,
            Columns = PersonTestData.StandardColumns,
            EnableSorting = true,
            EnableColumnFilters = true,
            EnableRowSelection = true,
            EnableCellSelection = true,
            EnableGlobalFilter = true
        };

        var grid = new SaGrid<TestPerson>(options);

        var rendererType = typeof(SaGrid<TestPerson>).Assembly
            .GetType("SaGrid.SaGridBodyRenderer`1")!
            .MakeGenericType(typeof(TestPerson));
        var renderer = Activator.CreateInstance(rendererType)!;
        var createBody = rendererType.GetMethod("CreateBody", BindingFlags.Public | BindingFlags.Instance)!;
        var control = (Control)createBody.Invoke(renderer, new object?[] { grid, null, null })!;

        var virtualizedType = control.GetType();
        var updateViewport = virtualizedType.GetMethod("UpdateViewport", BindingFlags.NonPublic | BindingFlags.Instance);
        updateViewport!.Invoke(control, new object?[] { true });

        var canvasField = virtualizedType.GetField("_canvas", BindingFlags.NonPublic | BindingFlags.Instance);
        var canvas = (Canvas)canvasField!.GetValue(control)!;

        // Assert there are row visuals
        canvas.Children.Count.Should().BeGreaterThan(0, "virtualized control should create row visuals");

        // Assert first row has cells corresponding to visible columns
        var firstRow = canvas.Children[0].Should().BeOfType<StackPanel>().Subject;
        firstRow.Children.Count.Should().Be(grid.VisibleLeafColumns.Count, "each visible column should produce a cell control");
    }

    [Fact]
    public void VirtualizedRowsControl_ShouldReflectFiltering()
    {
        var data = PersonTestData.GenerateLargeDataset(200).ToList();
        var options = new TableOptions<TestPerson>
        {
            Data = data,
            Columns = PersonTestData.StandardColumns,
            EnableSorting = true,
            EnableColumnFilters = true,
            EnableRowSelection = true,
            EnableCellSelection = true,
            EnableGlobalFilter = true
        };

        var grid = new SaGrid<TestPerson>(options);

        var rendererType = typeof(SaGrid<TestPerson>).Assembly
            .GetType("SaGrid.SaGridBodyRenderer`1")!
            .MakeGenericType(typeof(TestPerson));
        var renderer = Activator.CreateInstance(rendererType)!;
        var createBody = rendererType.GetMethod("CreateBody", BindingFlags.Public | BindingFlags.Instance)!;
        var control = (Control)createBody.Invoke(renderer, new object?[] { grid, null, null })!;

        var virtualizedType = control.GetType();
        var updateViewport = virtualizedType.GetMethod("UpdateViewport", BindingFlags.NonPublic | BindingFlags.Instance);
        var canvasField = virtualizedType.GetField("_canvas", BindingFlags.NonPublic | BindingFlags.Instance);

        updateViewport!.Invoke(control, new object?[] { true });
        var canvas = (Canvas)canvasField!.GetValue(control)!;
        var initialRowControls = canvas.Children.Count;

        grid.SetColumnFilter("department", "Engineering");
        updateViewport.Invoke(control, new object?[] { true });

        var filteredRowControls = canvas.Children.Count;
        var filteredRowCount = grid.RowModel.Rows.Count;

        filteredRowCount.Should().BeLessThan(data.Count, "filtering should reduce the dataset");

        var expectedVisible = Math.Min(filteredRowCount, initialRowControls);
        filteredRowControls.Should().Be(expectedVisible, "virtualized control should only render visible rows");

        var tryGetDisplayedRow = typeof(SaGrid<TestPerson>).GetMethod(
            "TryGetDisplayedRow",
            BindingFlags.NonPublic | BindingFlags.Instance);
        tryGetDisplayedRow.Should().NotBeNull();

        var firstFilteredRow = (Row<TestPerson>?)tryGetDisplayedRow!.Invoke(grid, new object?[] { 0 });
        firstFilteredRow.Should().NotBeNull();
        firstFilteredRow!.GetCell("department").Value?.ToString()
            .Should().Contain("Engineering", "first displayed row should match applied filter");
    }
}
