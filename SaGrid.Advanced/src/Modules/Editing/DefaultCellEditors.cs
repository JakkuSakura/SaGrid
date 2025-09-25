using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;

namespace SaGrid.Advanced.Modules.Editing;

internal sealed class TextCellEditor<TData> : ICellEditor<TData>
{
    private TextBox? _textBox;

    public Control BuildEditor(CellEditorContext<TData> context)
    {
        _textBox = new TextBox
        {
            Text = context.InitialValue?.ToString() ?? string.Empty,
            HorizontalContentAlignment = HorizontalAlignment.Left
        };

        _textBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                context.Commit();
            }
            else if (e.Key == Key.Escape)
            {
                context.Cancel();
            }
        };

        return _textBox;
    }

    public void SetInitialValue(object? value)
    {
        if (_textBox != null)
        {
            _textBox.Text = value?.ToString() ?? string.Empty;
            _textBox.SelectionStart = _textBox.Text?.Length ?? 0;
        }
    }

    public object? GetValue()
    {
        return _textBox?.Text;
    }

    public bool Validate(out string? validationMessage)
    {
        validationMessage = null;
        return true;
    }
}

internal sealed class NumericCellEditor<TData> : ICellEditor<TData>
{
    private TextBox? _textBox;

    public Control BuildEditor(CellEditorContext<TData> context)
    {
        _textBox = new TextBox
        {
            Text = context.InitialValue?.ToString() ?? string.Empty,
            HorizontalContentAlignment = HorizontalAlignment.Right
        };

        _textBox.AddHandler(TextBox.TextChangedEvent, (_, _) => { /* allow typing */ });
        _textBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                context.Commit();
            }
            else if (e.Key == Key.Escape)
            {
                context.Cancel();
            }
        };

        return _textBox;
    }

    public void SetInitialValue(object? value)
    {
        if (_textBox != null)
        {
            _textBox.Text = value?.ToString() ?? string.Empty;
            _textBox.SelectionStart = _textBox.Text?.Length ?? 0;
        }
    }

    public object? GetValue()
    {
        if (_textBox == null)
        {
            return null;
        }

        if (double.TryParse(_textBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        return _textBox.Text;
    }

    public bool Validate(out string? validationMessage)
    {
        if (_textBox != null && !string.IsNullOrWhiteSpace(_textBox.Text) &&
            !double.TryParse(_textBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
        {
            validationMessage = "Enter a valid number.";
            return false;
        }

        validationMessage = null;
        return true;
    }
}

internal sealed class DateCellEditor<TData> : ICellEditor<TData>
{
    private DatePicker? _datePicker;

    public Control BuildEditor(CellEditorContext<TData> context)
    {
        _datePicker = new DatePicker();
        SetInitialValue(context.InitialValue);

        _datePicker.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                context.Commit();
            }
            else if (e.Key == Key.Escape)
            {
                context.Cancel();
            }
        };

        return _datePicker;
    }

    public void SetInitialValue(object? value)
    {
        if (_datePicker == null)
        {
            return;
        }

        if (value is DateTime dt)
        {
            _datePicker.SelectedDate = dt;
        }
        else if (DateTime.TryParse(value?.ToString(), out var parsed))
        {
            _datePicker.SelectedDate = parsed;
        }
        else
        {
            _datePicker.SelectedDate = null;
        }
    }

    public object? GetValue()
    {
        return _datePicker?.SelectedDate;
    }

    public bool Validate(out string? validationMessage)
    {
        validationMessage = null;
        return true;
    }
}

internal sealed class CheckboxCellEditor<TData> : ICellEditor<TData>
{
    private CheckBox? _checkBox;

    public Control BuildEditor(CellEditorContext<TData> context)
    {
        _checkBox = new CheckBox
        {
            IsChecked = context.InitialValue as bool? ?? bool.TryParse(context.InitialValue?.ToString(), out var flag) && flag,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        _checkBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                context.Commit();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                context.Cancel();
                e.Handled = true;
            }
        };

        return _checkBox;
    }

    public void SetInitialValue(object? value)
    {
        if (_checkBox != null)
        {
            _checkBox.IsChecked = value as bool? ?? bool.TryParse(value?.ToString(), out var flag) && flag;
        }
    }

    public object? GetValue()
    {
        return _checkBox?.IsChecked ?? false;
    }

    public bool Validate(out string? validationMessage)
    {
        validationMessage = null;
        return true;
    }
}

