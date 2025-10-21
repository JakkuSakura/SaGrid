using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using SaGrid.Advanced;
using SaGrid.Advanced.Events;
using SaGrid.Advanced.Interfaces;
using SaGrid.Core;

namespace SaGrid.Advanced.Modules.Export;

internal sealed class ExportCoordinator : IExportCoordinator
{
    private const string ContextMenuId = "export.options";

    private readonly ExportService _exportService;
    private readonly IEventService _eventService;
    private readonly ConditionalWeakTable<object, ExportAttachment> _attachments = new();

    public ExportCoordinator(ExportService exportService, IEventService eventService)
    {
        _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
        _eventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
    }

    public void AttachToGrid<TData>(SaGrid<TData> grid)
    {
        if (grid == null) throw new ArgumentNullException(nameof(grid));

        var attachment = _attachments.GetValue(grid, _ => new ExportAttachment());
        if (attachment.ContextMenuAttached)
        {
            return;
        }

        EnsureContextMenu(grid, attachment);
    }

    public async Task<ExportResult?> ShowExportDialogAsync<TData>(SaGrid<TData> grid, Window? owner = null)
    {
        if (grid == null) throw new ArgumentNullException(nameof(grid));

        owner ??= GetActiveWindow();
        if (owner == null)
        {
            throw new InvalidOperationException("An active window is required to show the export dialog.");
        }

        var descriptors = grid.AllLeafColumns
            .Select(column => new ColumnDescriptor(
                column.Id,
                column.ColumnDef.Header?.ToString() ?? column.Id,
                grid.VisibleLeafColumns.Contains(column)))
            .ToList();

        ExportRequest? request;

        if (Dispatcher.UIThread.CheckAccess())
        {
            request = await ShowDialogInternalAsync(descriptors, owner);
        }
        else
        {
            var operation = Dispatcher.UIThread.InvokeAsync(() => ShowDialogInternalAsync(descriptors, owner));
            request = await operation;
        }

        if (request == null)
        {
            return null;
        }

        var result = ExecuteExport(grid, request);

        if (RequiresClipboardCopy(request) && result.HasText)
        {
            await CopyToClipboardAsync(owner, result.TextPayload!);
        }

        return result;
    }

    public ExportResult ExecuteExport<TData>(SaGrid<TData> grid, ExportRequest request)
    {
        if (grid == null) throw new ArgumentNullException(nameof(grid));
        if (request == null) throw new ArgumentNullException(nameof(request));

        var result = _exportService.Export(grid, request);
        _eventService.DispatchEvent(GridEventTypes.ExportPerformed,
            new ExportPerformedEventArgs(grid, request, result));
        return result;
    }

    private void EnsureContextMenu<TData>(SaGrid<TData> grid, ExportAttachment attachment)
    {
        var existing = grid.GetContextMenuItems().ToList();
        if (existing.Any(item => string.Equals(item.Id, ContextMenuId, StringComparison.OrdinalIgnoreCase)))
        {
            attachment.ContextMenuAttached = true;
            return;
        }

        existing.Add(new ContextMenuItem(ContextMenuId, "Export...")
        {
            Action = _ => _ = ShowExportDialogAsync(grid)
        });

        grid.SetContextMenuItems(existing);
        attachment.ContextMenuAttached = true;
    }

    private static Window? GetActiveWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.Windows.FirstOrDefault(w => w.IsActive)
                   ?? desktop.MainWindow
                   ?? desktop.Windows.FirstOrDefault();
        }

        return null;
    }

    private static bool RequiresClipboardCopy(ExportRequest request)
    {
        return request.Format is ExportFormat.ClipboardPlain or ExportFormat.ClipboardTab;
    }

    private static async Task CopyToClipboardAsync(Window owner, string text)
    {
        if (owner?.Clipboard is not { } provider)
        {
            throw new InvalidOperationException("Clipboard is not available on the current window.");
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            await provider.SetTextAsync(text);
        }
        else
        {
            await Dispatcher.UIThread.InvokeAsync(() => provider.SetTextAsync(text));
        }
    }

    private static Task<ExportRequest?> ShowDialogInternalAsync(IEnumerable<ColumnDescriptor> descriptors, Window owner)
    {
        var dialog = new ExportOptionsDialog(descriptors);
        return dialog.ShowDialog<ExportRequest?>(owner);
    }

    private sealed class ExportAttachment
    {
        public bool ContextMenuAttached;
    }
}
