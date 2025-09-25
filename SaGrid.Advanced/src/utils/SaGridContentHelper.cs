using System.Collections.Generic;
using System.Linq;
using SaGrid.Core;

namespace SaGrid;

internal static class SaGridContentHelper<TData>
{
    public static string GetHeaderContent(IHeader<TData> header)
    {
        return header.Column.Id;
    }

    public static string GetFooterContent(IHeader<TData> header)
    {
        return header.Column.Id;
    }

    public static string GetCellContent(Row<TData> row, Column<TData> column)
    {
        if (row.IsGroupRow && column.Id == row.GroupColumnId)
        {
            var label = row.GroupKey?.ToString() ?? "(blank)";
            var count = row.SubRows.Count;
            return $"{label} ({count})";
        }

        var cell = row.GetCell(column.Id);
        if (cell.Value is IEnumerable<string> stringEnumerable)
        {
            return string.Join(", ", stringEnumerable);
        }

        if (cell.Value is IEnumerable<object?> enumerable && cell.Value is not string)
        {
            return string.Join(", ", enumerable.Select(v => v?.ToString() ?? string.Empty));
        }

        return cell.Value?.ToString() ?? string.Empty;
    }
}