internal sealed class DropdownCellEditor<TData> : ICellEditor<TData>
{
    private ComboBox? _comboBox;

    public Control BuildEditor(CellEditorContext<TData> context)
    {
        var options = ExtractOptions(context.Meta);
        _comboBox = new ComboBox
        {
            ItemsSource = options,
            SelectedItem = context.InitialValue?.ToString()
        };

        SetInitialValue(context.InitialValue);

        _comboBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                context.Commit();
            }
            else if (e.Key == Key.Escape)
            {
                context.Cancel();
            }
        };

        return _comboBox;
    }

    public void SetInitialValue(object? value)
    {
        if (_comboBox != null)
        {
            _comboBox.SelectedItem = value?.ToString();
        }
    }

    public object? GetValue()
    {
        return _comboBox?.SelectedItem;
    }

    public bool Validate(out string? validationMessage)
    {
        validationMessage = null;
        return true;
    }

    private static IEnumerable<string> ExtractOptions(IReadOnlyDictionary<string, object>? meta)
    {
        if (meta != null && meta.TryGetValue("editorOptions", out var optionsObj))
        {
            if (optionsObj is IEnumerable<string> enumerable)
            {
                return enumerable.ToList();
            }

            if (optionsObj is string csv)
            {
                return csv.Split(',').Select(v => v.Trim()).Where(v => v.Length > 0).ToList();
            }
        }

        return Array.Empty<string>();
    }
}

internal sealed class MultiSelectCellEditor<TData> : ICellEditor<TData>
{
    private ListBox? _listBox;

    public Control BuildEditor(CellEditorContext<TData> context)
    {
        var options = ExtractOptions(context.Meta).ToList();
        _listBox = new ListBox
        {
            SelectionMode = SelectionMode.Multiple,
            ItemsSource = options
        };

        SetInitialValue(context.InitialValue);

        foreach (var option in options)
        {
            if (context.InitialValue is IEnumerable<string> enumerable && enumerable.Contains(option, StringComparer.OrdinalIgnoreCase))
            {
                _listBox.SelectedItems?.Add(option);
            }
            else if (context.InitialValue?.ToString()?.Split(',').Select(v => v.Trim()).Contains(option, StringComparer.OrdinalIgnoreCase) == true)
            {
                _listBox.SelectedItems?.Add(option);
            }
        }

        return _listBox;
    }

    public void SetInitialValue(object? value)
    {
        if (_listBox == null)
        {
            return;
        }

        _listBox.SelectedItems?.Clear();
        var values = value switch
        {
            IEnumerable<string> enumerable => enumerable,
            string csv => csv.Split(',').Select(v => v.Trim()),
            _ => Array.Empty<string>()
        };

        foreach (var item in values)
        {
            if (_listBox.Items != null && _listBox.Items.Contains(item))
            {
                _listBox.SelectedItems?.Add(item);
            }
        }
    }

    public object? GetValue()
    {
        if (_listBox?.SelectedItems == null)
        {
            return Array.Empty<string>();
        }

        return _listBox.SelectedItems.Cast<object?>().Select(o => o?.ToString() ?? string.Empty).ToArray();
    }

    public bool Validate(out string? validationMessage)
    {
        validationMessage = null;
        return true;
    }

    private static IEnumerable<string> ExtractOptions(IReadOnlyDictionary<string, object>? meta)
    {
        if (meta != null && meta.TryGetValue("editorOptions", out var optionsObj))
        {
            if (optionsObj is IEnumerable<string> enumerable)
            {
                return enumerable.ToList();
            }

            if (optionsObj is string csv)
            {
                return csv.Split(',').Select(v => v.Trim()).Where(v => v.Length > 0).ToList();
            }
        }

        return Array.Empty<string>();
    }
}
