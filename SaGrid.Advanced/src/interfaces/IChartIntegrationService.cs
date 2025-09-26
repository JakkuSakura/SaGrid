using SaGrid;
using SaGrid.Advanced.Modules.Analytics;

namespace SaGrid.Advanced.Interfaces;

public interface IChartIntegrationService
{
    void AttachToGrid<TData>(SaGrid<TData> grid);

    ChartRequest BuildDefaultRequest<TData>(SaGrid<TData> grid);

    ChartData BuildChartData<TData>(SaGrid<TData> grid, ChartRequest request);

    bool ShowChart<TData>(SaGrid<TData> grid, ChartRequest request);

    bool TryShowDefaultChart<TData>(SaGrid<TData> grid);
}
