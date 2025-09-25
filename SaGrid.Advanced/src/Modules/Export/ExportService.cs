using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using SaGrid.Core;

namespace SaGrid.Advanced.Modules.Export;

public enum ClipboardExportFormat
{
    TabDelimited,
    Plain
}

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

    public async Task<byte[]> ExportToExcelAsync<TData>(SaGrid<TData> grid)
    {
        return await Task.Run(() => BuildExcel(grid));
    }

    public byte[] ExportToExcel<TData>(SaGrid<TData> grid)
    {
        return BuildExcel(grid);
    }

    public string BuildClipboardData<TData>(SaGrid<TData> grid, ClipboardExportFormat format = ClipboardExportFormat.TabDelimited, bool includeHeaders = true)
    {
        return BuildClipboardText(grid, format, includeHeaders);
    }

    private static string BuildCsv<TData>(SaGrid<TData> grid)
    {
        var csv = new StringBuilder();

        var headers = grid.VisibleLeafColumns
            .Select(c => EscapeCsvValue(c.ColumnDef.Header?.ToString() ?? c.Id))
            .ToList();
        csv.AppendLine(string.Join(",", headers));

        foreach (var row in grid.RowModel.FlatRows)
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
        var data = grid.RowModel.FlatRows.Select(row =>
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

    private static byte[] BuildExcel<TData>(SaGrid<TData> grid)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            WriteEntry(archive, "[Content_Types].xml", BuildContentTypesXml());
            WriteEntry(archive, "_rels/.rels", BuildPackageRelsXml());
            WriteEntry(archive, "xl/workbook.xml", BuildWorkbookXml());
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelsXml());
            WriteEntry(archive, "xl/styles.xml", BuildStylesXml());
            WriteEntry(archive, "xl/worksheets/sheet1.xml", BuildWorksheetXml(grid));
        }

        return stream.ToArray();
    }

    private static string BuildClipboardText<TData>(SaGrid<TData> grid, ClipboardExportFormat format, bool includeHeaders)
    {
        var delimiter = format == ClipboardExportFormat.TabDelimited ? "\t" : " ";
        var builder = new StringBuilder();

        if (includeHeaders)
        {
            var headerLine = string.Join(delimiter, grid.VisibleLeafColumns.Select(c => EscapeClipboardValue(c.ColumnDef.Header?.ToString() ?? c.Id)));
            builder.AppendLine(headerLine);
        }

        foreach (var row in grid.RowModel.FlatRows)
        {
            var values = grid.VisibleLeafColumns.Select(column =>
            {
                var cell = row.GetCell(column.Id);
                return EscapeClipboardValue(cell.Value?.ToString() ?? string.Empty);
            });

            builder.AppendLine(string.Join(delimiter, values));
        }

        return builder.ToString().TrimEnd('\r', '\n');
    }

    private static string BuildWorksheetXml<TData>(SaGrid<TData> grid)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");

        var visibleColumns = grid.VisibleLeafColumns.ToList();
        var rows = grid.RowModel.FlatRows;

        AppendRow(sb, visibleColumns.Select(c => c.ColumnDef.Header?.ToString() ?? c.Id), 1);

        var excelRowIndex = 2;
        foreach (var row in rows)
        {
            var values = visibleColumns.Select(column => row.GetCell(column.Id).Value?.ToString() ?? string.Empty).ToList();
            AppendRow(sb, values, excelRowIndex++);
        }

        sb.Append("</sheetData></worksheet>");
        return sb.ToString();
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

    private static string EscapeCsvValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
        {
            var escaped = value.Replace("\"", "\"\"");
            return $"\"{escaped}\"";
        }

        return value;
    }
}
