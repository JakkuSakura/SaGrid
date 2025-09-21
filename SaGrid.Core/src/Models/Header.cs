namespace SaGrid.Core;

public class Header<TData> : IHeader<TData>
{
    private readonly List<Header<TData>> _subHeaders = new();
    
    public string Id { get; }
    public Column<TData> Column { get; }
    public int Index { get; }
    public bool IsPlaceholder { get; }
    public int ColSpan { get; private set; }
    public int RowSpan { get; private set; }
    public IReadOnlyList<IHeader<TData>> SubHeaders => _subHeaders.Cast<IHeader<TData>>().ToList().AsReadOnly();
    public double Size => Column.Size;

    IColumn<TData> IHeader<TData>.Column => Column;

    public Header(Column<TData> column, int index, bool isPlaceholder = false)
    {
        Column = column;
        Index = index;
        IsPlaceholder = isPlaceholder;
        Id = $"{column.Id}_header_{index}";
        
        CalculateSpans();
    }

    private void CalculateSpans()
    {
        if (Column.Columns.Any())
        {
            ColSpan = Column.LeafColumns.Count();
            RowSpan = 1;
            
            // Create sub-headers for child columns
            var index = 0;
            foreach (var childColumn in Column.Columns.Cast<Column<TData>>())
            {
                var subHeader = new Header<TData>(childColumn, index++);
                _subHeaders.Add(subHeader);
            }
        }
        else
        {
            ColSpan = 1;
            // RowSpan will be calculated based on depth in header groups
            RowSpan = 1;
        }
    }

    internal void SetRowSpan(int rowSpan)
    {
        RowSpan = rowSpan;
    }
}

public class HeaderGroup<TData> : IHeaderGroup<TData>
{
    public string Id { get; }
    public int Depth { get; }
    public IReadOnlyList<IHeader<TData>> Headers { get; }

    public HeaderGroup(string id, int depth, IReadOnlyList<Header<TData>> headers)
    {
        Id = id;
        Depth = depth;
        Headers = headers.Cast<IHeader<TData>>().ToList().AsReadOnly();
        
        // Calculate row spans for headers at this depth
        CalculateRowSpans(headers);
    }

    private void CalculateRowSpans(IReadOnlyList<Header<TData>> headers)
    {
        foreach (var header in headers.Cast<Header<TData>>())
        {
            if (!header.Column.Columns.Any())
            {
                // Leaf columns span to the bottom
                var maxDepth = headers.Max(h => h.Column.Depth);
                var rowSpan = maxDepth - header.Column.Depth + 1;
                header.SetRowSpan(rowSpan);
            }
        }
    }
}