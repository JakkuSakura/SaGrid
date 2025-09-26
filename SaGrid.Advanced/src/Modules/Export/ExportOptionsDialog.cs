using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace SaGrid.Advanced.Modules.Export;

internal sealed class ExportOptionsDialog : Window
{
    private readonly List<ColumnDescriptor> _columns;
    private readonly Dictionary<string, CheckBox> _columnCheckBoxes = new(StringComparer.OrdinalIgnoreCase);

    private ComboBox? _formatComboBox;
    private ComboBox? _delimiterComboBox;
    private CheckBox? _includeHeadersCheckBox;
    private CheckBox? _includeGroupRowsCheckBox;
    private TextBlock? _statusText;

    public ExportOptionsDialog(IEnumerable<ColumnDescriptor> columns)
    {
        if (columns == null) throw new ArgumentNullException(nameof(columns));

        _columns = columns.ToList();

        Title = "Export Options";
        Width = 460;
        Height = 540;
        MinWidth = 380;
        MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Content = BuildContent();
    }

    private Control BuildContent()
    {
        var root = new DockPanel
        {
            Margin = new Thickness(16)
        };

        root.Children.Add(BuildFooter());
        DockPanel.SetDock(root.Children[^1], Dock.Bottom);

        var stack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 12
        };

        stack.Children.Add(BuildFormatSection());
        stack.Children.Add(BuildColumnsSection());
        stack.Children.Add(BuildOptionsSection());

        root.Children.Add(new ScrollViewer
        {
            Content = stack,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        });

