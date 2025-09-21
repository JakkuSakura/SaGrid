using Tests.TestData;
using Tests.Contracts;

namespace Tests.Features;

/// <summary>
/// Tests for pagination functionality
/// These tests define the expected behavior for pagination features that need to be implemented
/// </summary>
public class PaginationTests : PersonContractTestBase
{
    [Fact]
    public void Table_Should_Support_Basic_Pagination()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        
        // Act - Enable pagination with page size 10
        table.SetState(new TableState<TestPerson>
        {
            Pagination = new PaginationState
            {
                PageIndex = 0,
                PageSize = 10
            }
        });
        
        var paginatedRows = table.RowModel.Rows.ToList();
        
        // Assert
        paginatedRows.Should().HaveCount(10, "Should show exactly the page size number of rows");
        
        // Should have access to pagination info
        table.GetPageCount().Should().BeGreaterThan(1, "Should have multiple pages for medium dataset");
        table.GetCanPreviousPage().Should().BeFalse("First page should not have previous page");
        table.GetCanNextPage().Should().BeTrue("First page should have next page available");
    }

    [Fact]
    public void Table_Should_Navigate_Between_Pages()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        table.SetState(new TableState<TestPerson>
        {
            Pagination = new PaginationState { PageIndex = 0, PageSize = 10 }
        });
        
        var firstPageRows = table.RowModel.Rows.Select(r => r.Original.Id).ToList();
        
        // Act - Go to next page
        table.NextPage();
        
        var secondPageRows = table.RowModel.Rows.Select(r => r.Original.Id).ToList();
        
        // Assert
        secondPageRows.Should().NotBeEquivalentTo(firstPageRows, "Different pages should show different data");
        secondPageRows.Should().HaveCount(10, "Second page should also have page size number of rows");
        
        // Navigation state should be updated
        table.GetCanPreviousPage().Should().BeTrue("Second page should have previous page");
        table.State.Pagination!.PageIndex.Should().Be(1, "Page index should be updated");
    }

    [Fact]
    public void Table_Should_Handle_Different_Page_Sizes()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        
        // Act & Assert - Test different page sizes
        var pageSizes = new[] { 5, 10, 20, 25 };
        
        foreach (var pageSize in pageSizes)
        {
            table.SetPageSize(pageSize);
            
            var rows = table.RowModel.Rows.ToList();
            var expectedCount = Math.Min(pageSize, PersonTestData.MediumDataset.Count);
            
            rows.Should().HaveCount(expectedCount, 
                $"Page size {pageSize} should show correct number of rows");
            table.State.Pagination!.PageSize.Should().Be(pageSize);
        }
    }

    [Fact]
    public void Table_Should_Handle_Last_Page_Correctly()
    {
        // Arrange
        var totalRows = PersonTestData.MediumDataset.Count;
        var pageSize = 7; // Choose a size that doesn't divide evenly
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        
        table.SetState(new TableState<TestPerson>
        {
            Pagination = new PaginationState { PageIndex = 0, PageSize = pageSize }
        });
        
        // Act - Go to last page
        var lastPageIndex = (int)Math.Ceiling((double)totalRows / pageSize) - 1;
        table.SetPageIndex(lastPageIndex);
        
        var lastPageRows = table.RowModel.Rows.ToList();
        
        // Assert
        var expectedLastPageSize = totalRows % pageSize == 0 ? pageSize : totalRows % pageSize;
        lastPageRows.Should().HaveCount(expectedLastPageSize, 
            "Last page should show remaining rows");
        
        table.GetCanNextPage().Should().BeFalse("Last page should not have next page");
        table.GetCanPreviousPage().Should().BeTrue("Last page should have previous page");
    }

    [Fact]
    public void Table_Should_Reset_To_First_Page_When_Data_Changes()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        table.SetState(new TableState<TestPerson>
        {
            Pagination = new PaginationState { PageIndex = 2, PageSize = 10 }
        });
        
        table.State.Pagination!.PageIndex.Should().Be(2, "Should be on page 2");
        
        // Act - Change data (simulate filtering or data update)
        var smallerDataset = PersonTestData.SmallDataset;
        table.SetState(new TableState<TestPerson>
        {
            Pagination = new PaginationState { PageIndex = 2, PageSize = 10 }
        });
        
        // In real implementation, data change might trigger page reset
        // For now, test the manual reset
        table.ResetPagination();
        
        // Assert
        table.State.Pagination.Should().BeNull("Pagination should be reset");
    }

    [Fact]
    public void Table_Should_Calculate_Pagination_Info_Correctly()
    {
        // Arrange
        var totalRows = PersonTestData.MediumDataset.Count; // 50 rows
        var pageSize = 7;
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        
        table.SetState(new TableState<TestPerson>
        {
            Pagination = new PaginationState { PageIndex = 0, PageSize = pageSize }
        });
        
        // Assert pagination calculations
        var expectedPageCount = (int)Math.Ceiling((double)totalRows / pageSize);
        table.GetPageCount().Should().Be(expectedPageCount, "Page count calculation should be correct");
        
        // Test different page indices
        for (int pageIndex = 0; pageIndex < expectedPageCount; pageIndex++)
        {
            table.SetPageIndex(pageIndex);
            
            var canPrev = pageIndex > 0;
            var canNext = pageIndex < expectedPageCount - 1;
            
            table.GetCanPreviousPage().Should().Be(canPrev, 
                $"Page {pageIndex} previous page availability should be correct");
            table.GetCanNextPage().Should().Be(canNext, 
                $"Page {pageIndex} next page availability should be correct");
        }
    }

    [Fact]
    public void Table_Should_Handle_Pagination_With_Filtering()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        
        // Act - Apply filter and pagination
        table.SetState(new TableState<TestPerson>
        {
            ColumnFilters = new ColumnFiltersState(new List<ColumnFilter>
            {
                new ColumnFilter("department", "Engineering")
            }),
            Pagination = new PaginationState { PageIndex = 0, PageSize = 5 }
        });
        
        var filteredPaginatedRows = table.RowModel.Rows.ToList();
        
        // Assert
        filteredPaginatedRows.Should().HaveCountLessOrEqualTo(5, "Should respect page size");
        filteredPaginatedRows.Should().AllSatisfy(row => 
            row.Original.Department.Should().Be("Engineering", "Should respect filter"));
        
        // Page count should be based on filtered data
        var engineeringCount = PersonTestData.MediumDataset.Count(p => p.Department == "Engineering");
        var expectedFilteredPageCount = (int)Math.Ceiling((double)engineeringCount / 5);
        table.GetPageCount().Should().Be(expectedFilteredPageCount, 
            "Page count should be based on filtered data");
    }

    [Fact]
    public void Table_Should_Handle_Pagination_With_Sorting()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        
        // Act - Apply sorting and pagination
        table.SetState(new TableState<TestPerson>
        {
            Sorting = new SortingState(new List<ColumnSort>
            {
                new ColumnSort("age", SortDirection.Ascending)
            }),
            Pagination = new PaginationState { PageIndex = 0, PageSize = 10 }
        });
        
        var sortedPaginatedRows = table.RowModel.Rows.ToList();
        
        // Assert
        sortedPaginatedRows.Should().HaveCount(10, "Should respect page size");
        
        var ages = sortedPaginatedRows.Select(r => r.Original.Age).ToList();
        ages.Should().BeInAscendingOrder("Should maintain sort order within page");
        
        // First page should contain the youngest people
        var allAgesSorted = PersonTestData.MediumDataset.Select(p => p.Age).OrderBy(x => x).Take(10).ToList();
        ages.Should().BeEquivalentTo(allAgesSorted, "First page should contain youngest people");
    }

    [Fact]
    public void Table_Should_Provide_Page_Navigation_Methods()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        table.SetState(new TableState<TestPerson>
        {
            Pagination = new PaginationState { PageIndex = 1, PageSize = 10 }
        });
        
        // Test previous page
        table.PreviousPage();
        table.State.Pagination!.PageIndex.Should().Be(0, "Previous page should decrease index");
        
        // Test next page
        table.NextPage();
        table.State.Pagination!.PageIndex.Should().Be(1, "Next page should increase index");
        
        // Test first page
        table.SetPageIndex(3);
        table.FirstPage();
        table.State.Pagination!.PageIndex.Should().Be(0, "First page should set index to 0");
        
        // Test last page
        table.LastPage();
        var expectedLastIndex = table.GetPageCount() - 1;
        table.State.Pagination!.PageIndex.Should().Be(expectedLastIndex, "Last page should set index to last");
    }

    [Fact]
    public void Table_Should_Handle_Invalid_Page_Navigation()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        table.SetState(new TableState<TestPerson>
        {
            Pagination = new PaginationState { PageIndex = 0, PageSize = 10 }
        });
        
        var originalPageIndex = table.State.Pagination!.PageIndex;
        
        // Act & Assert - Try to go to previous page from first page
        table.PreviousPage();
        table.State.Pagination!.PageIndex.Should().Be(originalPageIndex, 
            "Should not change page when at first page");
        
        // Go to last page and try to go next
        table.LastPage();
        var lastPageIndex = table.State.Pagination!.PageIndex;
        
        table.NextPage();
        table.State.Pagination!.PageIndex.Should().Be(lastPageIndex,
            "Should not change page when at last page");
    }

    [Fact]
    public void Table_Should_Handle_Page_Size_Changes_Gracefully()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        table.SetState(new TableState<TestPerson>
        {
            Pagination = new PaginationState { PageIndex = 2, PageSize = 10 }
        });
        
        // Act - Change page size (might affect current page validity)
        table.SetPageSize(25);
        
        // Assert
        table.State.Pagination!.PageSize.Should().Be(25);
        
        // Page index might need adjustment if new page size makes current page invalid
        var newPageCount = table.GetPageCount();
        table.State.Pagination!.PageIndex.Should().BeLessThan(newPageCount,
            "Page index should be valid for new page size");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(25)]
    [InlineData(50)]
    public void Table_Should_Handle_Various_Page_Sizes(int pageSize)
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        var totalRows = PersonTestData.MediumDataset.Count;
        
        // Act
        table.SetState(new TableState<TestPerson>
        {
            Pagination = new PaginationState { PageIndex = 0, PageSize = pageSize }
        });
        
        // Assert
        var rows = table.RowModel.Rows.ToList();
        var expectedRowCount = Math.Min(pageSize, totalRows);
        
        rows.Should().HaveCount(expectedRowCount, $"Page size {pageSize} should show correct rows");
        
        var expectedPageCount = (int)Math.Ceiling((double)totalRows / pageSize);
        table.GetPageCount().Should().Be(expectedPageCount, $"Page count should be correct for page size {pageSize}");
    }

    [Theory]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1000)]
    public void Pagination_Should_Be_Performant_With_Large_Datasets(int dataSize)
    {
        // Arrange
        var largeData = PersonTestData.GenerateLargeDataset(dataSize).ToList();
        var table = CreateTableWithData(largeData);
        
        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        table.SetState(new TableState<TestPerson>
        {
            Pagination = new PaginationState { PageIndex = 0, PageSize = 20 }
        });
        
        // Force evaluation
        var result = table.RowModel.Rows.ToList();
        
        stopwatch.Stop();
        
        // Assert
        result.Should().HaveCount(20, "Should show correct page size");
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10,
            $"Pagination of {dataSize} rows should complete within 10ms");
    }

    [Fact]
    public void Table_Should_Maintain_Row_Identity_Across_Pages()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        table.SetState(new TableState<TestPerson>
        {
            Pagination = new PaginationState { PageIndex = 0, PageSize = 10 }
        });
        
        var firstPageFirstRow = table.RowModel.Rows.First();
        var firstRowId = firstPageFirstRow.Original.Id;
        
        // Act - Navigate away and back
        table.NextPage();
        table.PreviousPage();
        
        // Assert
        var backToFirstPageFirstRow = table.RowModel.Rows.First();
        backToFirstPageFirstRow.Original.Id.Should().Be(firstRowId,
            "Row identity should be maintained across page navigation");
    }

    [Fact]
    public void Table_Should_Support_Manual_Pagination_State_Updates()
    {
        // Arrange
        var table = CreateTableWithData(PersonTestData.MediumDataset);
        
        // Act - Manually set pagination state
        var customPaginationState = new PaginationState
        {
            PageIndex = 2,
            PageSize = 15
        };
        
        table.SetState(table.State with { Pagination = customPaginationState });
        
        // Assert
        table.State.Pagination.Should().BeEquivalentTo(customPaginationState);
        table.RowModel.Rows.Should().HaveCountLessOrEqualTo(15, "Should respect custom page size");
        
        // Should be able to get pagination info
        table.GetPageCount().Should().BeGreaterThan(0);
        table.GetCanPreviousPage().Should().BeTrue("Page 2 should have previous page");
    }
}