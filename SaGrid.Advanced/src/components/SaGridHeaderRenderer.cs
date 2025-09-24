using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Declarative;
using Avalonia.Media;
using SaGrid.Advanced.DragDrop;
using SaGrid.Advanced.Interactive;
using SaGrid.Core;
using static SolidAvalonia.Solid;

namespace SaGrid;

internal class SaGridHeaderRenderer<TData>
{
    private readonly Action<TextBox>? _onFilterFocus;
    private readonly Action<string, TextBox>? _onFilterTextBoxCreated;
    private DragDropManager<TData>? _dragDropManager;
    private ColumnInteractiveService<TData>? _columnService;
    private readonly List<IDragSource> _activeDragSources = new();
    private readonly List<IDropZone> _activeDropZones = new();

    private bool HasInteractivity => _dragDropManager != null && _columnService != null;

    public SaGridHeaderRenderer(Action<TextBox>? onFilterFocus = null, Action<string, TextBox>? onFilterTextBoxCreated = null)
    {
        _onFilterFocus = onFilterFocus;
        _onFilterTextBoxCreated = onFilterTextBoxCreated;
    }

    public void EnableInteractivity(DragDropManager<TData> dragDropManager, ColumnInteractiveService<TData> columnService)
    {
        _dragDropManager = dragDropManager;
        _columnService = columnService;
    }

    public void DisableInteractivity()
    {
        CleanupInteractivity();
        _dragDropManager = null;
        _columnService = null;
    }

    public Control CreateHeader(SaGrid<TData> saGrid, Func<SaGrid<TData>>? gridSignalGetter = null,
        Func<int>? selectionSignalGetter = null)
    {
        _ = gridSignalGetter;
        _ = selectionSignalGetter;

        CleanupInteractivity();

        var headerControls = new List<Control>();

        if (saGrid.Options.EnableGrouping || saGrid.GetGroupedColumnIds().Count > 0)
        {
            var groupingArea = CreateGroupingArea(saGrid);
            if (groupingArea != null)
            {
                headerControls.Add(groupingArea);
            }
        }

        foreach (var headerGroup in saGrid.HeaderGroups)
        {
            var headerRow = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            foreach (var header in headerGroup.Headers)
            {
                var column = (Column<TData>)header.Column;
                var headerCell = CreateHeaderCell(saGrid, column, header);
                headerRow.Children.Add(headerCell);
            }

            if (headerRow.Children.Count > 0 && HasInteractivity)
            {
                var dropZone = new ColumnDropZone<TData>(headerRow, _columnService!, saGrid, _dragDropManager!.RootVisual);
                _dragDropManager.RegisterDropZone(dropZone);
                _activeDropZones.Add(dropZone);
            }

            headerControls.Add(headerRow);
        }

        if (saGrid.Options.EnableColumnFilters)
        {
            headerControls.Add(CreateFilterRow(saGrid));
        }

        return new StackPanel()
            .Orientation(Orientation.Vertical)
            .Children(headerControls.ToArray());
    }

    private Control CreateHeaderCell(SaGrid<TData> saGrid, Column<TData> column, IHeader<TData> header)
    {
        return HasInteractivity
            ? CreateInteractiveHeaderCell(saGrid, column, header)
            : CreateBasicHeaderCell(saGrid, column, header);
    }

