using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Declarative;
using Avalonia.Media;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using SaGrid.Avalonia;
using SaGrid.Advanced.Components;
using SaGrid.Advanced.DragDrop;
using SaGrid.Advanced.Interactive;
using SaGrid.Core;
using SaGrid.Core.Models;
using static SolidAvalonia.Solid;

namespace SaGrid;

internal class SaGridHeaderRenderer<TData>
{
    private const double HeaderHeight = 40;
    private const double ResizeHandleWidth = 6;

    private readonly Action<TextBox>? _onFilterFocus;
    private readonly Action<string, TextBox>? _onFilterTextBoxCreated;
    private DragDropManager<TData>? _dragDropManager;
    private ColumnInteractiveService<TData>? _columnService;
    private readonly List<IDragSource> _activeDragSources = new();
    private readonly List<IDropZone> _activeDropZones = new();
    private TableColumnLayoutManager<TData>? _layoutManager;

    private bool HasInteractivity => _dragDropManager != null && _columnService != null;

    private static Column<TData>? FindRightNeighbour(Table<TData> table, Column<TData> column)
    {
        return table.VisibleLeafColumns
            .Cast<Column<TData>>()
            .SkipWhile(c => c.Id != column.Id)
            .Skip(1)
            .OfType<Column<TData>>()
            .FirstOrDefault(c => c.CanResize);
    }

    private static bool HasRightResizeTarget(Table<TData> table, Column<TData> column)
    {
        return FindRightNeighbour(table, column) != null;
    }

