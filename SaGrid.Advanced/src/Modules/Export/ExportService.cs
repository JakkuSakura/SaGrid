using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SaGrid.Advanced;
using SaGrid.Core;

namespace SaGrid.Advanced.Modules.Export;

public enum ClipboardExportFormat
{
    TabDelimited,
    Plain
}

/// <summary>
/// Handles export operations for SaGrid, mirroring AG Grid's export services.
/// Provides additional option-based exports for advanced scenarios.
/// </summary>
public class ExportService
{
    public async Task<string> ExportToCsvAsync<TData>(SaGrid<TData> grid)
    {
        var request = new ExportRequest(ExportFormat.Csv);
        var result = await Task.Run(() => Export(grid, request));
        return result.TextPayload ?? string.Empty;
    }

    public string ExportToCsv<TData>(SaGrid<TData> grid)
    {
        return Export(grid, new ExportRequest(ExportFormat.Csv)).TextPayload ?? string.Empty;
    }

    public async Task<string> ExportToJsonAsync<TData>(SaGrid<TData> grid)
    {
        var request = new ExportRequest(ExportFormat.Json);
        var result = await Task.Run(() => Export(grid, request));
        return result.TextPayload ?? string.Empty;
    }

    public string ExportToJson<TData>(SaGrid<TData> grid)
    {
        return Export(grid, new ExportRequest(ExportFormat.Json)).TextPayload ?? string.Empty;
    }

    public async Task<byte[]> ExportToExcelAsync<TData>(SaGrid<TData> grid)
    {
        var request = new ExportRequest(ExportFormat.Excel);
        var result = await Task.Run(() => Export(grid, request));
        return result.BinaryPayload ?? Array.Empty<byte>();
    }

    public byte[] ExportToExcel<TData>(SaGrid<TData> grid)
    {
        return Export(grid, new ExportRequest(ExportFormat.Excel)).BinaryPayload ?? Array.Empty<byte>();
    }

    public string BuildClipboardData<TData>(SaGrid<TData> grid, ClipboardExportFormat format = ClipboardExportFormat.TabDelimited, bool includeHeaders = true)
    {
        var request = format == ClipboardExportFormat.TabDelimited
            ? new ExportRequest(ExportFormat.ClipboardTab, IncludeHeaders: includeHeaders)
            : new ExportRequest(ExportFormat.ClipboardPlain, IncludeHeaders: includeHeaders);

        return Export(grid, request).TextPayload ?? string.Empty;
    }

    public ExportResult Export<TData>(SaGrid<TData> grid, ExportRequest request)
    {
        if (grid == null) throw new ArgumentNullException(nameof(grid));
        if (request == null) throw new ArgumentNullException(nameof(request));

        var context = BuildContext(grid, request);

        return request.Format switch
        {
            ExportFormat.Csv => new ExportResult(ExportFormat.Csv, BuildCsvText(context, request.IncludeHeaders, request.CsvDelimiter), null),
            ExportFormat.Json => new ExportResult(ExportFormat.Json, BuildJson(context), null),
            ExportFormat.Excel => new ExportResult(ExportFormat.Excel, null, BuildExcel(context)),
            ExportFormat.ClipboardTab => new ExportResult(ExportFormat.ClipboardTab, BuildPlainDelimited(context, request.IncludeHeaders, '\t'), null),
            ExportFormat.ClipboardPlain => new ExportResult(ExportFormat.ClipboardPlain, BuildPlainDelimited(context, request.IncludeHeaders, ' '), null),
            _ => throw new NotSupportedException($"Export format '{request.Format}' is not supported.")
        };
    }

    private static ExportContext<TData> BuildContext<TData>(SaGrid<TData> grid, ExportRequest request)
    {
        var columns = ResolveColumns(grid, request);
        var rows = ResolveRows(grid, request);
        return new ExportContext<TData>(columns, rows);
    }

    private static IReadOnlyList<Column<TData>> ResolveColumns<TData>(SaGrid<TData> grid, ExportRequest request)
    {
        var allColumns = grid.AllLeafColumns.ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);
        var result = new List<Column<TData>>();

        if (request.ColumnIds is { Count: > 0 })
        {
            foreach (var id in request.ColumnIds)
            {
                if (allColumns.TryGetValue(id, out var column))
                {
                    if (request.IncludeHiddenColumns || grid.VisibleLeafColumns.Contains(column))
                    {
                        result.Add(column);
                    }
                }
            }

            if (result.Count > 0)
            {
                return result;
            }
        }

