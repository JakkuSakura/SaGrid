using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using SaGrid.Core;

namespace SaGrid;

internal class SaGridBodyRenderer<TData>
{
    private readonly SaGridCellRenderer<TData> _cellRenderer;

    public SaGridBodyRenderer()
    {
        _cellRenderer = new SaGridCellRenderer<TData>();
    }

    public Control CreateBody(SaGrid<TData> saGrid, Func<SaGrid<TData>>? gridSignalGetter = null, Func<int>? selectionSignalGetter = null)
    {
        var initialRows = FlattenRows(saGrid.RowModel.Rows).ToList();
        return new VirtualizedRowsControl(saGrid, initialRows, _cellRenderer, gridSignalGetter, selectionSignalGetter);
    }

    private static IEnumerable<Row<TData>> FlattenRows(IReadOnlyList<Row<TData>> rows)
    {
        foreach (var row in rows)
        {
            yield return row;

            if (row.SubRows.Count > 0)
            {
                var children = row.SubRows
                    .OfType<Row<TData>>()
                    .ToList();

                foreach (var child in FlattenRows(children))
                {
                    yield return child;
                }
            }
        }
    }

    private sealed class VirtualizedRowsControl : ContentControl
    {
        private const double RowHeight = 30;
        private const int Overscan = 4;

        private readonly SaGrid<TData> _grid;
        private readonly SaGridCellRenderer<TData> _cellRenderer;
        private readonly Func<SaGrid<TData>>? _gridSignalGetter;
        private readonly Func<int>? _selectionSignalGetter;
        private readonly ScrollViewer _scrollViewer;
        private readonly Canvas _canvas;
        private readonly IReadOnlyList<Row<TData>> _initialRows;
        private bool _hasRealRows;
        private int _lastKnownRowCount = -1;

        public VirtualizedRowsControl(
            SaGrid<TData> grid,
            IReadOnlyList<Row<TData>> initialRows,
            SaGridCellRenderer<TData> cellRenderer,
            Func<SaGrid<TData>>? gridSignalGetter,
            Func<int>? selectionSignalGetter)
        {
            _grid = grid;
            _initialRows = initialRows;
            _cellRenderer = cellRenderer;
            _gridSignalGetter = gridSignalGetter;
            _selectionSignalGetter = selectionSignalGetter;

            _canvas = new Canvas();
            _scrollViewer = new ScrollViewer
            {
                Focusable = false,
                Content = _canvas
            };

            Content = _scrollViewer;

            _scrollViewer.PropertyChanged += ScrollViewerOnPropertyChanged;
            AttachedToVisualTree += OnAttached;
            DetachedFromVisualTree += OnDetached;
        }

        private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            _grid.RowDataChanged += OnRowDataChanged;
            _ = _grid.EnsureDataRangeAsync(0, _grid.GetPreferredFetchSize());
            UpdateViewport(force: true);
        }

        private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            _grid.RowDataChanged -= OnRowDataChanged;
        }

        private void OnRowDataChanged(object? sender, EventArgs e)
        {
            Dispatcher.UIThread.Post(() => UpdateViewport(force: true));
        }

        private void ScrollViewerOnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == ScrollViewer.OffsetProperty || e.Property == ScrollViewer.ViewportProperty)
            {
                UpdateViewport();
            }
        }

        private void UpdateViewport(bool force = false)
        {
            var rowCount = _grid.GetApproximateRowCount();
            var totalRows = Math.Max(0, rowCount);

            if (totalRows == 0 && !_hasRealRows && _initialRows.Count > 0)
            {
                totalRows = _initialRows.Count;
            }

            if (totalRows != _lastKnownRowCount)
            {
                force = true;
                _lastKnownRowCount = totalRows;
            }

            var viewport = _scrollViewer.Viewport;
            var viewportHeight = viewport.Height;
            if (double.IsNaN(viewportHeight) || viewportHeight <= 0)
            {
                viewportHeight = _scrollViewer.Bounds.Height;
            }
            if (double.IsNaN(viewportHeight) || viewportHeight <= 0)
            {
                viewportHeight = RowHeight * 20; // sensible default when layout hasn't measured yet
            }

            if (totalRows == 0)
            {
                _canvas.Children.Clear();
                _canvas.Height = 0;
                return;
            }

            if (rowCount > 0)
            {
                _hasRealRows = true;
            }

            _canvas.Height = totalRows * RowHeight;

            var verticalOffset = _scrollViewer.Offset.Y;
            var startIndex = Math.Max(0, (int)(verticalOffset / RowHeight) - Overscan);
            var maxVisible = (int)Math.Ceiling(viewportHeight / RowHeight) + (Overscan * 2);
            if (maxVisible <= 0)
            {
                maxVisible = Overscan * 2 + _grid.GetPreferredFetchSize();
            }
            var endIndex = Math.Min(totalRows, startIndex + Math.Max(maxVisible, 0));

            if (endIndex <= startIndex)
            {
                endIndex = Math.Min(totalRows, startIndex + _grid.GetPreferredFetchSize());
            }

            _canvas.Children.Clear();

            for (var rowIndex = startIndex; rowIndex < endIndex; rowIndex++)
            {
                var row = _grid.TryGetDisplayedRow(rowIndex) ?? GetFallbackRow(rowIndex);
                if (row == null)
                {
                    _ = _grid.EnsureDataRangeAsync(rowIndex, rowIndex + _grid.GetPreferredFetchSize());
                    var placeholder = CreatePlaceholderRow();
                    Canvas.SetTop(placeholder, rowIndex * RowHeight);
                    _canvas.Children.Add(placeholder);
                    continue;
                }

                var rowControl = CreateRowControl(row);
                Canvas.SetTop(rowControl, rowIndex * RowHeight);
                _canvas.Children.Add(rowControl);
            }
        }

        private Row<TData>? GetFallbackRow(int index)
        {
            if (!_hasRealRows && index >= 0 && index < _initialRows.Count)
            {
                return _initialRows[index];
            }

            return null;
        }

        private Control CreateRowControl(Row<TData> row)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            foreach (var column in _grid.VisibleLeafColumns)
            {
                var control = _gridSignalGetter != null
                    ? _cellRenderer.CreateReactiveCell(_grid, row, column, _gridSignalGetter, _selectionSignalGetter)
                    : _cellRenderer.CreateCell(_grid, row, column);

                panel.Children.Add(control);
            }

            return panel;
        }

        private static Control CreatePlaceholderRow()
        {
            return new Border
            {
                Height = RowHeight,
                Background = Brushes.Transparent
            };
        }
    }
}
