using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Markup.Declarative;
using SaGrid.Core;
using Avalonia;
using Avalonia.Input;
using static SolidAvalonia.Solid;
using System.Linq;

namespace SaGrid;

internal class SaGridHeaderRenderer<TData>
{
  private readonly Action<TextBox>? _onFilterFocus;
  private readonly Action<string, TextBox>? _onFilterTextBoxCreated;

  public SaGridHeaderRenderer(Action<TextBox>? onFilterFocus = null, Action<string, TextBox>? onFilterTextBoxCreated = null)
  {
    _onFilterFocus = onFilterFocus;
    _onFilterTextBoxCreated = onFilterTextBoxCreated;
  }

  public Control CreateHeader(SaGrid<TData> saGrid, Func<SaGrid<TData>>? gridSignalGetter = null,
    Func<int>? selectionSignalGetter = null)
  {
    _ = gridSignalGetter;
    _ = selectionSignalGetter;
    var headerControls = new List<Control>();

    // Add header title rows with sortable headers
    headerControls.AddRange(saGrid.HeaderGroups.Select(headerGroup =>
      new StackPanel()
        .Orientation(Orientation.Horizontal)
        .Children(
          headerGroup.Headers.Select(header =>
          {
            var column = (Column<TData>)header.Column;
            var border = new Border()
              .BorderThickness(0, 0, 1, 1)
              .BorderBrush(Brushes.LightGray)
              .Background(Brushes.LightBlue)
              .Padding(new Thickness(0))
              .Width(header.Size)
              .Height(40)
              .HorizontalAlignment(HorizontalAlignment.Stretch)
              .VerticalAlignment(VerticalAlignment.Stretch);

            // Use a Button for reliable click handling and accessibility
            var button = new Button
            {
              Background = Brushes.Transparent,
              BorderBrush = null,
              BorderThickness = new Thickness(0),
              Padding = new Thickness(0),
              HorizontalContentAlignment = HorizontalAlignment.Stretch,
              VerticalContentAlignment = VerticalAlignment.Stretch,
              Focusable = false,
              IsTabStop = false,
              HorizontalAlignment = HorizontalAlignment.Stretch,
              VerticalAlignment = VerticalAlignment.Stretch,
              Cursor = new Cursor(StandardCursorType.Hand)
            };

            button.Content = CreateHeaderLabel(saGrid, column, header);

            // Unified sorting handler: plain click = single-sort cycle (replace others);
            // with modifier and multi-sort enabled = append/switch/remove in chain.
            void ApplySorting(bool multi)
            {
              if (multi && saGrid.IsMultiSortEnabled())
              {
                var currentDir = column.SortDirection;
                var current = saGrid.State.Sorting?.Columns ?? new List<ColumnSort>();

                if (currentDir == null)
                {
                  var newList = current.ToList();
                  newList.Add(new ColumnSort(column.Id, SortDirection.Ascending));
                  saGrid.SetSorting(newList);
                }
                else if (currentDir == SortDirection.Ascending)
                {
                  // Replace in-place to preserve index
                  var newList = current.ToList();
                  var idx = newList.FindIndex(s => s.Id == column.Id);
                  if (idx >= 0)
                  {
                    newList[idx] = new ColumnSort(column.Id, SortDirection.Descending);
                  }
                  else
                  {
                    newList.Add(new ColumnSort(column.Id, SortDirection.Descending));
                  }

                  saGrid.SetSorting(newList);
                }
                else
                {
                  // Remove this column from sorting, preserve others' order
                  var newList = current.Where(s => s.Id != column.Id).ToList();
                  saGrid.SetSorting(newList);
                }
              }
              else
              {
                // Single-sort cycle: toggle this column and clear all others
                var dir = column.SortDirection;
                if (dir == null)
                {
                  saGrid.SetSorting(new[] { new ColumnSort(column.Id, SortDirection.Ascending) });
                }
                else if (dir == SortDirection.Ascending)
                {
                  saGrid.SetSorting(new[] { new ColumnSort(column.Id, SortDirection.Descending) });
                }
                else
                {
                  saGrid.SetSorting(Array.Empty<ColumnSort>());
                }
              }
            }

            // Behaviors equivalent to XAML ButtonClickEventTriggerBehavior with KeyModifiers
            // Control => multi-sort toggle
            // Shift => reset sorting
            // Control+Shift => multi-sort toggle
            // Command (Meta) => multi-sort toggle (macOS)
            // Option (Alt) => multi-sort toggle
            {
              var ctrlBehavior = new SaGrid.Behaviors.ButtonClickEventTriggerBehavior
              {
                RequiredModifiers = KeyModifiers.Control,
                Action = () => ApplySorting(true)
              };
              ctrlBehavior.Attach(button);

              var shiftBehavior = new SaGrid.Behaviors.ButtonClickEventTriggerBehavior
              {
                RequiredModifiers = KeyModifiers.Shift,
                Action = () => ApplySorting(true)
              };
              shiftBehavior.Attach(button);

              var commandBehavior = new SaGrid.Behaviors.ButtonClickEventTriggerBehavior
              {
                RequiredModifiers = KeyModifiers.Meta,
                Action = () => ApplySorting(true)
              };
              commandBehavior.Attach(button);

              var optionBehavior = new SaGrid.Behaviors.ButtonClickEventTriggerBehavior
              {
                RequiredModifiers = KeyModifiers.Alt,
                Action = () => ApplySorting(true)
              };
              optionBehavior.Attach(button);
            }

            // Plain click toggles single-sort (replace others)
            button.Click += (s, e) =>
            {
              // Always single-sort on plain click; modifier-specific actions are handled by behaviors
              ApplySorting(false);
            };

            // Make the entire cell clickable
            border.Child(button);

            return border;
          }).ToArray()
        )
    ));

    // Add filter row if column filtering is enabled
    if (saGrid.Options.EnableColumnFilters)
    {
      var filterRow = CreateFilterRow(saGrid);
      Console.WriteLine($"Adding filter row to header - Total header controls: {headerControls.Count + 1}");
      headerControls.Add(filterRow);
    }
    else
    {
      Console.WriteLine("Column filtering is disabled - no filter row will be added");
    }

    return new StackPanel()
      .Orientation(Orientation.Vertical)
      .Children(headerControls.ToArray());
  }