    private Control? CreateGroupingArea(SaGrid<TData> saGrid)
    {
        var groupingService = saGrid.GetGroupingService();
        var groupedIds = groupingService.GetGroupedColumnIds(saGrid);

        var container = new Border()
            .BorderThickness(0, 0, 0, 1)
            .BorderBrush(Brushes.LightGray)
            .Background(new SolidColorBrush(Colors.WhiteSmoke))
            .Padding(new Thickness(8, 6));

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (groupedIds.Count == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "Drag columns here to create groups",
                Foreground = Brushes.Gray,
                FontStyle = FontStyle.Italic,
                VerticalAlignment = VerticalAlignment.Center
            });
        }
        else
        {
            foreach (var columnId in groupedIds)
            {
                if (saGrid.GetColumn(columnId) is not Column<TData> column)
                {
                    continue;
                }

                var chip = CreateGroupingChip(saGrid, column);
                panel.Children.Add(chip);

                if (HasInteractivity)
                {
                    var dragSource = new GroupingChipDragSource<TData>(column, chip);
                    _dragDropManager!.RegisterDragSource(dragSource);
                    _activeDragSources.Add(dragSource);
                }
            }
        }

        container.Child = panel;

        if (HasInteractivity)
        {
            var dropZone = new GroupingDropZone<TData>(panel, groupingService, saGrid, _dragDropManager!.RootVisual);
            _dragDropManager.RegisterDropZone(dropZone);
            _activeDropZones.Add(dropZone);
        }

        return container;
    }

    private Control CreateGroupingChip(SaGrid<TData> saGrid, Column<TData> column)
    {
        var text = SaGridContentHelper<TData>.GetHeaderContent(column);

        var label = new TextBlock
        {
            Text = text,
            Margin = new Thickness(12, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        var closeButton = new Button
        {
            Content = "✕",
            Width = 18,
            Height = 18,
            Padding = new Thickness(0),
            FontSize = 10,
            Background = Brushes.Transparent,
            BorderBrush = null,
            Cursor = new Cursor(StandardCursorType.Hand),
            ToolTip = $"Remove grouping by {text}"
        };

        closeButton.Click += (s, e) => saGrid.RemoveGroupingColumn(column.Id);

        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center
        };
        content.Children.Add(label);
        content.Children.Add(closeButton);

        var chip = new Border
        {
            Background = new SolidColorBrush(Colors.LightSteelBlue, 0.9),
            BorderBrush = Brushes.SteelBlue,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(4, 2),
            Child = content,
            Tag = column.Id,
            Cursor = new Cursor(StandardCursorType.Hand)
        };

        chip.PointerEntered += (s, e) =>
        {
            if (s is Border border)
            {
                border.Background = new SolidColorBrush(Colors.LightSkyBlue, 0.95);
            }
        };

        chip.PointerExited += (s, e) =>
        {
            if (s is Border border)
            {
                border.Background = new SolidColorBrush(Colors.LightSteelBlue, 0.9);
            }
        };

        return chip;
    }

    private Control CreateBasicHeaderCell(SaGrid<TData> saGrid, Column<TData> column, IHeader<TData> header)
    {
        var border = new Border()
            .BorderThickness(0, 0, 1, 1)
            .BorderBrush(Brushes.LightGray)
            .Background(Brushes.LightBlue)
            .Padding(new Thickness(0))
            .Width(header.Size)
            .Height(40)
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .VerticalAlignment(VerticalAlignment.Stretch);

        border.Child = CreateMainHeaderButton(saGrid, column, header);

        return border;
    }

    private Control CreateInteractiveHeaderCell(SaGrid<TData> saGrid, Column<TData> column, IHeader<TData> header)
    {
        var border = new Border()
            .BorderThickness(0, 0, 1, 1)
            .BorderBrush(Brushes.LightGray)
            .Background(Brushes.LightBlue)
            .Padding(new Thickness(0))
            .Width(header.Size)
            .Height(40)
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .VerticalAlignment(VerticalAlignment.Stretch);

        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(20)));

        var mainButton = CreateMainHeaderButton(saGrid, column, header);
        Grid.SetColumn(mainButton, 0);
        headerGrid.Children.Add(mainButton);

        if (_dragDropManager != null && _columnService != null)
        {
            var dragSource = new ColumnDragSource<TData>(column, mainButton, _columnService);
            _dragDropManager.RegisterDragSource(dragSource);
            _activeDragSources.Add(dragSource);

            var resizeHandle = CreateResizeHandle(column, header);
            Grid.SetColumn(resizeHandle, 1);
            headerGrid.Children.Add(resizeHandle);
        }

        border.Child = headerGrid;
        return border;
    }

    private Button CreateMainHeaderButton(SaGrid<TData> saGrid, Column<TData> column, IHeader<TData> header)
    {
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
            Cursor = new Cursor(StandardCursorType.Hand),
            Content = CreateHeaderLabel(saGrid, column, header)
        };

        SetupSortingBehavior(button, saGrid, column);
        return button;
    }

    private Control CreateResizeHandle(Column<TData> column, IHeader<TData> header)
    {
        var resizeHandle = new Border()
            .Width(4)
            .Background(Brushes.Transparent)
            .Cursor(new Cursor(StandardCursorType.SizeWestEast))
            .VerticalAlignment(VerticalAlignment.Stretch);

        double? dragStartX = null;
        double startWidth = 0;
        var isResizing = false;

        resizeHandle.PointerEntered += (s, e) =>
        {
            if (s is Border border)
            {
                border.Background = new SolidColorBrush(Colors.Blue, 0.3);
            }
        };

        resizeHandle.PointerExited += (s, e) =>
        {
            if (s is Border border)
            {
                border.Background = Brushes.Transparent;
            }
        };

        resizeHandle.PointerPressed += (s, e) =>
        {
            var point = e.GetCurrentPoint(resizeHandle);
            if (point.Properties.IsLeftButtonPressed)
            {
                dragStartX = point.Position.X;
                startWidth = header.Size;
                isResizing = true;
                e.Pointer.Capture(resizeHandle);
                e.Handled = true;
            }
        };

        resizeHandle.PointerMoved += (s, e) =>
        {
            if (isResizing && dragStartX.HasValue && ReferenceEquals(e.Pointer.Captured, resizeHandle))
            {
                var current = e.GetCurrentPoint(resizeHandle).Position.X;
                var delta = current - dragStartX.Value;
                var newWidth = Math.Max(30, startWidth + delta);
                _columnService?.SetColumnWidth(column.Id, newWidth);
                e.Handled = true;
            }
        };

        void EndResize(IPointer pointer)
        {
            if (isResizing)
            {
                isResizing = false;
                dragStartX = null;
                startWidth = 0;
                if (ReferenceEquals(pointer.Captured, resizeHandle))
                {
                    pointer.Capture(null);
                }
            }
        }

        resizeHandle.PointerReleased += (s, e) =>
        {
            EndResize(e.Pointer);
            e.Handled = true;
        };

        resizeHandle.PointerCaptureLost += (s, e) =>
        {
            isResizing = false;
            dragStartX = null;
            startWidth = 0;
        };

        resizeHandle.DoubleTapped += (s, e) =>
        {
            _columnService?.AutoSizeColumn(column.Id);
        };

        return resizeHandle;
    }

    private void SetupSortingBehavior(Button button, SaGrid<TData> saGrid, Column<TData> column)
    {
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
                    var newList = current.Where(s => s.Id != column.Id).ToList();
                    saGrid.SetSorting(newList);
                }
            }
            else
            {
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

        var modifiers = new[]
        {
            KeyModifiers.Control,
            KeyModifiers.Shift,
            KeyModifiers.Meta,
            KeyModifiers.Alt
        };

        foreach (var modifier in modifiers)
        {
            var behavior = new SaGrid.Behaviors.ButtonClickEventTriggerBehavior
            {
                RequiredModifiers = modifier,
                Action = () => ApplySorting(true)
            };
            behavior.Attach(button);
        }

        button.Click += (s, e) => ApplySorting(false);
    }

    private Control CreateFilterRow(SaGrid<TData> saGrid)
    {
        var filterControls = saGrid.VisibleLeafColumns.Select(column =>
        {
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

        return new Border()
            .BorderThickness(new Thickness(0, 0, 0, 1))
            .BorderBrush(Brushes.LightGray)
            .Child(
                new StackPanel()
                    .Orientation(Orientation.Horizontal)
                    .Children(filterControls)
            );
    }

    private TextBox CreateFilterTextBox(SaGrid<TData> saGrid, Column<TData> column)
    {
        var textBox = new TextBox
        {
            Watermark = $"Filter {column.Id}...",
            Width = double.NaN,
            Height = double.NaN,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Focusable = true,
            IsEnabled = true,
            AcceptsReturn = false,
            AcceptsTab = false,
            Margin = new Thickness(4),
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Gray,
            Background = Brushes.White,
            TabIndex = 0,
            IsTabStop = true,
            Tag = column.Id
        };

        _onFilterTextBoxCreated?.Invoke(column.Id, textBox);

        textBox.GotFocus += (sender, args) =>
        {
            if (sender is TextBox tb)
            {
                _onFilterFocus?.Invoke(tb);
            }
        };

        textBox.PointerPressed += (sender, args) =>
        {
            if (sender is TextBox tb && !tb.IsFocused)
            {
                tb.Focus();
            }
        };

        textBox.TextChanged += (sender, args) =>
        {
            if (sender is TextBox tb)
            {
                var newValue = string.IsNullOrWhiteSpace(tb.Text) ? (object?)null : tb.Text;
                var currentValue = saGrid.State.ColumnFilters?.Filters
                    .FirstOrDefault(f => f.Id == column.Id)?.Value;

                var equals = (currentValue == null && newValue == null) ||
                             (currentValue != null && newValue != null &&
                              string.Equals(currentValue.ToString(), newValue.ToString(), StringComparison.Ordinal));

                if (!equals)
                {
                    saGrid.SetColumnFilter(column.Id, newValue);
                }
            }
        };

        var currentFilter = saGrid.State.ColumnFilters?.Filters.FirstOrDefault(f => f.Id == column.Id)?.Value?.ToString();
        if (!string.IsNullOrEmpty(currentFilter) && textBox.Text != currentFilter)
        {
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
            var arrow = column.SortDirection == SortDirection.Ascending ? "▲" : "▼";
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

    private void CleanupInteractivity()
    {
        if (_activeDragSources.Count > 0 && _dragDropManager != null)
        {
            foreach (var source in _activeDragSources)
            {
                _dragDropManager.UnregisterDragSource(source);
            }
            _activeDragSources.Clear();
        }
        else if (_activeDragSources.Count > 0)
        {
            _activeDragSources.Clear();
        }

        if (_activeDropZones.Count > 0 && _dragDropManager != null)
        {
            foreach (var zone in _activeDropZones)
            {
                _dragDropManager.UnregisterDropZone(zone);
            }
            _activeDropZones.Clear();
        }
        else if (_activeDropZones.Count > 0)
        {
            _activeDropZones.Clear();
        }
    }
}