        return root;
    }

    private Control BuildFormatSection()
    {
        var container = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 6
        };

        container.Children.Add(new TextBlock
        {
            Text = "Format",
            FontWeight = FontWeight.Bold
        });

        _formatComboBox = new ComboBox
        {
            ItemsSource = Enum.GetValues(typeof(ExportFormat)).Cast<ExportFormat>().Select(f => new ComboBoxItem
            {
                Content = GetFormatLabel(f),
                Tag = f
            }).ToList(),
            SelectedIndex = 0
        };

        _formatComboBox.SelectionChanged += (_, _) => UpdateDelimiterAvailability();

        container.Children.Add(_formatComboBox);

        var delimiterPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 4
        };

        delimiterPanel.Children.Add(new TextBlock
        {
            Text = "CSV delimiter",
            FontSize = 12,
            Foreground = Brushes.Gray
        });

        _delimiterComboBox = new ComboBox
        {
            ItemsSource = new List<ComboBoxItem>
            {
                CreateDelimiterItem("Comma (,)", ','),
                CreateDelimiterItem("Semicolon (;)", ';'),
                CreateDelimiterItem("Tab", '\t')
            },
            SelectedIndex = 0
        };

        delimiterPanel.Children.Add(_delimiterComboBox);
        container.Children.Add(delimiterPanel);

        return container;
    }

    private Control BuildColumnsSection()
    {
        var container = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 6
        };

        container.Children.Add(new TextBlock
        {
            Text = "Columns",
            FontWeight = FontWeight.Bold
        });

        container.Children.Add(new TextBlock
        {
            Text = "Select the columns to include in the export.",
            FontSize = 12,
            Foreground = Brushes.Gray
        });

        var selectButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        selectButtons.Children.Add(CreateActionButton("Select All", () => SetAllColumnsSelected(true)));
        selectButtons.Children.Add(CreateActionButton("Clear", () => SetAllColumnsSelected(false)));

        container.Children.Add(selectButtons);

        var listPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 4
        };

        foreach (var column in _columns)
        {
            var checkBox = new CheckBox
            {
                Content = column.IsVisible ? column.Header : $"{column.Header} (hidden)",
                IsChecked = column.IsVisible,
                Tag = column.Id
            };

            _columnCheckBoxes[column.Id] = checkBox;
            listPanel.Children.Add(checkBox);
        }

        container.Children.Add(new ScrollViewer
        {
            Content = listPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Height = 220
        });

        return container;
    }

    private Control BuildOptionsSection()
    {
        var container = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 4
        };

        container.Children.Add(new TextBlock
        {
            Text = "Formatting",
            FontWeight = FontWeight.Bold
        });

        _includeHeadersCheckBox = new CheckBox
        {
            Content = "Include headers",
            IsChecked = true
        };

        _includeGroupRowsCheckBox = new CheckBox
        {
            Content = "Include group summary rows"
        };

        container.Children.Add(_includeHeadersCheckBox);
        container.Children.Add(_includeGroupRowsCheckBox);

        _statusText = new TextBlock
        {
            Text = string.Empty,
            Foreground = Brushes.Red,
            FontSize = 12,
            IsVisible = false
        };

        container.Children.Add(_statusText);

        return container;
    }

    private Control BuildFooter()
    {
        var border = new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            BorderBrush = Brushes.LightGray,
            Padding = new Thickness(0, 12, 0, 0)
        };

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };

        var cancelButton = new Button
        {
            Content = "Cancel"
        };

        cancelButton.Click += (_, _) => Close(null);

        var exportButton = new Button
        {
            Content = "Export",
            Classes = { "accent" }
        };

        exportButton.Click += (_, _) => Submit();

        panel.Children.Add(cancelButton);
        panel.Children.Add(exportButton);

        border.Child = panel;
        return border;
    }

    private void Submit()
    {
        var selectedColumns = _columnCheckBoxes
            .Where(kvp => kvp.Value.IsChecked == true)
            .Select(kvp => kvp.Key)
            .ToList();

        if (selectedColumns.Count == 0)
        {
            if (_statusText != null)
            {
                _statusText.Text = "Select at least one column.";
                _statusText.IsVisible = true;
            }
            return;
        }

        var selectedFormat = GetSelectedFormat();
        var delimiter = GetSelectedDelimiter();
        var includeHeaders = _includeHeadersCheckBox?.IsChecked != false;
        var includeGroupRows = _includeGroupRowsCheckBox?.IsChecked == true;

        var includesHidden = selectedColumns.Any(id => _columns.FirstOrDefault(c => c.Id == id)?.IsVisible == false);

        var request = new ExportRequest(
            selectedFormat,
            selectedColumns,
            IncludeHeaders: includeHeaders,
            CsvDelimiter: delimiter,
            IncludeHiddenColumns: includesHidden,
            IncludeGroupRows: includeGroupRows);

        Close(request);
    }

    private void SetAllColumnsSelected(bool selected)
    {
        foreach (var checkBox in _columnCheckBoxes.Values)
        {
            checkBox.IsChecked = selected;
        }

        if (_statusText != null)
        {
            _statusText.IsVisible = false;
        }
    }

    private ExportFormat GetSelectedFormat()
    {
        if (_formatComboBox?.SelectedItem is ComboBoxItem item && item.Tag is ExportFormat format)
        {
            return format;
        }

        return ExportFormat.Csv;
    }

    private char GetSelectedDelimiter()
    {
        if (_delimiterComboBox?.SelectedItem is ComboBoxItem item && item.Tag is char delimiter)
        {
            return delimiter;
        }

        return ',';
    }

    private void UpdateDelimiterAvailability()
    {
        if (_delimiterComboBox == null)
        {
            return;
        }

        var format = GetSelectedFormat();
        _delimiterComboBox.IsEnabled = format == ExportFormat.Csv;
    }

    private static string GetFormatLabel(ExportFormat format)
    {
        return format switch
        {
            ExportFormat.Csv => "CSV",
            ExportFormat.Json => "JSON",
            ExportFormat.Excel => "Excel (XLSX)",
            ExportFormat.ClipboardTab => "Clipboard (Tab)",
            ExportFormat.ClipboardPlain => "Clipboard (Plain)",
            _ => format.ToString()
        };
    }

    private static ComboBoxItem CreateDelimiterItem(string label, char delimiter)
    {
        return new ComboBoxItem
        {
            Content = label,
            Tag = delimiter
        };
    }

    private static Button CreateActionButton(string label, Action action)
    {
        var button = new Button
        {
            Content = label,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        button.Click += (_, _) => action();
        return button;
    }
}
