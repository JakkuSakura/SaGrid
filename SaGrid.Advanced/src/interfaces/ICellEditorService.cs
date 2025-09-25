using System;
using SaGrid.Advanced.Modules.Editing;
using SaGrid.Core;

namespace SaGrid.Advanced.Interfaces;

public interface ICellEditorService<TData>
{
    event EventHandler<CellEditingChangedEventArgs<TData>>? EditingStateChanged;

    bool BeginEdit(SaGrid<TData> grid, Row<TData> row, Column<TData> column);
    bool CommitEdit(SaGrid<TData> grid);
    void CancelEdit(SaGrid<TData> grid);
    CellEditingSession<TData>? GetActiveSession(SaGrid<TData> grid);

    void RegisterEditor(string editorKey, Func<ICellEditor<TData>> factory);
    void AssignColumnEditor(string columnId, string editorKey);

    BatchEditManager<TData> GetBatchManager(SaGrid<TData> grid);
    void BeginBatch(SaGrid<TData> grid);
    void CommitBatch(SaGrid<TData> grid);
    void CancelBatch(SaGrid<TData> grid);
    void Undo(SaGrid<TData> grid);
    void Redo(SaGrid<TData> grid);
}

public interface ICellEditorRegistry
{
    ICellEditorService<TData> GetOrCreate<TData>();
}
