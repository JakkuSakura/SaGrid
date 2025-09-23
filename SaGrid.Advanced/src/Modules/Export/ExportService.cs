using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using SaGrid.Core;

namespace SaGrid.Advanced.Modules.Export;

/// <summary>
/// Handles export operations for SaGrid, mirroring AG Grid's export services.
/// </summary>
public class ExportService
{
    public async Task<string> ExportToCsvAsync<TData>(SaGrid<TData> grid)
    {
        return await Task.Run(() => BuildCsv(grid));
    }

    public string ExportToCsv<TData>(SaGrid<TData> grid)
    {
        return BuildCsv(grid);
    }

    public async Task<string> ExportToJsonAsync<TData>(SaGrid<TData> grid)
    {
        return await Task.Run(() => BuildJson(grid));
    }

    public string ExportToJson<TData>(SaGrid<TData> grid)
    {
        return BuildJson(grid);
    }

    private static string BuildCsv<TData>(SaGrid<TData> grid)
    {
        var csv = new StringBuilder();

        var headers = grid.VisibleLeafColumns.Select(c => EscapeCsvValue(c.Id)).ToList();
        csv.AppendLine(string.Join(",", headers));

        foreach (var row in grid.RowModel.Rows)
        {
            var values = grid.VisibleLeafColumns.Select(column =>
            {
                var cell = row.GetCell(column.Id);
                var value = cell.Value?.ToString() ?? string.Empty;
                return EscapeCsvValue(value);
            });

            csv.AppendLine(string.Join(",", values));
        }

        return csv.ToString();
    }

    private static string BuildJson<TData>(SaGrid<TData> grid)
    {
        var data = grid.RowModel.Rows.Select(row =>
        {
            var obj = new Dictionary<string, object?>();
            foreach (var column in grid.VisibleLeafColumns)
            {
                var cell = row.GetCell(column.Id);
                obj[column.Id] = cell.Value;
            }
            return obj;
        }).ToList();

        return JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private static string EscapeCsvValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            value = value.Replace("\"", "\"\"");
            return $"\"{value}\"";
        }

        return value;
    }
}
