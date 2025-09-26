using System.Collections.Generic;

namespace SaGrid.Advanced.Modules.Analytics;

public enum ChartType
{
    Column,
    Bar,
    Line,
    Area,
    Pie
}

public record ChartRequest(
    ChartType ChartType,
    string CategoryColumnId,
    IReadOnlyList<string> ValueColumnIds,
    bool IncludeLeafRows = true,
    bool IncludeGroupRows = true
);

public record ChartSeries(string ColumnId, string DisplayName, IReadOnlyList<double> Points);

public record ChartData(
    ChartType ChartType,
    IReadOnlyList<string> Categories,
    IReadOnlyList<ChartSeries> Series,
    string? Title = null,
    string? Subtitle = null
)
{
    public bool HasData => Series.Count > 0 && Categories.Count > 0;
}
