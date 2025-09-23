using System;
using System.Collections.Generic;
using SaGrid.Core;

namespace SaGrid.Advanced.Interfaces;

/// <summary>
/// Row model interface mirroring AG Grid's IRowModel.
/// Defines the contract for different row model types (client-side, server-side, etc.)
/// </summary>
public interface IRowModel<TData>
{
    /// <summary>
    /// Returns the row at the given index
    /// </summary>
    Row<TData>? GetRow(int index);

    /// <summary>
    /// Returns the row for the given ID
    /// </summary>
    Row<TData>? GetRowById(string id);

    /// <summary>
    /// Returns the number of rows
    /// </summary>
    int GetRowCount();

    /// <summary>
    /// Returns the top level row count (for grouping)
    /// </summary>
    int GetTopLevelRowCount();

    /// <summary>
    /// Returns true if this model has no rows, regardless of filters
    /// </summary>
    bool IsEmpty();

    /// <summary>
    /// Returns true if no rows to render (empty or filtered out)
    /// </summary>
    bool IsRowsToRender();

    /// <summary>
    /// Iterate through each node
    /// </summary>
    void ForEachRow(Action<Row<TData>, int> callback);

    /// <summary>
    /// The row model type
    /// </summary>
    RowModelType GetRowModelType();

    /// <summary>
    /// Returns true if the last row index is known
    /// </summary>
    bool IsLastRowIndexKnown();

    /// <summary>
    /// Initialize the row model with data
    /// </summary>
    void Start();

    /// <summary>
    /// Reset row heights
    /// </summary>
    void ResetRowHeights();

    /// <summary>
    /// Called when row heights change
    /// </summary>
    void OnRowHeightChanged();
}

/// <summary>
/// Row model types following AG Grid's pattern
/// </summary>
public enum RowModelType
{
    ClientSide,
    ServerSide,
    Infinite,
    Viewport
}