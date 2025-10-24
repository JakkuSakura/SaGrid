using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using SaGrid.Core.Models;

namespace SaGrid.Avalonia;

public class TableHeaderRenderer<TData>
{
    private const double HeaderHeight = 40;
    private const double FilterHeight = 35;
    private const double ResizeHandleWidth = 8d;

    private TableColumnLayoutManager<TData>? _currentLayoutManager;
    private readonly Dictionary<Thumb, ResizeSession> _resizeSessions = new();
    private readonly record struct ResizeSession(string ColumnId, IDisposable? Scope, double BaseWidth, double AccumDelta);

    public Control CreateHeader(Table<TData> table)
    {
        var layoutManager = TableColumnLayoutManagerRegistry.GetOrCreate(table);
        return CreateHeader(table, layoutManager);
    }

    public Control CreateHeader(Table<TData> table, TableColumnLayoutManager<TData> layoutManager)
    {
        _currentLayoutManager = layoutManager;

        var container = new StackPanel { Orientation = Orientation.Vertical };

        foreach (var headerGroup in table.HeaderGroups)
        {
            var rowPanel = layoutManager.CreatePanel();
            rowPanel.Height = HeaderHeight;

            foreach (var header in headerGroup.Headers)
            {
                var column = (Column<TData>)header.Column;
                var headerCell = CreateHeaderCell(table, column, header);

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

                rowPanel.Children.Add(headerCell);
            }

            container.Children.Add(rowPanel);
        }

        if (table.Options.EnableColumnFilters)
        {
            container.Children.Add(CreateFilterRow(table, layoutManager));
        }

        return container;
    }

    private Control CreateHeaderCell(Table<TData> table, Column<TData> column, IHeader<TData> header)
    {
        var border = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = Brushes.LightGray,
            Background = Brushes.LightBlue,
            Padding = new Thickness(0),
            Height = HeaderHeight,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var headerDock = new DockPanel
        {
            LastChildFill = true
        };

        var resizeRail = CreateResizeRail(table, column);
        DockPanel.SetDock(resizeRail, Dock.Right);
        headerDock.Children.Add(resizeRail);

        var contentHost = new Border
        {
            Background = Brushes.Transparent,
            Padding = new Thickness(8, 0, 8, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var content = CreateHeaderContent(column, header, out var sortIndicator);
        contentHost.Child = content;

        headerDock.Children.Add(contentHost);

        border.Child = headerDock;

        AttachSortingInteraction(contentHost, table, column, sortIndicator);

        return border;
    }

    private Control CreateHeaderContent(
        Column<TData> column,
        IHeader<TData> header,
        out TextBlock sortIndicator)
    {
        var layout = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 4
        };

        var title = new TextBlock
        {
            Text = TableContentHelper<TData>.GetHeaderContent(header),
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };

        sortIndicator = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0)
        };

        stack.Children.Add(title);
        stack.Children.Add(sortIndicator);

        Grid.SetColumn(stack, 1);
        layout.Children.Add(stack);

        UpdateSortIndicator(column, sortIndicator);

