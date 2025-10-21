using System.Threading.Tasks;
using Avalonia.Controls;
using SaGrid.Advanced;
using SaGrid.Advanced.Modules.Export;

namespace SaGrid.Advanced.Interfaces;

public interface IExportCoordinator
{
    void AttachToGrid<TData>(SaGrid<TData> grid);

    Task<ExportResult?> ShowExportDialogAsync<TData>(SaGrid<TData> grid, Window? owner = null);

    ExportResult ExecuteExport<TData>(SaGrid<TData> grid, ExportRequest request);
}
