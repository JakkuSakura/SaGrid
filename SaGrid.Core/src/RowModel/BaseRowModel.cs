using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using SaGrid.Core.Models;
using SaGrid.Core;

namespace SaGrid.Core.Models;

// Shared filter/sort pipeline for row models
public class BaseRowModel<TData>
{
    internal static RowModel<TData> ApplyFilter(Table<TData> table, IReadOnlyList<Row<TData>> sourceRows)
    {
        var globalFilter = table.State.GlobalFilter;
        var columnFilters = table.State.ColumnFilters;
        var debug = IsDebugFilteringEnabled(table);

        if (globalFilter == null && (columnFilters == null || columnFilters.Filters.Count == 0))
        {
            return new RowModel<TData>
            {
                Rows = sourceRows.ToList().AsReadOnly(),
                FlatRows = sourceRows.ToList().AsReadOnly(),
                RowsById = sourceRows.ToDictionary(r => r.Id, r => r).AsReadOnly()
            };
        }

        var filteredRows = new List<Row<TData>>();
        foreach (var row in sourceRows)
        {
            var rowOk = true;
            if (debug) Console.WriteLine($"[Filter] Row {row.Index} (Id={row.Id})");

            if (globalFilter != null)
            {
                var ok = PassesGlobalFilter(table, row, globalFilter.Value);
                if (debug) Console.WriteLine($"[Filter]  -> Global {(ok ? "PASS" : "FAIL")}");
                if (!ok)
                {
                    if (debug) Console.WriteLine("[Filter]  => RESULT FAIL");
                    rowOk = false;
                }
            }

            if (rowOk && columnFilters != null)
            {
                foreach (var filter in columnFilters.Filters)
                {
                    var ok = PassesColumnFilter(table, row, filter);
                    if (!ok)
                    {
                        rowOk = false;
                        break;
                    }
                }
                if (debug) Console.WriteLine($"[Filter]  => RESULT {(rowOk ? "PASS" : "FAIL")}");
            }

            if (rowOk)
            {
                filteredRows.Add(row);
            }
        }

        return new RowModel<TData>
        {
            Rows = filteredRows.AsReadOnly(),
            FlatRows = sourceRows.ToList().AsReadOnly(),
            RowsById = sourceRows.ToDictionary(r => r.Id, r => r).AsReadOnly()
        };
    }

    internal static RowModel<TData> ApplySort(Table<TData> table, IReadOnlyList<Row<TData>> rows)
    {
        var sorting = table.State.Sorting;
        if (sorting == null || sorting.Columns.Count == 0)
        {
            return new RowModel<TData>
            {
                Rows = rows.ToList().AsReadOnly(),
                FlatRows = rows.ToList().AsReadOnly(),
                RowsById = rows.ToDictionary(r => r.Id, r => r).AsReadOnly()
            };
        }

        var sorted = rows.ToList();
        sorted.Sort((row1, row2) =>
        {
            foreach (var sortColumn in sorting.Columns)
            {
                var cell1 = row1.GetCell(sortColumn.Id);
                var cell2 = row2.GetCell(sortColumn.Id);

                var value1 = cell1.Value;
                var value2 = cell2.Value;

                int comparison;
                // Match Table's behavior: nulls last
                if (value1 == null && value2 == null) comparison = 0;
                else if (value1 == null) comparison = 1;
                else if (value2 == null) comparison = -1;
                else if (value1 is IComparable comparable1 && value2 is IComparable)
                {
                    comparison = comparable1.CompareTo(value2);
                }
                else
                {
                    comparison = string.Compare(value1.ToString(), value2.ToString(), StringComparison.Ordinal);
                }

                if (comparison != 0)
                {
                    if (sortColumn.Direction == SortDirection.Descending)
                    {
                        comparison = -comparison;
                    }
                    return comparison;
                }
            }
            return 0;
        });

        return new RowModel<TData>
        {
            Rows = sorted.AsReadOnly(),
            FlatRows = sorted.AsReadOnly(),
            RowsById = sorted.ToDictionary(r => r.Id, r => r).AsReadOnly()
        };
    }

