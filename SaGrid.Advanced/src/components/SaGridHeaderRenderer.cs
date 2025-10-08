using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Declarative;
using Avalonia.Media;
using SaGrid.Advanced.Components;
using SaGrid.Advanced.DragDrop;
using SaGrid.Advanced.Interactive;
using SaGrid.Core;
using SaGrid.Core.Models;
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

    public Control CreateHeader(
        ISaGridComponentHost<TData> host,
        Table<TData> table,
        Func<ISaGridComponentHost<TData>>? hostSignalGetter = null,
        Func<int>? selectionSignalGetter = null)
    {
        _ = hostSignalGetter;
        _ = selectionSignalGetter;

        CleanupInteractivity();

        var headerControls = new List<Control>();

        if (host.Options.EnableGrouping || host.GetGroupedColumnIds().Count > 0)
        {
            var groupingArea = CreateGroupingArea(host);
            if (groupingArea != null)
            {
                headerControls.Add(groupingArea);
            }
        }

        foreach (var headerGroup in host.HeaderGroups)
        {
            var headerRow = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            foreach (var header in headerGroup.Headers)
            {
                var column = (Column<TData>)header.Column;
                var headerCell = CreateHeaderCell(host, column, header);
                headerRow.Children.Add(headerCell);
            }

            if (headerRow.Children.Count > 0 && HasInteractivity)
            {
                var dropZone = new ColumnDropZone<TData>(headerRow, _columnService!, table, _dragDropManager!.RootVisual);
                _dragDropManager.RegisterDropZone(dropZone);
                _activeDropZones.Add(dropZone);
            }

            headerControls.Add(headerRow);
        }

        if (host.Options.EnableColumnFilters)
        {
            headerControls.Add(CreateFilterRow(host));
        }

        return new StackPanel()
            .Orientation(Orientation.Vertical)
            .Children(headerControls.ToArray());
    }

    private Control CreateHeaderCell(ISaGridComponentHost<TData> host, Column<TData> column, IHeader<TData> header)
    {
        return HasInteractivity
            ? CreateInteractiveHeaderCell(host, column, header)
            : CreateBasicHeaderCell(host, column, header);
    }

    private Control? CreateGroupingArea(ISaGridComponentHost<TData> host)
    {
        var groupedIds = host.GetGroupedColumnIds();

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
                if (host.GetColumn(columnId) is not Column<TData> column)
                {
                    continue;
                }

                var chip = CreateGroupingChip(host, column);
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
            var dropZone = new GroupingDropZone<TData>(panel, host, _dragDropManager!.RootVisual);
            _dragDropManager.RegisterDropZone(dropZone);
            _activeDropZones.Add(dropZone);
        }

        return container;
    }

    private Control CreateGroupingChip(ISaGridComponentHost<TData> host, Column<TData> column)
    {
        var text = column.ColumnDef.Header?.ToString() ?? column.Id;

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
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        ToolTip.SetTip(closeButton, $"Remove grouping by {text}");

        closeButton.Click += (s, e) => host.RemoveGroupingColumn(column.Id);

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

        chip.PointerEntered += (_, _) =>
        {
            chip.Background = new SolidColorBrush(Colors.LightSkyBlue, 0.95);
        };

        chip.PointerExited += (_, _) =>
        {
            chip.Background = new SolidColorBrush(Colors.LightSteelBlue, 0.9);
        };

        return chip;
    }

    private Control CreateBasicHeaderCell(ISaGridComponentHost<TData> host, Column<TData> column, IHeader<TData> header)
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

        border.Child = CreateMainHeaderButton(host, column, header);

        return border;
    }

    private Control CreateInteractiveHeaderCell(ISaGridComponentHost<TData> host, Column<TData> column, IHeader<TData> header)
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

        var mainButton = CreateMainHeaderButton(host, column, header);
        Grid.SetColumn(mainButton, 0);
        headerGrid.Children.Add(mainButton);

        if (_dragDropManager != null && _columnService != null)
        {
            var dragSource = new ColumnDragSource<TData>(column, mainButton, _columnService);
            _dragDropManager.RegisterDragSource(dragSource);
            _activeDragSources.Add(dragSource);

            var resizeHandle = CreateResizeHandle(host, column, header);
            Grid.SetColumn(resizeHandle, 1);
            headerGrid.Children.Add(resizeHandle);
        }

        border.Child = headerGrid;
        return border;
    }

    private Button CreateMainHeaderButton(ISaGridComponentHost<TData> host, Column<TData> column, IHeader<TData> header)
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
            Content = CreateHeaderLabel(host, column, header)
        };

        SetupSortingBehavior(button, host, column);
        return button;
    }

    private Control CreateResizeHandle(ISaGridComponentHost<TData> host, Column<TData> column, IHeader<TData> header)
    {
        var resizeHandle = new Border()
            .Width(4)
            .Background(Brushes.Transparent)
            .Cursor(new Cursor(StandardCursorType.SizeWestEast))
            .VerticalAlignment(VerticalAlignment.Stretch);

        double? dragStartX = null;
        double startWidth = 0;
        var isResizing = false;
        Column<TData>? neighbourColumn = null;

        resizeHandle.PointerEntered += (_, _) =>
        {
            resizeHandle.Background = new SolidColorBrush(Colors.Blue, 0.3);
        };

        resizeHandle.PointerExited += (_, _) =>
        {
            resizeHandle.Background = Brushes.Transparent;
        };

        resizeHandle.PointerPressed += (_, e) =>
        {
            var point = e.GetCurrentPoint(resizeHandle);
            if (point.Properties.IsLeftButtonPressed)
            {
                dragStartX = point.Position.X;
                startWidth = header.Size;
                isResizing = true;
                neighbourColumn = host.VisibleLeafColumns
                    .SkipWhile(c => c.Id != column.Id)
                    .Skip(1)
                    .Cast<Column<TData>?>()
                    .FirstOrDefault();
                e.Pointer.Capture(resizeHandle);
                e.Handled = true;
            }
        };

        resizeHandle.PointerMoved += (_, e) =>
        {
            if (isResizing && dragStartX.HasValue && ReferenceEquals(e.Pointer.Captured, resizeHandle))
            {
                var current = e.GetCurrentPoint(resizeHandle).Position.X;
                var delta = current - dragStartX.Value;
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && neighbourColumn != null)
                {
                    _columnService?.ResizeColumnPair(column.Id, neighbourColumn.Id, delta);
                }
                else
                {
                    var newWidth = startWidth + delta;
                    _columnService?.SetColumnWidth(column.Id, newWidth);
                }
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

        resizeHandle.PointerReleased += (_, e) =>
        {
            EndResize(e.Pointer);
            e.Handled = true;
        };

        resizeHandle.PointerCaptureLost += (_, _) =>
        {
            isResizing = false;
            dragStartX = null;
            startWidth = 0;
        };

        resizeHandle.DoubleTapped += (_, _) =>
        {
            _columnService?.AutoSizeColumn(column.Id);
        };

        return resizeHandle;
    }

    private void SetupSortingBehavior(Button button, ISaGridComponentHost<TData> host, Column<TData> column)
    {
        void ApplySorting(bool multi)
        {
            if (multi && host.IsMultiSortEnabled())
            {
                var currentDir = column.SortDirection;
                var current = host.State.Sorting?.Columns ?? new List<ColumnSort>();

                if (currentDir == null)
                {
                    var newList = current.ToList();
                    newList.Add(new ColumnSort(column.Id, SortDirection.Ascending));
                    host.SetSorting(newList);
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

                    host.SetSorting(newList);
                }
                else
                {
                    var newList = current.Where(s => s.Id != column.Id).ToList();
                    host.SetSorting(newList);
                }
            }
            else
            {
                var dir = column.SortDirection;
                if (dir == null)
                {
                    host.SetSorting(new[] { new ColumnSort(column.Id, SortDirection.Ascending) });
                }
                else if (dir == SortDirection.Ascending)
                {
                    host.SetSorting(new[] { new ColumnSort(column.Id, SortDirection.Descending) });
                }
                else
                {
                    host.SetSorting(Array.Empty<ColumnSort>());
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

        button.Click += (_, _) => ApplySorting(false);
    }

    private Control CreateFilterRow(ISaGridComponentHost<TData> host)
    {
        var filterControls = host.VisibleLeafColumns.Select(column =>
        {
            var textBox = CreateFilterTextBox(host, column);
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

    private TextBox CreateFilterTextBox(ISaGridComponentHost<TData> host, Column<TData> column)
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

        textBox.GotFocus += (_, args) =>
        {
            if (args.Source is TextBox tb)
            {
                _onFilterFocus?.Invoke(tb);
            }
        };

        textBox.PointerPressed += (sender, _) =>
        {
            if (sender is TextBox tb && !tb.IsFocused)
            {
                tb.Focus();
            }
        };

        textBox.TextChanged += (sender, _) =>
        {
            if (sender is TextBox tb)
            {
                var newValue = string.IsNullOrWhiteSpace(tb.Text) ? (object?)null : tb.Text;
                var currentValue = host.State.ColumnFilters?.Filters
                    .FirstOrDefault(f => f.Id == column.Id)?.Value;

                var equals = (currentValue == null && newValue == null) ||
                             (currentValue != null && newValue != null &&
                              string.Equals(currentValue.ToString(), newValue.ToString(), StringComparison.Ordinal));

                if (!equals)
                {
                    host.SetColumnFilter(column.Id, newValue);
                }
            }
        };

        var currentFilterValue = host.State.ColumnFilters?.Filters.FirstOrDefault(f => f.Id == column.Id)?.Value;
        if (currentFilterValue is string textValue)
        {
            if (!string.Equals(textBox.Text, textValue, StringComparison.Ordinal))
            {
                textBox.Text = textValue;
            }
        }
        else
        {
            textBox.Text = string.Empty;
        }

        return textBox;
    }

    private Control CreateHeaderLabel(ISaGridComponentHost<TData> host, Column<TData> column, IHeader<TData> header)
    {
        var title = SaGridContentHelper<TData>.GetHeaderContent(header);
        var sortSuffix = string.Empty;

        if (column.SortDirection != null)
        {
            var arrow = column.SortDirection == SortDirection.Ascending ? "▲" : "▼";
            var isMulti = host.IsMultiSortEnabled();
            var index = (isMulti && column.SortIndex.HasValue)
                ? $" {column.SortIndex.Value + 1}"
                : string.Empty;
            sortSuffix = $" {arrow}{index}";
        }

        var container = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var textBlock = new TextBlock
        {
            Text = title + sortSuffix,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(8, 0, 0, 0)
        };

        container.Children.Add(textBlock);

        return container;
    }

    private void CleanupInteractivity()
    {
        foreach (var dragSource in _activeDragSources)
        {
            _dragDropManager?.UnregisterDragSource(dragSource);
        }
        _activeDragSources.Clear();

        foreach (var dropZone in _activeDropZones)
        {
            _dragDropManager?.UnregisterDropZone(dropZone);
        }
        _activeDropZones.Clear();
    }
}
