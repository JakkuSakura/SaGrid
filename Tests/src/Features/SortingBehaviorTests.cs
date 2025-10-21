using System.Linq;
using FluentAssertions;
using SaGrid.Advanced;
using SaGrid.Core;
using Tests.Contracts;
using Tests.TestData;
using Xunit;

namespace Tests.Features;

public class SortingBehaviorTests : PersonContractTestBase
{
    [Fact]
    public void SaGrid_Should_Sort_By_FirstName()
    {
        var data = PersonTestData.GenerateLargeDataset(40).ToList();
        var options = new TableOptions<TestPerson>
        {
            Data = data,
            Columns = PersonTestData.StandardColumns,
            EnableSorting = true
        };

        var grid = new SaGrid<TestPerson>(options);
        grid.SetSorting(new[] { new ColumnSort("firstName", SortDirection.Ascending) });

        var firstNames = grid.RowModel.Rows
            .Select(r => r.GetCell("firstName").Value?.ToString())
            .ToList();

        firstNames.Should().BeInAscendingOrder();
    }
}