  private Control CreateFilterRow(SaGrid<TData> saGrid)
  {
    Console.WriteLine($"Creating filter row with {saGrid.VisibleLeafColumns.Count} columns");

    var filterControls = saGrid.VisibleLeafColumns.Select(column =>
    {
      Console.WriteLine($"Creating filter for column: {column.Id}");
      var textBox = CreateFilterTextBox(saGrid, column);
      return new Border()
        .BorderThickness(0, 0, 1, 1)
        .BorderBrush(Brushes.LightGray)
        .Background(Brushes.White)
        .Width(column.Size)
        .Height(35)
        .Padding(new Thickness(2))
        .Child(textBox);
    }).ToArray();

    // Put filter row inside a Border with a visible bottom line to suggest input area
    return new Border()
      .BorderThickness(new Thickness(0, 0, 0, 1))
      .BorderBrush(Brushes.LightGray)
      .Child(
        new StackPanel()
          .Orientation(Orientation.Horizontal)
          .Children(filterControls)
      );
  }

  private Control CreateFilterTextBox(SaGrid<TData> saGrid, Column<TData> column)
  {
    Console.WriteLine($"Creating TextBox for column {column.Id}");

    var textBox = new TextBox
    {
      Watermark = $"Filter {column.Id}...",
      Width = double.NaN, // Auto width
      Height = double.NaN, // Auto height
      HorizontalAlignment = HorizontalAlignment.Stretch,
      VerticalAlignment = VerticalAlignment.Stretch,
      Focusable = true,
      IsEnabled = true,
      AcceptsReturn = false,
      AcceptsTab = false
    };
    textBox.Tag = column.Id;
    _onFilterTextBoxCreated?.Invoke(column.Id, textBox);
    textBox.Margin = new Thickness(4, 4, 4, 4);
    textBox.BorderThickness = new Thickness(1);
    textBox.BorderBrush = Brushes.Gray;
    textBox.Background = Brushes.White;
    // Ensure the TextBox can receive input immediately
    textBox.TabIndex = 0;
    textBox.IsTabStop = true;

    Console.WriteLine(
      $"TextBox created for {column.Id} - Focusable: {textBox.Focusable}, IsEnabled: {textBox.IsEnabled}");

    // Add multiple event handlers to debug what's happening
    textBox.GotFocus += (sender, args) =>
    {
      Console.WriteLine($"TextBox for column {column.Id} got focus");
      if (sender is TextBox tb)
      {
        _onFilterFocus?.Invoke(tb);
      }
    };

    textBox.LostFocus += (sender, args) => { Console.WriteLine($"TextBox for column {column.Id} lost focus"); };

    textBox.PointerPressed += (sender, args) =>
    {
      Console.WriteLine($"TextBox for column {column.Id} pointer pressed");
      // Explicitly capture focus on click to resist parent re-renders
      if (sender is TextBox tb && !tb.IsFocused)
      {
        tb.Focus();
      }

      args.Handled = false;
    };

    textBox.PointerEntered += (sender, args) =>
    {
      Console.WriteLine($"TextBox for column {column.Id} pointer entered");
    };

    textBox.KeyDown += (sender, args) =>
    {
      // Let TextBox handle keys normally; grid ignores keys from TextBox
      Console.WriteLine($"TextBox for column {column.Id} key down: {args.Key}");
    };

    textBox.TextChanging += (sender, args) => { Console.WriteLine($"TextBox for column {column.Id} text changing"); };

    textBox.TextChanged += (sender, args) =>
    {
      if (sender is TextBox tb)
      {
        var newValue = string.IsNullOrWhiteSpace(tb.Text) ? (object?)null : tb.Text;

        // Avoid redundant SetColumnFilter calls that can cause render loops
        var currentValue = saGrid.State.ColumnFilters?.Filters
          .FirstOrDefault(f => f.Id == column.Id)?.Value;

        var equals = (currentValue == null && newValue == null) ||
                     (currentValue != null && newValue != null &&
                      string.Equals(currentValue.ToString(), newValue.ToString(), StringComparison.Ordinal));

        if (!equals)
        {
          Console.WriteLine($"Filter changed for column {column.Id}: '{currentValue}' -> '{newValue}'");
          saGrid.SetColumnFilter(column.Id, newValue);
        }
      }
    };

    // Initialize TextBox with current filter value (if any)
    var currentFilter = saGrid.State.ColumnFilters?.Filters.FirstOrDefault(f => f.Id == column.Id)?.Value?.ToString();
    if (!string.IsNullOrEmpty(currentFilter) && textBox.Text != currentFilter)
    {
      // Set Text without firing TextChanged loop by checking inequality
      textBox.Text = currentFilter;
    }

    return textBox;
  }

