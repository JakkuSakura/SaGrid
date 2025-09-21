using Tests.TestData;
using Tests.Contracts;

namespace Tests.Features;

/// <summary>
/// Tests for cell selection functionality
/// These tests verify the cell selection features work correctly
/// </summary>
public class CellSelectionTests : PersonContractTestBase
{
    [Fact]
    public void SaGrid_Should_Support_Single_Cell_Selection()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.SmallDataset);
        var saGrid = new SaGrid<TestPerson>(table.Options);

        // Act - Select a single cell
        saGrid.SelectCell(0, "firstName");

        // Assert
        var selectedCells = saGrid.GetSelectedCells();
        var activeCell = saGrid.GetActiveCell();

        selectedCells.Should().HaveCount(1, "Should have exactly one selected cell");
        activeCell.Should().NotBeNull("Should have an active cell");
        activeCell!.Value.RowIndex.Should().Be(0, "Active cell should be at row 0");
        activeCell.Value.ColumnId.Should().Be("firstName", "Active cell should be in firstName column");
        saGrid.IsCellSelected(0, "firstName").Should().BeTrue("The selected cell should be marked as selected");
    }

    [Fact]
    public void SaGrid_Should_Support_Cell_Range_Selection()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.SmallDataset);
        var saGrid = new SaGrid<TestPerson>(table.Options);

        // Act - Select a range of cells
        saGrid.SelectCellRange(0, "id", 1, "firstName");

        // Assert
        var selectedCells = saGrid.GetSelectedCells();
        selectedCells.Should().HaveCountGreaterThan(1, "Should have multiple selected cells in range");
        
        // Verify specific cells in the range are selected
        saGrid.IsCellSelected(0, "id").Should().BeTrue("Cell (0, id) should be selected");
        saGrid.IsCellSelected(0, "firstName").Should().BeTrue("Cell (0, firstName) should be selected");
        saGrid.IsCellSelected(1, "id").Should().BeTrue("Cell (1, id) should be selected");
        saGrid.IsCellSelected(1, "firstName").Should().BeTrue("Cell (1, firstName) should be selected");
    }

    [Fact]
    public void SaGrid_Should_Clear_Cell_Selection()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.SmallDataset);
        var saGrid = new SaGrid<TestPerson>(table.Options);
        saGrid.SelectCell(0, "firstName");

        // Act - Clear selection
        saGrid.ClearCellSelection();

        // Assert
        var selectedCells = saGrid.GetSelectedCells();
        var activeCell = saGrid.GetActiveCell();

        selectedCells.Should().BeEmpty("Should have no selected cells after clearing");
        activeCell.Should().BeNull("Should have no active cell after clearing");
        saGrid.IsCellSelected(0, "firstName").Should().BeFalse("Previously selected cell should no longer be selected");
    }

    [Fact]
    public void SaGrid_Should_Copy_Selected_Cells_To_Text()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.SmallDataset);
        var saGrid = new SaGrid<TestPerson>(table.Options);
        saGrid.SelectCellRange(0, "firstName", 1, "lastName");

        // Act - Copy selected cells
        var copiedText = saGrid.CopySelectedCells();

        // Assert
        copiedText.Should().NotBeNullOrEmpty("Should generate copied text");
        copiedText.Should().Contain("John", "Should contain data from selected cells");
        // The copied text should be tab-separated values representing the selected cells
    }

    [Fact]
    public void SaGrid_Should_Support_Cell_Navigation()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.SmallDataset);
        var saGrid = new SaGrid<TestPerson>(table.Options);
        saGrid.SelectCell(1, "firstName"); // Start at row 1, firstName column

        // Act & Assert - Navigate right
        saGrid.NavigateCell(CellNavigationDirection.Right);
        var activeCell = saGrid.GetActiveCell();
        activeCell.Should().NotBeNull("Should have active cell after navigation");
        activeCell!.Value.RowIndex.Should().Be(1, "Should stay in same row");
        activeCell.Value.ColumnId.Should().Be("lastName", "Should move to next column");

        // Act & Assert - Navigate down
        saGrid.NavigateCell(CellNavigationDirection.Down);
        activeCell = saGrid.GetActiveCell();
        activeCell!.Value.RowIndex.Should().Be(2, "Should move to next row");
        activeCell.Value.ColumnId.Should().Be("lastName", "Should stay in same column");

        // Act & Assert - Navigate up
        saGrid.NavigateCell(CellNavigationDirection.Up);
        activeCell = saGrid.GetActiveCell();
        activeCell!.Value.RowIndex.Should().Be(1, "Should move back to previous row");

        // Act & Assert - Navigate left
        saGrid.NavigateCell(CellNavigationDirection.Left);
        activeCell = saGrid.GetActiveCell();
        activeCell!.Value.ColumnId.Should().Be("firstName", "Should move back to previous column");
    }

    [Fact]
    public void SaGrid_Should_Respect_EnableCellSelection_Option()
    {
        // Arrange - Create SaGrid.Advanced with cell selection disabled
        var table = CreateTableWithData(PersonTestData.SmallDataset);
        var optionsWithoutCellSelection = new TableOptions<TestPerson>
        {
            Data = table.Options.Data,
            Columns = table.Options.Columns,
            EnableCellSelection = false // Explicitly disable
        };
        var saGrid = new SaGrid<TestPerson>(optionsWithoutCellSelection);

        // Act - Try to select a cell
        saGrid.SelectCell(0, "firstName");

        // Assert - Selection should be ignored
        var selectedCells = saGrid.GetSelectedCells();
        var activeCell = saGrid.GetActiveCell();

        selectedCells.Should().BeEmpty("Should not select cells when cell selection is disabled");
        activeCell.Should().BeNull("Should not have active cell when cell selection is disabled");
    }

    [Fact]
    public void SaGrid_Should_Handle_Multiple_Cell_Selection_With_AddToSelection()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.SmallDataset);
        var saGrid = new SaGrid<TestPerson>(table.Options);

        // Act - Select multiple individual cells
        saGrid.SelectCell(0, "firstName", addToSelection: false); // First cell
        saGrid.SelectCell(1, "lastName", addToSelection: true);   // Add second cell
        saGrid.SelectCell(2, "age", addToSelection: true);        // Add third cell

        // Assert
        var selectedCells = saGrid.GetSelectedCells();
        selectedCells.Should().HaveCount(3, "Should have three individually selected cells");
        
        saGrid.IsCellSelected(0, "firstName").Should().BeTrue("First cell should be selected");
        saGrid.IsCellSelected(1, "lastName").Should().BeTrue("Second cell should be selected");
        saGrid.IsCellSelected(2, "age").Should().BeTrue("Third cell should be selected");
    }
}