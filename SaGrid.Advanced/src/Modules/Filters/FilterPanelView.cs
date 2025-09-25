using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SaGrid.Advanced.Interfaces;

namespace SaGrid.Advanced.Modules.Filters;

internal sealed class FilterPanelView<TData> : UserControl
{
    private readonly SaGrid<TData> _grid;
    private readonly FilterService _filterService;
    private readonly ListBox _columnList;
    private readonly ContentControl _detailsHost;
    private readonly TextBox _quickFilterBox;

    public FilterPanelView(SaGrid<TData> grid, FilterService filterService)
    {
        _grid = grid;
        _filterService = filterService;

        var root = new DockPanel
        {
            LastChildFill = true,
            Margin = new Thickness(12)
        };

        var header = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(0, 0, 0, 12)
        };

        header.Children.Add(new TextBlock
        {
            Text = "Quick Filter",
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 0, 0, 4)
        });

        _quickFilterBox = new TextBox
        {
            Watermark = "Type to filter all columns...",
            Text = _filterService.GetQuickFilter(_grid),
            Margin = new Thickness(0, 0, 0, 12)
        };
        _quickFilterBox.TextChanged += OnQuickFilterChanged;
        header.Children.Add(_quickFilterBox);

        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var contentGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("180, *"),
            RowDefinitions = new RowDefinitions("*")
        };

        _columnList = new ListBox
        {
            SelectionMode = SelectionMode.Single
        };
        _columnList.SelectionChanged += OnColumnSelectionChanged;

        _detailsHost = new ContentControl
        {
            Margin = new Thickness(12, 0, 0, 0)
        };

        foreach (var column in _grid.AllLeafColumns)
        {
            var headerText = column.ColumnDef.Header?.ToString() ?? column.Id;
            _columnList.Items.Add(new ListBoxItem
            {
                Content = headerText,
                Tag = column.Id
            });
        }

        if (_columnList.Items.Count > 0)
        {
            _columnList.SelectedIndex = 0;
        }
        else
        {
            _detailsHost.Content = new TextBlock
            {
                Text = "No columns available for filtering.",
                FontStyle = FontStyle.Italic
            };
        }

        contentGrid.Children.Add(_columnList);
        Grid.SetColumn(_columnList, 0);
        contentGrid.Children.Add(_detailsHost);
        Grid.SetColumn(_detailsHost, 1);

        root.Children.Add(contentGrid);

        Content = root;
    }

    private void OnQuickFilterChanged(object? sender, TextChangedEventArgs e)
    {
        _filterService.ApplyQuickFilter(_grid, _quickFilterBox.Text);
    }

    private void OnColumnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_columnList.SelectedItem is not ListBoxItem item || item.Tag is not string columnId)
        {
            _detailsHost.Content = new TextBlock
            {
                Text = "Select a column to configure filters.",
                FontStyle = FontStyle.Italic
            };
            return;
        }

        _detailsHost.Content = new SetFilterControl<TData>(_grid, _filterService, columnId);
    }
}