        return layout;
    }

    private static void UpdateSortIndicator(Column<TData> column, TextBlock sortIndicator)
    {
        var sortText = column.SortDirection switch
        {
            SortDirection.Ascending => "▲",
            SortDirection.Descending => "▼",
            _ => string.Empty
        };

        sortIndicator.Text = sortText;
        sortIndicator.IsVisible = !string.IsNullOrEmpty(sortText);
    }

    private static void ToggleSorting(Table<TData> table, Column<TData> column)
    {
        var currentDirection = column.SortDirection;

        if (currentDirection == null)
        {
            table.SetSorting(column.Id, SortDirection.Ascending);
        }
        else if (currentDirection == SortDirection.Ascending)
        {
            table.SetSorting(column.Id, SortDirection.Descending);
        }
        else
        {
            table.SetSorting(Array.Empty<ColumnSort>());
        }
    }

    private Control CreateResizeRail(Table<TData> table, Column<TData> column)
    {
        var rail = new Grid
        {
            Width = column.CanResize && !column.Columns.Any() ? ResizeHandleWidth : 1,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = Brushes.Transparent,
            IsHitTestVisible = column.CanResize && !column.Columns.Any()
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

        if (!(column.CanResize && !column.Columns.Any()))
        {
            return rail;
        }

        var thumb = new Thumb
        {
            Cursor = new Cursor(StandardCursorType.SizeWestEast),
            Background = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        thumb.SetValue(Panel.ZIndexProperty, 1);
        rail.Children.Add(thumb);

        void ResetLine() => line.Background = Brushes.LightGray;

        thumb.PointerEntered += (_, _) => line.Background = new SolidColorBrush(Colors.DodgerBlue);
        thumb.PointerExited += (_, _) => ResetLine();
        thumb.PointerCaptureLost += (_, _) => EndResize(thumb, resetLine: true);

        thumb.DragStarted += (_, _) => BeginResize(thumb, column);
        thumb.DragDelta += (_, e) => OnResizeDelta(thumb, column, e);
        thumb.DragCompleted += (_, _) => EndResize(thumb, resetLine: true);

        thumb.DoubleTapped += (_, e) =>
        {
            column.ResetSize();
            _currentLayoutManager?.Refresh();
            ResetLine();
            e.Handled = true;
        };

        return rail;
    }

    private void BeginResize(Thumb thumb, Column<TData> column)
    {
        if (_currentLayoutManager == null)
        {
            return;
        }

        var baseWidth = _currentLayoutManager.Snapshot.GetWidth(column.Id);
        var scope = _currentLayoutManager.BeginUserResize(column.Id);
        _resizeSessions[thumb] = new ResizeSession(column.Id, scope, baseWidth, 0);
    }

    private void OnResizeDelta(Thumb thumb, Column<TData> column, VectorEventArgs e)
    {
        if (_currentLayoutManager == null)
        {
            return;
        }

        if (!_resizeSessions.TryGetValue(thumb, out var session))
        {
            BeginResize(thumb, column);
            session = _resizeSessions.GetValueOrDefault(thumb);
        }

        var newAccum = session.AccumDelta + e.Vector.X;

        var min = column.ColumnDef.MinSize.HasValue ? Math.Max(column.ColumnDef.MinSize.Value, 1) : 40;
        var max = column.ColumnDef.MaxSize.HasValue ? Math.Max(column.ColumnDef.MaxSize.Value, min) : double.PositiveInfinity;

        var target = session.BaseWidth + newAccum;
        if (!double.IsPositiveInfinity(max)) target = Math.Min(target, max);
        target = Math.Max(target, min);

        column.SetSize(target);
        _currentLayoutManager.Refresh();

        _resizeSessions[thumb] = session with { AccumDelta = newAccum };
        e.Handled = true;
    }

    private void EndResize(Thumb thumb, bool resetLine)
    {
        if (_resizeSessions.TryGetValue(thumb, out var session))
        {
            session.Scope?.Dispose();
            _resizeSessions.Remove(thumb);
        }
        if (resetLine)
        {
            // No-op here; caller handles visual line reset.
        }
    }

    private void AttachSortingInteraction(
        Control target,
        Table<TData> table,
        Column<TData> column,
        TextBlock sortIndicator)
    {
        if (!column.CanSort)
        {
            return;
        }

        target.Cursor = new Cursor(StandardCursorType.Hand);

        var pressed = false;

        target.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(target).Properties.IsLeftButtonPressed)
            {
                pressed = true;
            }
        };

        target.PointerReleased += (_, e) =>
        {
            if (!pressed || e.InitialPressMouseButton != MouseButton.Left)
            {
                pressed = false;
                return;
            }

            pressed = false;

            ToggleSorting(table, column);
            UpdateSortIndicator(column, sortIndicator);
            e.Handled = true;
        };

        target.PointerCaptureLost += (_, __) => pressed = false;
        target.PointerExited += (_, __) => pressed = false;
    }

    private Control CreateFilterRow(Table<TData> table, TableColumnLayoutManager<TData> layoutManager)
    {
        var rowPanel = layoutManager.CreatePanel();
        rowPanel.Height = FilterHeight;

        foreach (var column in table.VisibleLeafColumns)
        {
            var border = new Border
            {
                BorderThickness = new Thickness(0, 0, 1, 1),
                BorderBrush = Brushes.LightGray,
                Background = Brushes.White,
                Padding = new Thickness(4)
            };

            var textBox = new TextBox
            {
                Watermark = $"Filter {column.Id}...",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            textBox.Text = column.FilterValue?.ToString() ?? string.Empty;
            textBox.TextChanged += (_, __) =>
            {
                column.SetFilterValue(string.IsNullOrEmpty(textBox.Text) ? null : textBox.Text);
            };

            textBox.KeyDown += (_, args) =>
            {
                if (args.Key == Key.Enter)
                {
                    table.SetState(table.State);
                }
            };

            border.Child = textBox;
            ColumnLayoutPanel.SetColumnId(border, column.Id);
            rowPanel.Children.Add(border);
        }

        return rowPanel;
    }
}
