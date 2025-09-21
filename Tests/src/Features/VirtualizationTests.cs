using Tests.TestData;
using Tests.Contracts;

namespace Tests.Features;

/// <summary>
/// Tests for virtualization functionality
/// These tests define the expected behavior for virtualization features that need to be implemented
/// </summary>
public class VirtualizationTests : PersonContractTestBase
{
    [Theory]
    [InlineData(1000)]
    [InlineData(5000)]
    [InlineData(10000)]
    public void Table_Should_Handle_Large_Datasets_Efficiently_With_Virtualization(int dataSize)
    {
        // Arrange
        var largeData = PersonTestData.GenerateLargeDataset(dataSize).ToList();
        var options = new TableOptions<TestPerson>
        {
            Data = largeData,
            Columns = PersonTestData.StandardColumns,
            EnableVirtualization = true
        };
        
        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var table = new Table<TestPerson>(options);
        stopwatch.Stop();
        
        // Assert
        table.RowModel.Rows.Should().HaveCount(dataSize, "All data should be accessible");
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100, 
            $"Creating virtualized table with {dataSize} rows should complete within 100ms");
    }

    [Fact]
    public void Table_Should_Support_Virtual_Row_Rendering()
    {
        // Arrange
        var largeData = PersonTestData.GenerateLargeDataset(1000).ToList();
        var table = CreateTableWithData(largeData);
        
        // Act - Get virtual rows for viewport (e.g., rows 50-100)
        var virtualRows = table.GetVirtualRows(50, 50); // Start index 50, count 50
        
        // Assert
        virtualRows.Should().HaveCount(50, "Should return requested number of virtual rows");
        virtualRows.First().Index.Should().Be(50, "First virtual row should have correct index");
        virtualRows.Last().Index.Should().Be(99, "Last virtual row should have correct index");
    }

    [Fact]
    public void Table_Should_Calculate_Virtual_Item_Size()
    {
        // Arrange
        var largeData = PersonTestData.GenerateLargeDataset(1000).ToList();
        var table = CreateTableWithData(largeData);
        
        // Act
        var itemSize = table.GetEstimatedRowSize();
        var totalSize = table.GetEstimatedTotalSize();
        
        // Assert
        itemSize.Should().BeGreaterThan(0, "Estimated row size should be positive");
        totalSize.Should().Be(itemSize * largeData.Count, 
            "Total size should be item size multiplied by row count");
    }

    [Fact]
    public void Table_Should_Support_Dynamic_Row_Heights()
    {
        // Arrange
        var data = PersonTestData.MediumDataset;
        var table = CreateTableWithData(data);
        
        // Act - Set different heights for different rows
        table.SetRowHeight(0, 50);
        table.SetRowHeight(1, 75);
        table.SetRowHeight(2, 60);
        
        // Assert
        table.GetRowHeight(0).Should().Be(50);
        table.GetRowHeight(1).Should().Be(75);
        table.GetRowHeight(2).Should().Be(60);
        
        // Total size should reflect dynamic heights
        var totalSize = table.GetEstimatedTotalSize();
        totalSize.Should().BeGreaterThan(0, "Total size should account for dynamic heights");
    }

    [Fact]
    public void Table_Should_Support_Scroll_To_Row()
    {
        // Arrange
        var largeData = PersonTestData.GenerateLargeDataset(1000).ToList();
        var table = CreateTableWithData(largeData);
        
        // Act
        var scrollOffset = table.ScrollToRow(500);
        
        // Assert
        scrollOffset.Should().BeGreaterThan(0, "Should provide scroll offset to target row");
        
        // Should be able to get the row at that position
        var targetRow = table.GetRowAtIndex(500);
        targetRow.Should().NotBeNull("Should be able to get row at scroll position");
        targetRow!.Index.Should().Be(500);
    }

    [Fact]
    public void Table_Should_Handle_Viewport_Changes()
    {
        // Arrange
        var largeData = PersonTestData.GenerateLargeDataset(1000).ToList();
        var table = CreateTableWithData(largeData);
        
        // Act - Set viewport
        table.SetViewport(100, 50); // Start at row 100, show 50 rows
        var viewportRows = table.GetViewportRows();
        
        // Assert
        viewportRows.Should().HaveCount(50, "Should show 50 rows in viewport");
        viewportRows.First().Index.Should().Be(100, "Viewport should start at row 100");
        
        // Change viewport
        table.SetViewport(200, 30);
        var newViewportRows = table.GetViewportRows();
        newViewportRows.Should().HaveCount(30);
        newViewportRows.First().Index.Should().Be(200);
    }
}