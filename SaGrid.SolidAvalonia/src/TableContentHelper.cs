using SaGrid.Core;

namespace SaGrid.SolidAvalonia;

internal static class TableContentHelper<TData>
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