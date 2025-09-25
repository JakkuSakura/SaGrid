using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SaGrid.Advanced.Interfaces;
using SaGrid.Advanced.Events;
using SaGrid.Core;

namespace SaGrid.Advanced.Modules.Filters;

internal sealed class SetFilterControl<TData> : UserControl
{
    private readonly SaGrid<TData> _grid;
    private readonly FilterService _filterService;
    private readonly string _columnId;
    private readonly StackPanel _valuesPanel;
    private readonly TextBlock _summaryText;
    private readonly ComboBox _operatorBox;
    private readonly TextBox _searchBox;
    private readonly List<CheckBox> _valueCheckBoxes = new();
    private readonly Action<ModelUpdatedEventArgs> _modelUpdatedHandler;
    private readonly Action<FilterChangedEventArgs> _filterChangedHandler;

    public SetFilterControl(SaGrid<TData> grid, FilterService filterService, string columnId)
    {
        _grid = grid;
        _filterService = filterService;
        _columnId = columnId;

        var layout = new DockPanel
        {
            LastChildFill = true
        };

        var header = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(0, 0, 0, 8)
        };

        _summaryText = new TextBlock
        {
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 0, 0, 4)
        };
        header.Children.Add(_summaryText);

        _operatorBox = new ComboBox
        {
            ItemsSource = Enum.GetValues(typeof(SetFilterOperator)),
            SelectedItem = SetFilterOperator.Any,
            Margin = new Thickness(0, 0, 0, 8)
        };
        _operatorBox.SelectionChanged += (_, _) => ApplyFromUi();
        header.Children.Add(_operatorBox);

        _searchBox = new TextBox
        {
            Watermark = "Search values...",
            Margin = new Thickness(0, 0, 0, 8)
        };
        _searchBox.TextChanged += (_, _) => ApplySearch();
        header.Children.Add(_searchBox);

        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var selectAllButton = new Button
        {
            Content = "Select All"
        };
        selectAllButton.Click += (_, _) =>
        {
            foreach (var cb in _valueCheckBoxes)
            {
                cb.IsChecked = true;
            }
            ApplyFromUi();
        };

        var clearButton = new Button
        {
            Content = "Clear"
        };
        clearButton.Click += (_, _) =>
        {
            foreach (var cb in _valueCheckBoxes)
            {
                cb.IsChecked = false;
            }
            ApplyFromUi();
        };

        var resetButton = new Button
        {
            Content = "Reset"
        };
        resetButton.Click += (_, _) =>
        {
            _filterService.ClearFilter(_grid, _columnId);
            BuildValueList();
        };

        buttonRow.Children.Add(selectAllButton);
        buttonRow.Children.Add(clearButton);
        buttonRow.Children.Add(resetButton);
        header.Children.Add(buttonRow);

        DockPanel.SetDock(header, Dock.Top);
        layout.Children.Add(header);

        var scroll = new ScrollViewer();
        _valuesPanel = new StackPanel
        {
            Orientation = Orientation.Vertical
        };
        scroll.Content = _valuesPanel;
        layout.Children.Add(scroll);

        Content = layout;

        BuildValueList();

        _modelUpdatedHandler = OnModelUpdated;
        _filterChangedHandler = OnFilterChanged;
        _grid.AddEventListener(GridEventTypes.ModelUpdated, _modelUpdatedHandler);
        _grid.AddEventListener(GridEventTypes.FilterChanged, _filterChangedHandler);
        DetachedFromVisualTree += OnDetachedFromVisualTreeInternal;
    }

    private void BuildValueList()
    {
        _valuesPanel.Children.Clear();
        _valueCheckBoxes.Clear();

        var distinct = _filterService.GetDistinctValues(_grid, _columnId);
        var setState = _filterService.GetSetFilterState(_grid, _columnId);

        _operatorBox.SelectedItem = setState.Operator;

        foreach (var value in distinct)
        {
            var check = new CheckBox
            {
                Content = string.IsNullOrEmpty(value.Value) ? "(Blanks)" : value.Value,
                IsThreeState = false,
                Tag = value.Value,
                IsChecked = setState.SelectedValues.Count == 0
                            || (!string.IsNullOrEmpty(value.Value) && setState.SelectedValues.Contains(value.Value, StringComparer.OrdinalIgnoreCase))
                            || (string.IsNullOrEmpty(value.Value) && setState.IncludeBlanks),
                Margin = new Thickness(0, 0, 0, 4)
            };
            check.Checked += (_, _) => OnValueChanged();
            check.Unchecked += (_, _) => OnValueChanged();
            _valuesPanel.Children.Add(check);
            _valueCheckBoxes.Add(check);
        }

        UpdateSummary();
    }

    private void ApplySearch()
    {
        var term = _searchBox.Text?.Trim();
        if (string.IsNullOrEmpty(term))
        {
            foreach (var check in _valueCheckBoxes)
            {
                check.IsVisible = true;
            }
            return;
        }

        foreach (var check in _valueCheckBoxes)
        {
            var text = check.Content?.ToString() ?? string.Empty;
            check.IsVisible = text.Contains(term, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void OnValueChanged()
    {
        ApplyFromUi();
    }

    private void ApplyFromUi()
    {
        var selectedValues = _valueCheckBoxes
            .Where(cb => cb.IsChecked == true)
            .Select(cb => cb.Tag?.ToString() ?? string.Empty)
            .ToList();

        if (selectedValues.Count == _valueCheckBoxes.Count)
        {
            selectedValues.Clear();
        }

        var operatorSelection = _operatorBox.SelectedItem is SetFilterOperator op
            ? op
            : SetFilterOperator.Any;

        var includeBlanks = selectedValues.RemoveAll(string.IsNullOrEmpty) > 0;
        var state = new SetFilterState(selectedValues, operatorSelection, includeBlanks);
        _filterService.ApplySetFilter(_grid, _columnId, state);
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        var selectedCount = _valueCheckBoxes.Count(cb => cb.IsChecked == true);
        var total = _valueCheckBoxes.Count;
        var operatorText = _operatorBox.SelectedItem?.ToString() ?? SetFilterOperator.Any.ToString();
        _summaryText.Text = $"Values: {selectedCount}/{total} ({operatorText})";
    }

    private void OnModelUpdated(ModelUpdatedEventArgs args)
    {
        _filterService.InvalidateDistinctCache(_grid, _columnId);
        Dispatcher.UIThread.Post(BuildValueList);
    }

    private void OnFilterChanged(FilterChangedEventArgs args)
    {
        if (!string.Equals(args.ColumnId, _columnId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Dispatcher.UIThread.Post(BuildValueList);
    }

    private void OnDetachedFromVisualTreeInternal(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _grid.RemoveEventListener(GridEventTypes.ModelUpdated, _modelUpdatedHandler);
        _grid.RemoveEventListener(GridEventTypes.FilterChanged, _filterChangedHandler);
        DetachedFromVisualTree -= OnDetachedFromVisualTreeInternal;
    }
}
