using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using SaGrid.Advanced;
using SaGrid.Advanced.Interfaces;
using SaGrid.Core;
using SaGrid.Core.Models;
using Tests.Contracts;
using Tests.TestData;
using Xunit;

namespace Tests.Features;

public class ServerSideFilteringAndSortingTests : PersonContractTestBase
{
    private static SaGrid<TestPerson> CreateServerSideGrid(IEnumerable<TestPerson> data)
    {
        var options = new TableOptions<TestPerson>
        {
            Data = Array.Empty<TestPerson>(),
            Columns = PersonTestData.StandardColumns,
            EnableSorting = true,
            EnableGlobalFilter = true,
            EnableColumnFilters = true,
            Meta = new Dictionary<string, object>
            {
                ["rowModelType"] = RowModelType.ServerSide,
                ["serverSideBlockSize"] = 128
            }
        };

        var grid = new SaGrid<TestPerson>(options);
        grid.SetServerSideDataSource(new InMemoryServerDataSource(data.ToList(), PersonTestData.StandardColumns));
        return grid;
    }

    [Fact]
    public async Task ServerSide_Should_Filter_By_Text()
    {
        var data = PersonTestData.GenerateLargeDataset(500).ToList();
        var grid = CreateServerSideGrid(data);

        grid.SetColumnFilter("department", "Engineering");

        var model = grid.GetServerSideRowModel();
        model.Should().NotBeNull();
        await model!.EnsureRangeAsync(0, 200);

        var rows = new List<Row<TestPerson>>();
        model.ForEachRow((r, _) => rows.Add(r));

        rows.Should().NotBeEmpty();
        rows.Should().AllSatisfy(r => r.Original.Department.Should().Be("Engineering"));
    }

    [Fact]
    public async Task ServerSide_Should_Filter_By_SetFilterState()
    {
        var data = PersonTestData.GenerateLargeDataset(500).ToList();
        var grid = CreateServerSideGrid(data);

        grid.SetColumnFilter("department", new SetFilterState(new[] { "Marketing" }));

        var model = grid.GetServerSideRowModel();
        model.Should().NotBeNull();
        await model!.EnsureRangeAsync(0, 200);

        var rows = new List<Row<TestPerson>>();
        model.ForEachRow((r, _) => rows.Add(r));

        rows.Should().NotBeEmpty();
        rows.Should().AllSatisfy(r => r.Original.Department.Should().Be("Marketing"));
    }

    [Fact]
    public async Task ServerSide_Should_Filter_By_Range()
    {
        var data = PersonTestData.GenerateLargeDataset(500).ToList();
        var grid = CreateServerSideGrid(data);

        grid.SetColumnFilter("age", new { min = 30, max = 40 });

        var model = grid.GetServerSideRowModel();
        model.Should().NotBeNull();
        await model!.EnsureRangeAsync(0, 200);

        var rows = new List<Row<TestPerson>>();
        model.ForEachRow((r, _) => rows.Add(r));

        rows.Should().NotBeEmpty();
        rows.Should().AllSatisfy(r =>
        {
            r.Original.Age.Should().BeGreaterOrEqualTo(30);
            r.Original.Age.Should().BeLessOrEqualTo(40);
        });
    }

    [Fact]
    public async Task ServerSide_Should_Apply_Sorting()
    {
        var data = PersonTestData.GenerateLargeDataset(200).ToList();
        var grid = CreateServerSideGrid(data);

        grid.SetSorting(new[] { new ColumnSort("firstName", SortDirection.Ascending) });

        var model = grid.GetServerSideRowModel();
        model.Should().NotBeNull();
        await model!.EnsureRangeAsync(0, 200);

        var rows = new List<Row<TestPerson>>();
        model.ForEachRow((r, _) => rows.Add(r));

        rows.Should().NotBeEmpty();
        var firstNames = rows.Select(r => r.Original.FirstName).ToList();
        firstNames.Should().BeInAscendingOrder();
    }

    private sealed class InMemoryServerDataSource : IServerSideDataSource<TestPerson>
    {
        private readonly List<TestPerson> _rows;
        private readonly IReadOnlyList<ColumnDef<TestPerson>> _columns;

        public InMemoryServerDataSource(List<TestPerson> rows, IReadOnlyList<ColumnDef<TestPerson>> columns)
        {
            _rows = rows;
            _columns = columns;
        }

        public Task<ServerSideRowsResult<TestPerson>> GetRowsAsync(ServerSideRowsRequest request, System.Threading.CancellationToken cancellationToken = default)
        {
            // Rebuild Core state from request and return the filtered/sorted slice
            var state = BuildStateFromRequest(request);

            var table = new Table<TestPerson>(new TableOptions<TestPerson>
            {
                Data = _rows,
                Columns = _columns,
                EnableSorting = true,
                EnableGlobalFilter = true,
                EnableColumnFilters = true,
                State = state
            });

            var start = Math.Max(0, request.StartRow);
            var end = Math.Max(start, request.EndRow);

            var filtered = table.RowModel.Rows.Select(r => r.Original).ToList();
            start = Math.Clamp(start, 0, filtered.Count);
            end = Math.Clamp(end, start, filtered.Count);
            var slice = filtered.Skip(start).Take(Math.Max(0, end - start)).ToList();

            return Task.FromResult(new ServerSideRowsResult<TestPerson>(slice, filtered.Count));
        }

        private static TableState<TestPerson> BuildStateFromRequest(ServerSideRowsRequest request)
        {
            var filters = new List<ColumnFilter>();
            object? global = null;
            foreach (var kv in request.FilterModel)
            {
                if (string.Equals(kv.Key, "__global", StringComparison.OrdinalIgnoreCase))
                {
                    global = kv.Value;
                }
                else if (kv.Value is IReadOnlyDictionary<string, object?> dict)
                {
                    dict.TryGetValue("min", out var minObj);
                    dict.TryGetValue("max", out var maxObj);
                    filters.Add(new ColumnFilter(kv.Key, new RangeProxy(minObj, maxObj)));
                }
                else
                {
                    filters.Add(new ColumnFilter(kv.Key, kv.Value));
                }
            }

            return new TableState<TestPerson>
            {
                Sorting = new SortingState(request.SortModel?.ToList() ?? new List<ColumnSort>()),
                ColumnFilters = filters.Count > 0 ? new ColumnFiltersState(filters) : null,
                GlobalFilter = global != null ? new GlobalFilterState(global) : null,
                ColumnVisibility = request.ColumnVisibilityMap != null
                    ? new ColumnVisibilityState(new Dictionary<string, bool>(request.ColumnVisibilityMap))
                    : null
            };
        }

        private sealed record RangeProxy(object? min, object? max);
    }
}