        var source = request.IncludeHiddenColumns ? grid.AllLeafColumns : grid.VisibleLeafColumns;
        return source.ToList();
    }

    private static IReadOnlyList<Row<TData>> ResolveRows<TData>(SaGrid<TData> grid, ExportRequest request)
    {
        var rows = grid.RowModel.FlatRows.OfType<Row<TData>>();
        if (!request.IncludeGroupRows)
        {
            rows = rows.Where(row => !row.IsGroupRow);
        }

        return rows.ToList();
    }

    private static string BuildCsvText<TData>(ExportContext<TData> context, bool includeHeaders, char delimiter)
    {
        var builder = new StringBuilder();

        if (includeHeaders)
        {
            var headers = context.Columns.Select(c => EscapeCsvValue(c.ColumnDef.Header?.ToString() ?? c.Id, delimiter));
            builder.AppendLine(string.Join(delimiter, headers));
        }

        foreach (var row in context.Rows)
        {
            var values = context.Columns.Select(column =>
            {
                var value = ExtractCellValue(row, column.Id);
                return EscapeCsvValue(value?.ToString() ?? string.Empty, delimiter);
            });

            builder.AppendLine(string.Join(delimiter, values));
        }

        return builder.ToString().TrimEnd('\r', '\n');
    }

    private static string BuildPlainDelimited<TData>(ExportContext<TData> context, bool includeHeaders, char delimiter)
    {
        var builder = new StringBuilder();
        var separator = delimiter.ToString();

        if (includeHeaders)
        {
            var headers = context.Columns.Select(c => EscapeClipboardValue(c.ColumnDef.Header?.ToString() ?? c.Id));
            builder.AppendLine(string.Join(separator, headers));
        }

        foreach (var row in context.Rows)
        {
            var values = context.Columns.Select(column =>
            {
                var value = ExtractCellValue(row, column.Id);
                return EscapeClipboardValue(value?.ToString() ?? string.Empty);
            });

            builder.AppendLine(string.Join(separator, values));
        }

        return builder.ToString().TrimEnd('\r', '\n');
    }

    private static string BuildJson<TData>(ExportContext<TData> context)
    {
        var payload = context.Rows.Select(row =>
        {
            var obj = new Dictionary<string, object?>();
            foreach (var column in context.Columns)
            {
                obj[column.Id] = ExtractCellValue(row, column.Id);
            }

            return obj;
        }).ToList();

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private static byte[] BuildExcel<TData>(ExportContext<TData> context)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            WriteEntry(archive, "[Content_Types].xml", BuildContentTypesXml());
            WriteEntry(archive, "_rels/.rels", BuildPackageRelsXml());
            WriteEntry(archive, "xl/workbook.xml", BuildWorkbookXml());
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelsXml());
            WriteEntry(archive, "xl/styles.xml", BuildStylesXml());
            WriteEntry(archive, "xl/worksheets/sheet1.xml", BuildWorksheetXml(context));
        }

        return stream.ToArray();
    }

    private static string BuildWorksheetXml<TData>(ExportContext<TData> context)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");

        AppendRow(sb, context.Columns.Select(c => c.ColumnDef.Header?.ToString() ?? c.Id), 1);

        var excelRowIndex = 2;
        foreach (var row in context.Rows)
        {
            var values = context.Columns.Select(column => ExtractCellValue(row, column.Id)?.ToString() ?? string.Empty).ToList();
            AppendRow(sb, values, excelRowIndex++);
        }

        sb.Append("</sheetData></worksheet>");
        return sb.ToString();
    }

    private static object? ExtractCellValue<TData>(Row<TData> row, string columnId)
    {
        if (row.TryGetAggregatedValue(columnId, out var aggregated))
        {
            return aggregated;
        }

        return row.GetCell(columnId).Value;
    }

    private static void AppendRow(StringBuilder sb, IEnumerable<string> values, int rowIndex)
    {
        sb.Append("<row r=\"").Append(rowIndex).Append("\">");
        var columnIndex = 0;
        foreach (var value in values)
        {
            var cellReference = GetCellReference(columnIndex++, rowIndex);
            sb.Append("<c r=\"").Append(cellReference).Append("\" t=\"inlineStr\"><is><t xml:space=\"preserve\">")
              .Append(EscapeXml(value))
              .Append("</t></is></c>");
        }

        sb.Append("</row>");
    }

    private static string BuildContentTypesXml()
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/></Types>";
    }

    private static string BuildPackageRelsXml()
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>";
    }

    private static string BuildWorkbookXml()
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"SaGrid Export\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>";
    }

    private static string BuildWorkbookRelsXml()
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>";
    }

    private static string BuildStylesXml()
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><fonts count=\"1\"><font><sz val=\"11\"/><color theme=\"1\"/><name val=\"Calibri\"/><family val=\"2\"/></font></fonts><fills count=\"1\"><fill><patternFill patternType=\"none\"/></fill></fills><borders count=\"1\"><border/></borders><cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs><cellXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/></cellXfs><cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles></styleSheet>";
    }

    private static void WriteEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string GetCellReference(int columnIndex, int rowIndex)
    {
        var columnName = BuildColumnName(columnIndex);
        return columnName + rowIndex;
    }

    private static string BuildColumnName(int columnIndex)
    {
        var index = columnIndex;
        var chars = new Stack<char>();
        do
        {
            var remainder = index % 26;
            chars.Push((char)('A' + remainder));
            index = (index / 26) - 1;
        }
        while (index >= 0);

        return new string(chars.ToArray());
    }

    private static string EscapeXml(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '<':
                    sb.Append("&lt;");
                    break;
                case '>':
                    sb.Append("&gt;");
                    break;
                case '"':
                    sb.Append("&quot;");
                    break;
                case '&':
                    sb.Append("&amp;");
                    break;
                case '\'':
                    sb.Append("&apos;");
                    break;
                case '\n':
                    sb.Append("&#10;");
                    break;
                case '\r':
                    sb.Append("&#13;");
                    break;
                case '\t':
                    sb.Append("&#9;");
                    break;
                default:
                    sb.Append(ch);
                    break;
            }
        }

        return sb.ToString();
    }

    private static string EscapeClipboardValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Replace("\r", " ").Replace("\n", " ");
    }

    private static string EscapeCsvValue(string value, char delimiter)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var requiresQuotes = value.IndexOfAny(new[] { '\"', '\n', '\r' }) >= 0 || value.Contains(delimiter);
        if (!requiresQuotes)
        {
            return value;
        }

        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    private sealed record ExportContext<TData>(IReadOnlyList<Column<TData>> Columns, IReadOnlyList<Row<TData>> Rows);
}
