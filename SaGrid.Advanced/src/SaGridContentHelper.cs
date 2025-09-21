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
        var cell = row.GetCell(column.Id);
        return cell.Value?.ToString() ?? "";
    }
}