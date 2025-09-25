using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Input;
using SaGrid.Advanced.Events;
using SaGrid.Advanced.Interfaces;
using SaGrid.Core;

namespace SaGrid.Advanced.Modules.Editing;

public sealed class CellEditorService<TData> : ICellEditorService<TData>
{
    private readonly Dictionary<string, Func<ICellEditor<TData>>> _editorFactories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _columnEditorMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConditionalWeakTable<SaGrid<TData>, GridEditingState> _gridStates = new();

    public event EventHandler<CellEditingChangedEventArgs<TData>>? EditingStateChanged;

    public CellEditorService()
    {
        RegisterDefaultEditors();
    }

    public bool BeginEdit(SaGrid<TData> grid, Row<TData> row, Column<TData> column)
    {
        var state = GetOrCreateState(grid);

        if (state.ActiveSession != null)
        {
            if (!CommitEdit(grid))
            {
                CancelEdit(grid);
            }
        }

        var editor = ResolveEditor(grid, column);
        var commitAction = new Action(() => CommitEdit(grid));
        var cancelAction = new Action(() => CancelEdit(grid));

        var meta = column.ColumnDef.Meta != null
            ? new Dictionary<string, object>(column.ColumnDef.Meta)
            : new Dictionary<string, object>();

        var context = new CellEditorContext<TData>(grid, row, column, row.GetCell(column.Id).Value, meta, commitAction, cancelAction);
        var control = editor.BuildEditor(context);
        editor.SetInitialValue(context.InitialValue);

        AttachEditorEvents(control, commitAction, cancelAction);

        state.ActiveSession = new CellEditingSession<TData>(row, column, editor, control, context.InitialValue);
        grid.DispatchEvent(GridEventTypes.CellEditStarted, new CellEditStartedEventArgs<TData>(grid, row, column));
        RaiseEditingChanged(grid, state.ActiveSession);
        FocusEditor(control);
        return true;
    }

    public bool CommitEdit(SaGrid<TData> grid)
    {
        var state = GetOrCreateState(grid);
        if (state.ActiveSession == null)
        {
            return false;
        }

        var session = state.ActiveSession;
        if (!session.Editor.Validate(out var message))
        {
            if (!string.IsNullOrEmpty(message))
            {
                // TODO: surface validation feedback via event/log
            }
            return false;
        }

        var newValue = session.Editor.GetValue();
        var started = state.BatchManager.RecordEdit(grid, session.Row, session.Column, newValue);
        if (started)
        {
            grid.DispatchEvent(GridEventTypes.BatchEditStarted, new BatchEditEventArgs<TData>(grid, GridEventTypes.BatchEditStarted, Array.Empty<CellEditEntry<TData>>()));
        }

        state.ActiveSession = null;
        RaiseEditingChanged(grid, null);

        grid.DispatchEvent(GridEventTypes.CellEditCommitted, new CellEditCommittedEventArgs<TData>(grid, session.Row, session.Column, session.OriginalValue, newValue));
        return true;
    }

    public void CancelEdit(SaGrid<TData> grid)
    {
        var state = GetOrCreateState(grid);
        if (state.ActiveSession == null)
        {
            return;
        }

        var session = state.ActiveSession;
        state.ActiveSession = null;
        RaiseEditingChanged(grid, null);
        grid.DispatchEvent(GridEventTypes.CellEditCancelled, new CellEditCancelledEventArgs<TData>(grid, session.Row, session.Column));
    }

    public CellEditingSession<TData>? GetActiveSession(SaGrid<TData> grid)
    {
        return GetOrCreateState(grid).ActiveSession;
    }