  private Control CreateHeaderLabel(SaGrid<TData> saGrid, Column<TData> column, IHeader<TData> header)
  {
    var title = SaGridContentHelper<TData>.GetHeaderContent(header);
    var sortSuffix = string.Empty;

    if (column.SortDirection != null)
    {
      var arrow = column.SortDirection == SaGrid.Core.SortDirection.Ascending ? "▲" : "▼";
      var isMulti = saGrid.IsMultiSortEnabled();
      var index = (isMulti && column.SortIndex.HasValue)
        ? $" {column.SortIndex.Value + 1}"
        : string.Empty;
      sortSuffix = $" {arrow}{index}";
    }

    var container = new Grid
    {
      HorizontalAlignment = HorizontalAlignment.Stretch,
      VerticalAlignment = VerticalAlignment.Stretch
    };

    var centeredTitle = new TextBlock()
      .Text(title)
      .HorizontalAlignment(HorizontalAlignment.Center)
      .VerticalAlignment(VerticalAlignment.Center)
      .TextAlignment(TextAlignment.Center)
      .FontWeight(FontWeight.Bold);

    var rightIndicator = new TextBlock()
      .Text(sortSuffix)
      .HorizontalAlignment(HorizontalAlignment.Right)
      .VerticalAlignment(VerticalAlignment.Center)
      .Margin(new Thickness(8, 0, 8, 0));

    container.Children.Add(centeredTitle);
    container.Children.Add(rightIndicator);

    return container;
  }
}
