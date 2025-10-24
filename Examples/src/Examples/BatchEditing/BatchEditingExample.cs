using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Examples.Models;
using SaGrid.Advanced;
using SaGrid.Core;

namespace Examples.Examples.BatchEditing;

internal sealed class BatchEditingExample : IExample
{
    public string Name => "Batch Editing";
    public string Description => "Inline editing with batch commit and undo/redo over a compact dataset.";

    public ExampleHost Create()
    {
        var data = ExampleData.GenerateSmallDataset(60);
        var columns = ExampleData.CreateDefaultColumns();

        var options = new TableOptions<Person>
        {
            Data = data,
            Columns = columns,
            EnableSorting = true,
            EnableColumnFilters = true,
            EnableRowSelection = true,
            EnableCellSelection = true,
            EnableGlobalFilter = false,
            EnableColumnResizing = true,
            State = new TableState<Person>
            {
                // Let the layout manager auto-fit star columns to the viewport
                ColumnSizing = new ColumnSizingState()
            }
        };

        var grid = new SaGrid<Person>(options);

        var infoText = new TextBlock
        {
            Text = "Select a row and use the buttons to begin batch editing, commit changes, or undo/redo.",
            Foreground = Brushes.Gray,
            FontSize = 13
        };

        var buttonRow = new WrapPanel
        {
            Margin = new Thickness(0, 4, 0, 10)
        };

        buttonRow.Children.Add(CreateButton("Begin Batch", () => grid.BeginBatchEdit()));
        buttonRow.Children.Add(CreateButton("Commit Batch", () => grid.CommitBatchEdit()));
        buttonRow.Children.Add(CreateButton("Cancel Batch", () => grid.CancelBatchEdit()));
        buttonRow.Children.Add(CreateButton("Undo", () => grid.UndoLastEdit()));
        buttonRow.Children.Add(CreateButton("Redo", () => grid.RedoLastEdit()));

        // Build a grid so the content row can stretch (last row star)
        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"),
            RowSpacing = 8
        };

        var title = new TextBlock
        {
            Text = "Inline Editing + Batch Commit",
            FontWeight = FontWeight.Bold,
            FontSize = 16
        };
        layout.Children.Add(title);

        Grid.SetRow(infoText, 1);
        layout.Children.Add(infoText);

        Grid.SetRow(buttonRow, 2);
        layout.Children.Add(buttonRow);

        var gridComponent = grid.Component;
        gridComponent.HorizontalAlignment = HorizontalAlignment.Stretch;
        gridComponent.VerticalAlignment = VerticalAlignment.Stretch;
        gridComponent.MinHeight = 360;

        var gridHost = new Border
        {
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4),
            Background = Brushes.White,
            Child = gridComponent
        };
        Grid.SetRow(gridHost, 3);
        layout.Children.Add(gridHost);

        return new ExampleHost(layout);
    }

    private static Button CreateButton(string label, Action onClick)
    {
        var button = new Button
        {
            Content = label,
            Padding = new Thickness(12, 6),
            Height = 32
        };
        button.Click += (_, _) => onClick();
        return button;
    }
}