    public void RegisterEditor(string editorKey, Func<ICellEditor<TData>> factory)
    {
        _editorFactories[editorKey] = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public void AssignColumnEditor(string columnId, string editorKey)
    {
        _columnEditorMap[columnId] = editorKey;
    }

    public BatchEditManager<TData> GetBatchManager(SaGrid<TData> grid)
    {
        return GetOrCreateState(grid).BatchManager;
    }

    public void BeginBatch(SaGrid<TData> grid)
    {
        GetOrCreateState(grid).BatchManager.BeginBatch();
        grid.DispatchEvent(GridEventTypes.BatchEditStarted, new BatchEditEventArgs<TData>(grid, GridEventTypes.BatchEditStarted, Array.Empty<CellEditEntry<TData>>()));
    }

    public void CommitBatch(SaGrid<TData> grid)
    {
        var state = GetOrCreateState(grid);
        var committed = state.BatchManager.Commit(grid);
        grid.RefreshModel(new RefreshModelParams(ClientSideRowModelStage.Everything));
        grid.ScheduleUIUpdate();
        grid.DispatchEvent(GridEventTypes.BatchEditCommitted, new BatchEditEventArgs<TData>(grid, GridEventTypes.BatchEditCommitted, committed));
    }

    public void CancelBatch(SaGrid<TData> grid)
    {
        var state = GetOrCreateState(grid);
        var cancelled = state.BatchManager.Cancel(grid);
        grid.RefreshModel(new RefreshModelParams(ClientSideRowModelStage.Everything));
        grid.ScheduleUIUpdate();
        grid.DispatchEvent(GridEventTypes.BatchEditCancelled, new BatchEditEventArgs<TData>(grid, GridEventTypes.BatchEditCancelled, cancelled));
    }

    public void Undo(SaGrid<TData> grid)
    {
        var state = GetOrCreateState(grid);
        var edits = state.BatchManager.Undo(grid);
        if (edits.Count > 0)
        {
            grid.RefreshModel(new RefreshModelParams(ClientSideRowModelStage.Everything));
            grid.ScheduleUIUpdate();
            grid.DispatchEvent(GridEventTypes.BatchEditUndone, new BatchEditEventArgs<TData>(grid, GridEventTypes.BatchEditUndone, edits));
        }
    }

    public void Redo(SaGrid<TData> grid)
    {
        var state = GetOrCreateState(grid);
        var edits = state.BatchManager.Redo(grid);
        if (edits.Count > 0)
        {
            grid.RefreshModel(new RefreshModelParams(ClientSideRowModelStage.Everything));
            grid.ScheduleUIUpdate();
            grid.DispatchEvent(GridEventTypes.BatchEditRedone, new BatchEditEventArgs<TData>(grid, GridEventTypes.BatchEditRedone, edits));
        }
    }

    private GridEditingState GetOrCreateState(SaGrid<TData> grid)
    {
        return _gridStates.GetValue(grid, _ => new GridEditingState());
    }

    private ICellEditor<TData> ResolveEditor(SaGrid<TData> grid, Column<TData> column)
    {
        if (_columnEditorMap.TryGetValue(column.Id, out var key) && _editorFactories.TryGetValue(key, out var factory))
        {
            return factory();
        }

        var editorKey = ResolveEditorKeyFromMeta(column) ?? ResolveEditorKeyFromType(column);
        if (editorKey != null && _editorFactories.TryGetValue(editorKey, out var resolvedFactory))
        {
            return resolvedFactory();
        }

        return _editorFactories[CellEditorKeys.Text]();
    }

    private static string? ResolveEditorKeyFromMeta(Column<TData> column)
    {
        if (column.ColumnDef.Meta != null && column.ColumnDef.Meta.TryGetValue("editor", out var editorValue) && editorValue is string editorKey)
        {
            return editorKey;
        }

        return null;
    }

    private string? ResolveEditorKeyFromType(Column<TData> column)
    {
        var columnDefType = column.ColumnDef.GetType();
        if (columnDefType.IsGenericType && columnDefType.GetGenericTypeDefinition() == typeof(ColumnDef<,>))
        {
            var valueType = columnDefType.GetGenericArguments()[1];

            if (valueType == typeof(bool))
            {
                return CellEditorKeys.Checkbox;
            }

            if (valueType == typeof(DateTime) || valueType == typeof(DateTime?))
            {
                return CellEditorKeys.Date;
            }

            if (valueType.IsEnum)
            {
                return CellEditorKeys.Dropdown;
            }

            if (IsNumericType(valueType))
            {
                return CellEditorKeys.Numeric;
            }
        }

        return null;
    }

    private static bool IsNumericType(Type type)
    {
        return type == typeof(byte) || type == typeof(sbyte) ||
               type == typeof(short) || type == typeof(ushort) ||
               type == typeof(int) || type == typeof(uint) ||
               type == typeof(long) || type == typeof(ulong) ||
               type == typeof(float) || type == typeof(double) ||
               type == typeof(decimal);
    }

    private void RegisterDefaultEditors()
    {
        RegisterEditor(CellEditorKeys.Text, () => new TextCellEditor<TData>());
        RegisterEditor(CellEditorKeys.Numeric, () => new NumericCellEditor<TData>());
        RegisterEditor(CellEditorKeys.Date, () => new DateCellEditor<TData>());
        RegisterEditor(CellEditorKeys.Checkbox, () => new CheckboxCellEditor<TData>());
        RegisterEditor(CellEditorKeys.Dropdown, () => new DropdownCellEditor<TData>());
        RegisterEditor(CellEditorKeys.MultiSelect, () => new MultiSelectCellEditor<TData>());
    }

    private void RaiseEditingChanged(SaGrid<TData> grid, CellEditingSession<TData>? session)
    {
        EditingStateChanged?.Invoke(this, new CellEditingChangedEventArgs<TData>(grid, session));
    }

    private static void AttachEditorEvents(Control control, Action commit, Action cancel)
    {
        control.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                commit();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                cancel();
                e.Handled = true;
            }
        };

        control.LostFocus += (_, _) => commit();
    }

    private static void FocusEditor(Control control)
    {
        if (!control.Focus())
        {
            control.Focusable = true;
            control.Focus();
        }
    }

    private sealed class GridEditingState
    {
        public CellEditingSession<TData>? ActiveSession;
        public BatchEditManager<TData> BatchManager { get; } = new();
    }
}

public static class CellEditorKeys
{
    public const string Text = "text";
    public const string Numeric = "numeric";
    public const string Date = "date";
    public const string Checkbox = "checkbox";
    public const string Dropdown = "dropdown";
    public const string MultiSelect = "multiSelect";
}