    private static bool IsStarColumn(Column<TData> column)
    {
        return column.ColumnDef.Width?.Mode == ColumnWidthMode.Star;
    }

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
        TableColumnLayoutManager<TData> layoutManager,
        Func<ISaGridComponentHost<TData>>? hostSignalGetter = null,
        Func<int>? selectionSignalGetter = null)
    {
        _ = hostSignalGetter;
        _ = selectionSignalGetter;

        CleanupInteractivity();
        _layoutManager = layoutManager;

        var headerControls = new List<Control>();

        if (table.Options.EnableGrouping || host.GetGroupedColumnIds().Count > 0)
        {
            var groupingArea = CreateGroupingArea(host);
            if (groupingArea != null)
            {
                headerControls.Add(groupingArea);
            }
        }

        foreach (var headerGroup in table.HeaderGroups)
        {
            var headerRow = layoutManager.CreatePanel();
            headerRow.Height = HeaderHeight;

            foreach (var header in headerGroup.Headers)
            {
                var column = (Column<TData>)header.Column;
                var headerCell = CreateHeaderCell(host, table, column, header);

                if (header.SubHeaders.Count > 0)
                {
                    var spanIds = column.LeafColumns
                        .OfType<Column<TData>>()
                        .Where(c => c.IsVisible)
                        .Select(c => c.Id)
                        .ToArray();

                    if (spanIds.Length > 0)
                    {
                        ColumnLayoutPanel.SetColumnSpan(headerCell, spanIds);
                    }
                    else
                    {
                        ColumnLayoutPanel.SetColumnId(headerCell, column.Id);
                    }
                }
                else
                {
                    ColumnLayoutPanel.SetColumnId(headerCell, column.Id);
                }

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

        if (table.Options.EnableColumnFilters)
        {
            headerControls.Add(CreateFilterRow(host, table, layoutManager));
        }

        return new StackPanel()
            .Orientation(Orientation.Vertical)
            .Children(headerControls.ToArray());
    }

    private Control CreateHeaderCell(ISaGridComponentHost<TData> host, Table<TData> table, Column<TData> column, IHeader<TData> header)
    {
        return HasInteractivity
            ? CreateInteractiveHeaderCell(host, table, column, header)
            : CreateBasicHeaderCell(host, table, column, header);
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

    private Control CreateBasicHeaderCell(ISaGridComponentHost<TData> host, Table<TData> table, Column<TData> column, IHeader<TData> header)
    {
        var border = new Border()
            .BorderThickness(0, 0, 0, 1)
            .BorderBrush(Brushes.LightGray)
            .Background(Brushes.LightBlue)
            .Padding(new Thickness(0))
            .Height(HeaderHeight)
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .VerticalAlignment(VerticalAlignment.Stretch);

        var dock = new DockPanel { LastChildFill = true };

        var canResize = column.CanResize && HasRightResizeTarget(table, column);
        var resizeRail = CreateResizeRail(table, column, border, enableResize: canResize);
        DockPanel.SetDock(resizeRail, Dock.Right);
        dock.Children.Add(resizeRail);

        var mainButton = CreateMainHeaderButton(host, table, column, header);
        dock.Children.Add(mainButton);

        border.Child = dock;

        return border;
    }

    private Control CreateInteractiveHeaderCell(ISaGridComponentHost<TData> host, Table<TData> table, Column<TData> column, IHeader<TData> header)
    {
        var border = new Border()
            .BorderThickness(0, 0, 0, 1)
            .BorderBrush(Brushes.LightGray)
            .Background(Brushes.LightBlue)
            .Padding(new Thickness(0))
            .Height(HeaderHeight)
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .VerticalAlignment(VerticalAlignment.Stretch);

        var headerDock = new DockPanel { LastChildFill = true };

        var mainButton = CreateMainHeaderButton(host, table, column, header);

        if (_dragDropManager != null && _columnService != null)
        {
            var dragSource = new ColumnDragSource<TData>(column, mainButton, _columnService);
            _dragDropManager.RegisterDragSource(dragSource);
            _activeDragSources.Add(dragSource);
        }

        var canResize = column.CanResize && HasRightResizeTarget(table, column);
        var resizeRail = CreateResizeRail(table, column, border, enableResize: canResize);
        DockPanel.SetDock(resizeRail, Dock.Right);
        headerDock.Children.Add(resizeRail);

        headerDock.Children.Add(mainButton);

        border.Child = headerDock;
        return border;
    }

    private Button CreateMainHeaderButton(ISaGridComponentHost<TData> host, Table<TData> table, Column<TData> column, IHeader<TData> header)
    {
        var button = new Button
        {
            Background = Brushes.Transparent,
            BorderBrush = null,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Focusable = false,
            IsTabStop = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Cursor = new Cursor(StandardCursorType.Hand),
            Content = CreateHeaderLabel(table, column, header)
        };

        SetupSortingBehavior(button, host, table, column);
        return button;
    }

    private Control CreateResizeRail(Table<TData> table, Column<TData> column, Border headerBorder, bool enableResize)
    {
        var service = _columnService;
        var canResize = enableResize && service != null && column.CanResize && !column.Columns.Any() && HasRightResizeTarget(table, column);

        var rail = new Grid
        {
            Width = canResize ? ResizeHandleWidth : 1,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = Brushes.Transparent,
            IsHitTestVisible = canResize,
            Cursor = canResize ? new Cursor(StandardCursorType.SizeWestEast) : new Cursor(StandardCursorType.Arrow)
        };

        rail.SetValue(Panel.ZIndexProperty, 1);

        var line = new Border
        {
            Width = 1,
            Background = new SolidColorBrush(Colors.Gray),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        rail.Children.Add(line);

        if (!canResize)
        {
            return rail;
        }

        bool isDragging = false;
        double lastPointerX = 0;
        Column<TData>? neighbourColumn = null;
        IDisposable? resizeScope = null;
        Visual? dragReferenceVisual = null;

        void ResetLine()
        {
            line.Background = Brushes.LightGray;
        }

        void CompleteResizeSession()
        {
            resizeScope?.Dispose();
            resizeScope = null;
        }

        rail.PointerEntered += (_, _) =>
        {
            line.Background = new SolidColorBrush(Colors.DodgerBlue);
        };

        rail.PointerExited += (_, _) =>
        {
            if (!isDragging)
            {
                ResetLine();
            }
        };

        rail.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(rail).Properties.IsLeftButtonPressed)
            {
                return;
            }

            dragReferenceVisual = (headerBorder.GetVisualRoot() as Visual) ?? rail;
            lastPointerX = e.GetPosition(dragReferenceVisual).X;
            neighbourColumn = FindRightNeighbour(table, column);

            if (neighbourColumn == null)
            {
                return;
            }

            isDragging = true;
            resizeScope ??= _layoutManager?.BeginUserResize(column.Id);

            e.Pointer.Capture(rail);
            e.Handled = true;
        };

        rail.PointerMoved += (_, e) =>
        {
            if (!isDragging)
            {
                return;
            }

            var visual = dragReferenceVisual ?? (headerBorder.GetVisualRoot() as Visual) ?? rail;
            var currentX = e.GetPosition(visual).X;
            var delta = currentX - lastPointerX;
            if (Math.Abs(delta) < 0.1)
            {
                return;
            }

            lastPointerX = currentX;

            var applied = false;

            if (service != null && neighbourColumn != null)
            {
                applied = service.ResizeColumnPair(column.Id, neighbourColumn.Id, delta);
            }

            if (applied)
            {
                _layoutManager?.Refresh();
            }

            e.Handled = true;
        };

        void EndDrag(PointerEventArgs e)
        {
            if (!isDragging)
            {
                return;
            }

            isDragging = false;
            neighbourColumn = null;
            ResetLine();
            dragReferenceVisual = null;

            if (e.Pointer.Captured == rail)
            {
                e.Pointer.Capture(null);
            }

            CompleteResizeSession();
            _layoutManager?.Refresh();
            e.Handled = true;
        }

        rail.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Left)
            {
                EndDrag(e);
            }
        };

        rail.PointerCaptureLost += (_, _) =>
        {
            isDragging = false;
            neighbourColumn = null;
            ResetLine();
            dragReferenceVisual = null;
            CompleteResizeSession();
        };

        rail.DoubleTapped += (_, e) =>
        {
            if (service != null)
            {
                if (service.AutoSizeColumn(column.Id))
                {
                    _layoutManager?.Refresh();
                }
            }
            else
            {
                column.ResetSize();
                _layoutManager?.Refresh();
            }

            CompleteResizeSession();
            ResetLine();
            e.Handled = true;
        };

        return rail;
    }

    private void SetupSortingBehavior(Button button, ISaGridComponentHost<TData> host, Table<TData> table, Column<TData> column)
    {
        void ApplySorting(bool multi)
        {
            if (multi && host.IsMultiSortEnabled())
            {
                var currentDir = column.SortDirection;
                var current = table.State.Sorting?.Columns ?? new List<ColumnSort>();

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

    private Control CreateFilterRow(
        ISaGridComponentHost<TData> host,
        Table<TData> table,
        TableColumnLayoutManager<TData> layoutManager)
    {
        var panel = layoutManager.CreatePanel();
        panel.Height = 35;

        foreach (var column in table.VisibleLeafColumns.Cast<Column<TData>>())
        {
            var textBox = CreateFilterTextBox(host, table, column);
            var border = new Border()
                .BorderThickness(0, 0, 1, 1)
                .BorderBrush(Brushes.LightGray)
                .Background(Brushes.White)
                .Padding(new Thickness(2))
                .Child(textBox);

            ColumnLayoutPanel.SetColumnId(border, column.Id);
            panel.Children.Add(border);
        }

        return new Border()
            .BorderThickness(new Thickness(0, 0, 0, 1))
            .BorderBrush(Brushes.LightGray)
            .Child(panel);
    }

    private TextBox CreateFilterTextBox(ISaGridComponentHost<TData> host, Table<TData> table, Column<TData> column)
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
                var currentValue = table.State.ColumnFilters?.Filters
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

        var currentFilterValue = table.State.ColumnFilters?.Filters.FirstOrDefault(f => f.Id == column.Id)?.Value;
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

    private Control CreateHeaderLabel(Table<TData> table, Column<TData> column, IHeader<TData> header)
    {
        var title = SaGridContentHelper<TData>.GetHeaderContent(header);
        var sortSuffix = string.Empty;

        if (column.SortDirection != null)
        {
            var arrow = column.SortDirection == SortDirection.Ascending ? "▲" : "▼";
            var isMulti = table.State.Sorting?.Columns.Count > 1;
            var index = (isMulti && column.SortIndex.HasValue)
                ? $" {column.SortIndex.Value + 1}"
                : string.Empty;
            sortSuffix = $" {arrow}{index}";
        }

        return new TextBlock
        {
            Text = title + sortSuffix,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0)
        };
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
