using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Examples.Models;
using SaGrid;
using SaGrid.Advanced;
using SaGrid.Core;

namespace Examples.Examples.BatchEditing;

internal sealed class BatchEditingExample : IExample
{
    public string Name => "Batch Editing Playground";
    public string Description => "Inline cell editing with batch commit and undo/redo on a compact dataset.";

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
            EnableGlobalFilter = false
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

        var layout = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = "Inline Editing + Batch Commit",
                    FontWeight = FontWeight.Bold,
                    FontSize = 16
                },
                infoText,
                buttonRow,
                new Border
                {
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(4),
                    Child = new SaGridComponent<Person>(grid)
                }
            }
        };

        return new ExampleHost(new ScrollViewer { Content = layout });
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
