using SaGrid.Advanced;
using Tests.TestData;
using Tests.Contracts;

namespace Tests.Features;

/// <summary>
/// Tests for SaGrid.Advanced integration functionality
/// These tests define the expected behavior for SaGrid.Advanced-specific features that need to be implemented
/// </summary>
public class SaGridIntegrationTests : PersonContractTestBase
{
    [Fact]
    public void SaGrid_Should_Support_Advanced_Cell_Renderers()
    {
        // Arrange
        var table = CreateTable();
        
        // Act - This test assumes SaGrid.Advanced-specific cell renderers
        var saGrid = new SaGrid<TestPerson>(table);
        
        // Assert
        saGrid.Should().NotBeNull("SaGrid.Advanced should be creatable from table");
        
        // Test custom cell renderers (implementation-specific)
        Action renderTest = () => saGrid.RenderCell("age", table.RowModel.Rows.First());
        renderTest.Should().NotThrow("Should support custom cell rendering");
    }

    [Fact]
    public void SaGrid_Should_Support_Column_Resizing()
    {
        // Arrange
        var table = CreateTable();
        var saGrid = new SaGrid<TestPerson>(table);
        var ageColumn = table.GetColumn("age");
        
        var originalSize = ageColumn!.Size;
        
        // Act
        saGrid.ResizeColumn("age", 150);
        
        // Assert
        ageColumn.Size.Should().Be(150, "Column size should be updated");
        ageColumn.Size.Should().NotBe(originalSize, "Size should have changed");
    }

    [Fact]
    public void SaGrid_Should_Support_Column_Reordering()
    {
        // Arrange
        var table = CreateTable();
        var saGrid = new SaGrid<TestPerson>(table);
        var originalOrder = table.VisibleLeafColumns.Select(c => c.Id).ToList();
        
        // Act - Move "age" column to first position
        saGrid.MoveColumn("age", 0);
        
        // Assert
        var newOrder = table.VisibleLeafColumns.Select(c => c.Id).ToList();
        newOrder.Should().NotBeEquivalentTo(originalOrder, "Column order should change");
        newOrder.First().Should().Be("age", "Age column should be first");
    }

    [Fact]
    public void SaGrid_Should_Support_Advanced_Theming()
    {
        // Arrange
        var table = CreateTable();
        var saGrid = new SaGrid<TestPerson>(table);
        
        // Act - Apply theme
        saGrid.SetTheme("dark");
        
        // Assert
        saGrid.CurrentTheme.Should().Be("dark", "Theme should be applied");
        
        // Test theme properties
        saGrid.GetThemeProperty("backgroundColor").Should().NotBeNull("Theme should have properties");
    }

    [Fact]
    public void SaGrid_Should_Support_Custom_Header_Renderers()
    {
        // Arrange
        var table = CreateTable();
        var saGrid = new SaGrid<TestPerson>(table);
        
        // Act - Set custom header renderer
        saGrid.SetHeaderRenderer("age", columnId => $"Custom: {columnId}");
        
        // Assert
        var headerContent = saGrid.RenderHeader("age");
        headerContent.Should().Contain("Custom: age", "Should use custom header renderer");
    }

    [Fact]
    public void SaGrid_Should_Support_Row_Actions()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.SmallDataset);
        var saGrid = new SaGrid<TestPerson>(table);
        var firstRow = table.RowModel.Rows.First();
        
        // Act - Add row actions
        saGrid.AddRowAction("edit", (row) => $"Edit {row.Original.FirstName}");
        saGrid.AddRowAction("delete", (row) => $"Delete {row.Original.FirstName}");
        
        // Assert
        var actions = saGrid.GetRowActions(firstRow);
        actions.Should().HaveCount(2, "Should have two row actions");
        actions.Should().Contain(action => action.Id == "edit");
        actions.Should().Contain(action => action.Id == "delete");
    }

    [Fact]
    public void SaGrid_Should_Support_Context_Menus()
    {
        // Arrange
        var table = CreateTable();
        var saGrid = new SaGrid<TestPerson>(table);
        
        // Act - Configure context menu
        saGrid.SetContextMenuItems(new[]
        {
            new ContextMenuItem("copy", "Copy")
            {
                Action = _ => { }
            },
            new ContextMenuItem("export", "Export")
            {
                Action = _ => { }
            }
        });
        
        // Assert
        var menuItems = saGrid.GetContextMenuItems();
        menuItems.Should().HaveCount(2);
        menuItems.Should().Contain(item => item.Id == "copy");
        menuItems.Should().Contain(item => item.Id == "export");
    }

    [Fact]
    public void SaGrid_Should_Support_Export_Functionality()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.SmallDataset);
        var saGrid = new SaGrid<TestPerson>(table);
        
        // Act - Export to CSV
        var csvData = saGrid.ExportToCsv();
        
        // Assert
        csvData.Should().NotBeNullOrEmpty("Should generate CSV data");
        csvData.Should().Contain("FirstName", "Should include column headers");
        csvData.Should().Contain("John", "Should include row data");
        
        // Act - Export to JSON
        var jsonData = saGrid.ExportToJson();
        
        // Assert
        jsonData.Should().NotBeNullOrEmpty("Should generate JSON data");
        jsonData.Should().Contain("firstName", "Should include property names");
    }

    [Fact]
    public void SaGrid_Should_Support_Keyboard_Navigation()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.SmallDataset);
        var saGrid = new SaGrid<TestPerson>(table);
        
        // Act - Simulate keyboard navigation
        saGrid.HandleKeyDown("ArrowDown");
        saGrid.HandleKeyDown("ArrowRight");
        
        // Assert
        var currentCell = saGrid.GetCurrentCell();
        currentCell.Should().NotBeNull("Should have current cell after navigation");
        
        var currentRow = saGrid.GetCurrentRow();
        currentRow.Should().NotBeNull("Should have current row after navigation");
    }
}
