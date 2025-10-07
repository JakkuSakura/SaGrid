using System.Linq;
using FluentAssertions;
using SaGrid;
using SaGrid.Advanced;
using SaGrid.Advanced.RowModel;
using SaGrid.Core;
using Tests.Contracts;
using Tests.TestData;
using Xunit;

namespace Tests.Features;

public class FilteringBehaviorTests : PersonContractTestBase
{
    [Fact]
    public void SaGrid_Should_Filter_Rows_By_Text_Filter()
    {
        var data = PersonTestData.GenerateLargeDataset(200).ToList();
        var options = new TableOptions<TestPerson>
        {
            Data = data,
            Columns = PersonTestData.StandardColumns,
            EnableSorting = true,
            EnableColumnFilters = true,
            EnableRowSelection = true,
            EnableCellSelection = true,
            EnableGlobalFilter = true
        };

        var grid = new SaGrid<TestPerson>(options);

        grid.SetColumnFilter("department", "Engineering");

        var filteredRows = grid.RowModel.Rows;
        filteredRows.Should().NotBeEmpty();
        filteredRows.Count.Should().BeLessThan(data.Count);

        grid.GetApproximateRowCount().Should().Be(filteredRows.Count);

        filteredRows.All(r =>
        {
            var cellValue = r.GetCell("department").Value;
            return cellValue != null &&
                   cellValue.ToString()!.Contains("Engineering", StringComparison.OrdinalIgnoreCase);
        }).Should().BeTrue("every row should match the department filter");
    }

}
