using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using SaGrid.Advanced;
using SaGrid.Advanced.Interfaces;
using SaGrid.Core;

namespace SaGrid;

internal interface ISelectionAwareRowsControl
{
    void ApplySelectionDelta(CellSelectionDelta delta);
}

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

    private sealed class VirtualizedRowsControl : ContentControl, ISelectionAwareRowsControl
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
        private readonly Dictionary<string, RowControlContainer> _rowControlsById = new();

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
                foreach (var container in _rowControlsById.Values)
                {
                    container.Detach(_canvas);
                }

                _rowControlsById.Clear();
                _canvas.Children.Clear();
                _canvas.Height = 0;
                return;
            }

            if (rowCount > 0)
            {
                _hasRealRows = true;
            }

            _canvas.Height = totalRows * RowHeight;

            if (force)
            {
                PruneStaleControlsIfNeeded();
            }

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

            var visibleRowIds = new HashSet<string>();

            for (var rowIndex = startIndex; rowIndex < endIndex; rowIndex++)
            {
                var row = _grid.TryGetDisplayedRow(rowIndex) ?? GetFallbackRow(rowIndex);
                if (row == null)
                {
                    _ = _grid.EnsureDataRangeAsync(rowIndex, rowIndex + _grid.GetPreferredFetchSize());
                    continue;
                }

                var displayIndex = row.DisplayIndex >= 0 ? row.DisplayIndex : rowIndex;
                var rowId = row.Id ?? displayIndex.ToString();
                visibleRowIds.Add(rowId);

                var control = GetOrCreateControl(row, displayIndex, rowId);
                Canvas.SetTop(control, rowIndex * RowHeight);
            }

            CleanupObsoleteControls(visibleRowIds);
        }

        void ISelectionAwareRowsControl.ApplySelectionDelta(CellSelectionDelta delta)
        {
            if (delta.Added.Count == 0 && delta.Removed.Count == 0 && delta.ActiveCell == null)
            {
                return;
            }

            UpdateCells(delta.Removed, force: true);
            UpdateCells(delta.Added, force: true);

            if (delta.ActiveCell is { } active)
            {
                UpdateCellVisual(active, force: true);
            }
        }

        private void UpdateCells(IEnumerable<CellPosition> cells, bool force)
        {
            foreach (var cell in cells)
            {
                UpdateCellVisual(cell, force);
            }
        }

        private void UpdateCellVisual(CellPosition cell, bool force)
        {
            var row = _grid.TryGetDisplayedRow(cell.RowIndex);
            if (row == null)
            {
                return;
            }

            var displayIndex = row.DisplayIndex >= 0 ? row.DisplayIndex : cell.RowIndex;
            var rowId = row.Id ?? displayIndex.ToString();

            if (!_rowControlsById.TryGetValue(rowId, out var container))
            {
                return;
            }

            if (container.TryGetCell(cell.ColumnId, out var visual) && visual != null)
            {
                visual.Update(row, displayIndex, force);
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

        private Control GetOrCreateControl(Row<TData> row, int displayIndex, string rowId)
        {
            if (!_rowControlsById.TryGetValue(rowId, out var container))
            {
                container = CreateRowControl(row, displayIndex, rowId);
                _rowControlsById[rowId] = container;
            }

            container.Update(row, displayIndex);
            container.Attach(_canvas);
            container.Control.Tag = rowId;

            return container.Control;
        }

        private void CleanupObsoleteControls(HashSet<string> visibleRowIds)
        {
            if (_rowControlsById.Count == 0)
            {
                return;
            }

            foreach (var kvp in _rowControlsById)
            {
                if (!visibleRowIds.Contains(kvp.Key))
                {
                    kvp.Value.Detach(_canvas);
                }
            }
        }

        private void PruneStaleControlsIfNeeded()
        {
            if (!_hasRealRows || _rowControlsById.Count == 0)
            {
                return;
            }

            if (_grid.GetActiveRowModelType() == RowModelType.ServerSide)
            {
                // Server-side row models manage their own cache; don't prune eagerly.
                return;
            }

            var currentRowIds = _grid.RowModel.FlatRows.Select(r => r.Id).ToHashSet();
            if (currentRowIds.Count == 0)
            {
                return;
            }

            var staleIds = _rowControlsById.Keys
                .Where(id => !currentRowIds.Contains(id))
                .ToList();

            foreach (var staleId in staleIds)
            {
                if (_rowControlsById.TryGetValue(staleId, out var container))
                {
                    container.Detach(_canvas);
                }

                _rowControlsById.Remove(staleId);
            }
        }

        private RowControlContainer CreateRowControl(Row<TData> row, int displayIndex, string rowId)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            var cellVisuals = new List<IReusableCellVisual<TData>>();
            var cellVisualsByColumn = new Dictionary<string, IReusableCellVisual<TData>>();

            foreach (var column in _grid.VisibleLeafColumns)
            {
                var control = _gridSignalGetter != null
                    ? _cellRenderer.CreateReactiveCell(_grid, row, column, displayIndex, _gridSignalGetter, _selectionSignalGetter)
                    : _cellRenderer.CreateCell(_grid, row, column, displayIndex);

                if (control is IReusableCellVisual<TData> reusable)
                {
                    cellVisuals.Add(reusable);
                    cellVisualsByColumn[column.Id] = reusable;
                }

                panel.Children.Add(control);
            }

            panel.Tag = rowId;
            return new RowControlContainer(panel, cellVisuals, cellVisualsByColumn);
        }

        private sealed class RowControlContainer
        {
            private readonly Control _control;
            private readonly List<IReusableCellVisual<TData>> _cellVisuals;
            private readonly Dictionary<string, IReusableCellVisual<TData>> _cellVisualsByColumn;

            public RowControlContainer(
                Control control,
                List<IReusableCellVisual<TData>> cellVisuals,
                Dictionary<string, IReusableCellVisual<TData>> cellVisualsByColumn)
            {
                _control = control;
                _cellVisuals = cellVisuals;
                _cellVisualsByColumn = cellVisualsByColumn;
            }

            public Control Control => _control;
            public bool IsAttached { get; private set; }

            public void Attach(Canvas canvas)
            {
                if (IsAttached)
                {
                    return;
                }

                canvas.Children.Add(_control);
                IsAttached = true;
            }

            public void Detach(Canvas canvas)
            {
                if (!IsAttached)
                {
                    return;
                }

                canvas.Children.Remove(_control);
                IsAttached = false;
            }

            public void Update(Row<TData> row, int displayIndex, bool force = false)
            {
                for (var i = 0; i < _cellVisuals.Count; i++)
                {
                    _cellVisuals[i].Update(row, displayIndex, force);
                }
            }

            public bool TryGetCell(string columnId, out IReusableCellVisual<TData>? cell)
            {
                return _cellVisualsByColumn.TryGetValue(columnId, out cell);
            }
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
