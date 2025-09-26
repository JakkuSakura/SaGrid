using System.Collections.Generic;

namespace SaGrid.Advanced.Modules.Export;

public enum ExportFormat
{
    Csv,
    Json,
    Excel,
    ClipboardTab,
    ClipboardPlain
}

public record ExportRequest(
    ExportFormat Format,
    IReadOnlyList<string>? ColumnIds = null,
    bool IncludeHeaders = true,
    char CsvDelimiter = ',',
    bool IncludeHiddenColumns = false,
    bool IncludeGroupRows = false
);

public record ExportResult(ExportFormat Format, string? TextPayload, byte[]? BinaryPayload)
{
    public bool IsBinary => BinaryPayload is { Length: > 0 };
    public bool HasText => !string.IsNullOrEmpty(TextPayload);
}

internal sealed record ColumnDescriptor(string Id, string Header, bool IsVisible);