    internal static RowModel<TData> ApplyFilterAndSort(Table<TData> table, IReadOnlyList<Row<TData>> sourceRows)
    {
        var filtered = ApplyFilter(table, sourceRows);
        var sorted = ApplySort(table, filtered.Rows);
        return sorted;
    }

    internal static bool PassesGlobalFilter(Table<TData> table, Row<TData> row, object filterValue)
    {
        var filterText = filterValue?.ToString()?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(filterText)) return true;

        foreach (var column in table.VisibleLeafColumns)
        {
            var cell = row.GetCell(column.Id);
            var cellValue = cell.Value?.ToString()?.ToLowerInvariant();
            if (!string.IsNullOrEmpty(cellValue) && cellValue.Contains(filterText))
            {
                return true;
            }
        }
        return false;
    }

    internal static bool PassesColumnFilter(Table<TData> table, Row<TData> row, ColumnFilter filter)
    {
        var column = table.GetColumn(filter.Id);
        if (column == null) return true;

        var cell = row.GetCell(filter.Id);
        var cellValue = cell.Value;
        var filterValue = filter.Value;
        var debug = IsDebugFilteringEnabled(table);

        if (filterValue == null) return true;
        if (cellValue == null) return false;

        if (filterValue is SetFilterState setState)
        {
            var cellString = cellValue?.ToString() ?? string.Empty;
            var ok = EvaluateSetFilter(cellString, setState);
            if (debug)
            {
                Console.WriteLine($"[Filter]  -> Column '{filter.Id}' (SetFilter) cell='{cellString}' selected=[{string.Join(",", setState.SelectedValues)}] op={setState.Operator} blanks={setState.IncludeBlanks} => {(ok ? "PASS" : "FAIL")}");
            }
            return ok;
        }

        // Advanced text filter: supports mode and case sensitivity
        if (filterValue is TextFilterState textState)
        {
            var text = textState.Query?.Trim() ?? string.Empty;
            if (text.Length == 0) return true;

            // Numeric comparator detection: <, >, =, <=, >=, !=
            if (TryParseNumericExpression(text, out var op, out var rhs))
            {
                if (TryConvertToDouble(cellValue, out var cellNumber))
                {
                    var cmpOk = op switch
                    {
                        NumericOp.Lt => cellNumber < rhs,
                        NumericOp.Lte => cellNumber <= rhs,
                        NumericOp.Gt => cellNumber > rhs,
                        NumericOp.Gte => cellNumber >= rhs,
                        NumericOp.Eq => Math.Abs(cellNumber - rhs) < 0.000_000_1,
                        NumericOp.Ne => Math.Abs(cellNumber - rhs) >= 0.000_000_1,
                        _ => false
                    };
                    if (debug) Console.WriteLine($"[Filter]     (Numeric {op}) cell={cellNumber} rhs={rhs} => {cmpOk}");
                    if (debug) Console.WriteLine($"[Filter]  -> Column '{filter.Id}' {(cmpOk ? "PASS" : "FAIL")} (NumericExpr)");
                    return cmpOk;
                }
                // If cell is not numeric, fall back to text compare below
            }

            var cellString = cellValue.ToString() ?? string.Empty;
            var comparison = textState.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            bool ok = textState.Mode switch
            {
                TextFilterMode.StartsWith => cellString.StartsWith(text, comparison),
                TextFilterMode.EndsWith => cellString.EndsWith(text, comparison),
                _ => cellString.IndexOf(text, comparison) >= 0
            };

            if (debug) Console.WriteLine($"[Filter]     (Text {textState.Mode}, Case={(textState.CaseSensitive ? "Sensitive" : "Insensitive")}) cell='{cellString}' filter='{text}' => {ok}");
            if (debug) Console.WriteLine($"[Filter]  -> Column '{filter.Id}' {(ok ? "PASS" : "FAIL")} (TextFilterState)");
            return ok;
        }

        if (filterValue is string stringFilter)
        {
            var text = stringFilter?.Trim() ?? string.Empty;
            if (text.Length == 0) return true;

            if (cellValue is int cellInt && int.TryParse(text, out var parsedInt))
            {
                var ok = cellInt == parsedInt;
                if (debug) Console.WriteLine($"[Filter]     (String->Int) cell={cellInt} filter={parsedInt} => {ok}");
                if (ok) { if (debug) Console.WriteLine($"[Filter]  -> Column '{filter.Id}' PASS (cell='{cellInt}', filter='{text}')"); return true; }
            }
            else if (cellValue is double cellDouble && double.TryParse(text, out var parsedDouble))
            {
                var ok = Math.Abs(cellDouble - parsedDouble) < 0.000_001;
                if (debug) Console.WriteLine($"[Filter]     (String->Double) cell={cellDouble} filter={parsedDouble} => {ok}");
                if (ok) { if (debug) Console.WriteLine($"[Filter]  -> Column '{filter.Id}' PASS (cell='{cellDouble}', filter='{text}')"); return true; }
            }
            else if (cellValue is float cellFloat && float.TryParse(text, out var parsedFloat))
            {
                var ok = Math.Abs(cellFloat - parsedFloat) < 0.000_001f;
                if (debug) Console.WriteLine($"[Filter]     (String->Float) cell={cellFloat} filter={parsedFloat} => {ok}");
                if (ok) { if (debug) Console.WriteLine($"[Filter]  -> Column '{filter.Id}' PASS (cell='{cellFloat}', filter='{text}')"); return true; }
            }
            else if (cellValue is decimal cellDecimal && decimal.TryParse(text, out var parsedDecimal))
            {
                var ok = cellDecimal == parsedDecimal;
                if (debug) Console.WriteLine($"[Filter]     (String->Decimal) cell={cellDecimal} filter={parsedDecimal} => {ok}");
                if (ok) { if (debug) Console.WriteLine($"[Filter]  -> Column '{filter.Id}' PASS (cell='{cellDecimal}', filter='{text}')"); return true; }
            }
            else if (cellValue is bool cellBool && bool.TryParse(text, out var parsedBool))
            {
                var ok = cellBool == parsedBool;
                if (debug) Console.WriteLine($"[Filter]     (String->Bool) cell={cellBool} filter={parsedBool} => {ok}");
                if (ok) { if (debug) Console.WriteLine($"[Filter]  -> Column '{filter.Id}' PASS (cell='{cellBool}', filter='{text}')"); return true; }
            }

            var cellString = cellValue.ToString() ?? string.Empty;
            var contains = cellString.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
            if (debug) Console.WriteLine($"[Filter]     (String Contains) cell='{cellString}' filter='{text}' => {contains}");
            if (debug) Console.WriteLine($"[Filter]  -> Column '{filter.Id}' {(contains ? "PASS" : "FAIL")} (cell='{cellString}', filter='{text}')");
            return contains;
        }

        if (filterValue is bool boolFilter)
        {
            var ok = cellValue is bool cellBool && cellBool == boolFilter;
            if (debug) Console.WriteLine($"[Filter]  -> Column '{filter.Id}' {(ok ? "PASS" : "FAIL")} (Bool) cell={cellValue} filter={boolFilter}");
            return ok;
        }

        if (filterValue is int intFilter)
        {
            var ok = cellValue is int cellInt && cellInt == intFilter;
            if (debug) Console.WriteLine($"[Filter]  -> Column '{filter.Id}' {(ok ? "PASS" : "FAIL")} (Int) cell={cellValue} filter={intFilter}");
            return ok;
        }

        if (filterValue is double doubleFilter)
        {
            var ok = cellValue is double cellDouble && Math.Abs(cellDouble - doubleFilter) < 0.001;
            if (debug) Console.WriteLine($"[Filter]  -> Column '{filter.Id}' {(ok ? "PASS" : "FAIL")} (Double) cell={cellValue} filter={doubleFilter}");
            return ok;
        }

        // Numeric range object detection (anonymous objects with min/max)
        var fType = filterValue.GetType();
        if (!(filterValue is string) && !(filterValue is bool))
        {
            var minProp = fType.GetProperty("min") ?? fType.GetProperty("Min");
            var maxProp = fType.GetProperty("max") ?? fType.GetProperty("Max");
            if (minProp != null || maxProp != null)
            {
                double? min = null, max = null;
                try
                {
                    if (minProp != null)
                    {
                        var mv = minProp.GetValue(filterValue);
                        if (mv != null && double.TryParse(mv.ToString(), out var d)) min = d;
                    }
                    if (maxProp != null)
                    {
                        var xv = maxProp.GetValue(filterValue);
                        if (xv != null && double.TryParse(xv.ToString(), out var d)) max = d;
                    }
                }
                catch { }

                if (min.HasValue || max.HasValue)
                {
                    var ok = double.TryParse(cellValue.ToString(), out var v) &&
                             (!min.HasValue || v >= min.Value) &&
                             (!max.HasValue || v <= max.Value);
                    if (debug) Console.WriteLine($"[Filter]  -> Column '{filter.Id}' {(ok ? "PASS" : "FAIL")} (Range) cell={cellValue} min={min?.ToString() ?? "-"} max={max?.ToString() ?? "-"}");
                    return ok;
                }
            }
        }

        var eq = cellValue.ToString()?.Equals(filterValue.ToString(), StringComparison.OrdinalIgnoreCase) == true;
        if (debug) Console.WriteLine($"[Filter]  -> Column '{filter.Id}' {(eq ? "PASS" : "FAIL")} (Default Equals) cell='{cellValue}' filter='{filterValue}'");
        return eq;
    }

    internal static bool EvaluateSetFilter(string cellString, SetFilterState state)
    {
        var isBlank = string.IsNullOrEmpty(cellString);
        if (isBlank)
        {
            return state.IncludeBlanks;
        }

        var tokens = cellString
            .Split(new[] { ',' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        var selections = state.SelectedValues;
        if (selections.Count == 0)
        {
            return true;
        }

        if (state.Operator == SetFilterOperator.All)
        {
            return selections.All(selection => tokens.Contains(selection, StringComparer.OrdinalIgnoreCase));
        }

        return tokens.Any(token => selections.Contains(token, StringComparer.OrdinalIgnoreCase));
    }

    private static bool IsDebugFilteringEnabled(Table<TData> table)
    {
        if (table.Options.Meta != null && table.Options.Meta.TryGetValue("debugFiltering", out var v))
        {
            return v switch
            {
                bool b => b,
                string s => s.Equals("true", StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }
        return false;
    }

    private enum NumericOp { Lt, Lte, Gt, Gte, Eq, Ne }

    private static bool TryParseNumericExpression(string input, out NumericOp op, out double value)
    {
        op = default;
        value = default;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var s = input.Trim();
        ReadOnlySpan<char> span = s.AsSpan();
        NumericOp? parsedOp = null;
        int pos = 0;

        if (span.StartsWith("<=")) { parsedOp = NumericOp.Lte; pos = 2; }
        else if (span.StartsWith(">=")) { parsedOp = NumericOp.Gte; pos = 2; }
        else if (span.StartsWith("!=")) { parsedOp = NumericOp.Ne; pos = 2; }
        else if (span.StartsWith("==")) { parsedOp = NumericOp.Eq; pos = 2; }
        else if (span.StartsWith("<")) { parsedOp = NumericOp.Lt; pos = 1; }
        else if (span.StartsWith(">")) { parsedOp = NumericOp.Gt; pos = 1; }
        else if (span.StartsWith("=")) { parsedOp = NumericOp.Eq; pos = 1; }

        if (parsedOp == null) return false;

        var rest = span[pos..].TrimStart().ToString();
        if (double.TryParse(rest, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var rhs))
        {
            op = parsedOp.Value;
            value = rhs;
            return true;
        }

        return false;
    }

    private static bool TryConvertToDouble(object? value, out double number)
    {
        number = default;
        if (value == null) return false;
        try
        {
            switch (value)
            {
                case double d: number = d; return true;
                case float f: number = f; return true;
                case decimal m: number = (double)m; return true;
                case long l: number = l; return true;
                case int i: number = i; return true;
                case short s: number = s; return true;
                case byte b: number = b; return true;
                case ulong ul: number = ul; return true;
                case uint ui: number = ui; return true;
                case ushort usv: number = usv; return true;
                case sbyte sb: number = sb; return true;
            }

            if (value is IConvertible)
            {
                number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                return true;
            }
        }
        catch
        {
            // ignore conversion errors
        }

        return double.TryParse(value.ToString(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out number);
    }
}
