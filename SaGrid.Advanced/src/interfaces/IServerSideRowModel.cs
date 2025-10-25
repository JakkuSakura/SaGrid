using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SaGrid.Core;
using SaGrid.Core.Models;

namespace SaGrid.Advanced.Interfaces;

public interface IServerSideDataSource<TData>
{
    Task<ServerSideRowsResult<TData>> GetRowsAsync(ServerSideRowsRequest request, CancellationToken cancellationToken = default);
}

public sealed record ServerSideRowsRequest(
    int StartRow,
    int EndRow,
    IReadOnlyList<ColumnSort> SortModel,
    IReadOnlyDictionary<string, object?> FilterModel,
    IReadOnlyDictionary<string, bool>? ColumnVisibilityMap = null);

public sealed record ServerSideRowsResult<TData>(
    IReadOnlyList<TData> Rows,
    int? LastRow = null);

public enum ServerSideRefreshMode
{
    Full,
    Partial
}

public interface IServerSideRowModel<TData> : IRowModel<TData>
{
    int BlockSize { get; }
    bool HasDataSource { get; }
    IReadOnlyList<Row<TData>> RootRows { get; }
    Task EnsureRangeAsync(int startRow, int endRow, CancellationToken cancellationToken = default);
    void SetDataSource(IServerSideDataSource<TData> dataSource, bool refresh = true);
    void Refresh(ServerSideRefreshMode mode = ServerSideRefreshMode.Full, bool purge = false);
    event EventHandler? RowsChanged;
    void ConfigureRetention(int retainMarginBlocks, int maxResidentBlocks);
}
