using System.Collections.Concurrent;
using System.Diagnostics;
using SaGrid.Core.Models;
using Tests.TestData;

namespace Tests.Contracts;

/// <summary>
/// Performance contract tests that verify the table meets performance requirements
/// These tests validate that operations complete within acceptable time limits
/// </summary>
public class PerformanceContractTests : PersonContractTestBase
{
    [Theory]
    [InlineData(1000)]
    [InlineData(5000)]
    [InlineData(10000)]
    public void Table_Creation_Should_Complete_Within_Time_Limit(int rowCount)
    {
        // Arrange
        var data = PersonTestData.GenerateLargeDataset(rowCount).ToList();
        var options = new TableOptions<TestPerson>
        {
            Data = data,
            Columns = PersonTestData.StandardColumns
        };

        // Act
        var stopwatch = Stopwatch.StartNew();
        var table = new Table<TestPerson>(options);
        stopwatch.Stop();

        // Assert
        var maxTimeMs = rowCount switch
        {
            <= 1000 => 100,
            <= 5000 => 300,
            <= 10000 => 500,
            _ => 1000
        };

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxTimeMs,
            $"Table creation with {rowCount} rows should complete within {maxTimeMs}ms");
        
        // Verify the table was created correctly
        table.RowModel.Rows.Should().HaveCount(rowCount);
        VerifyBasicTableProperties(table);
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(5000)]
    [InlineData(10000)]
    public void Basic_Operations_Should_Be_Fast_With_Large_Datasets(int rowCount)
    {
        // Arrange
        var data = PersonTestData.GenerateLargeDataset(rowCount).ToList();
        var options = new TableOptions<TestPerson>
        {
            Data = data,
            Columns = PersonTestData.StandardColumns
        };
        var table = new Table<TestPerson>(options);

        // Act & Assert - Test various basic operations
        var stopwatch = Stopwatch.StartNew();
        
        // Test property access
        _ = table.AllColumns.Count;
        _ = table.VisibleLeafColumns.Count;
        _ = table.RowModel.Rows.Count;
        _ = table.HeaderGroups.Count;
        
        stopwatch.Stop();

        // Basic property access should be very fast
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(50,
            "Basic property access should complete within 50ms regardless of dataset size");
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(5000)]
    public void Column_Lookup_Should_Be_Fast(int rowCount)
    {
        // Arrange
        var data = PersonTestData.GenerateLargeDataset(rowCount).ToList();
        var options = new TableOptions<TestPerson>
        {
            Data = data,
            Columns = PersonTestData.StandardColumns
        };
        var table = new Table<TestPerson>(options);
        var columnIds = PersonTestData.StandardColumns.Select(c => c.Id).ToList();

        // Act
        var stopwatch = Stopwatch.StartNew();
        
        foreach (var columnId in columnIds)
        {
            _ = table.GetColumn(columnId);
        }
        
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10,
            "Column lookup operations should be very fast");
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(5000)]
    public void State_Reset_Operations_Should_Be_Fast(int rowCount)
    {
        // Arrange
        var data = PersonTestData.GenerateLargeDataset(rowCount).ToList();
        var options = new TableOptions<TestPerson>
        {
            Data = data,
            Columns = PersonTestData.StandardColumns
        };
        var table = new Table<TestPerson>(options);

        // Act & Assert - Test various reset operations
        var resetOperations = new Action[]
        {
            () => table.ResetColumnFilters(),
            () => table.ResetGlobalFilter(),
            () => table.ResetSorting(),
            () => table.ResetRowSelection(),
            () => table.ResetColumnOrder(),
            () => table.ResetColumnSizing(),
            () => table.ResetColumnVisibility(),
            () => table.ResetExpanded(),
            () => table.ResetGrouping(),
            () => table.ResetPagination()
        };

        foreach (var operation in resetOperations)
        {
            var stopwatch = Stopwatch.StartNew();
            operation();
            stopwatch.Stop();

            stopwatch.ElapsedMilliseconds.Should().BeLessThan(100,
                $"Reset operation should complete within 100ms for {rowCount} rows");
        }
    }

    [Fact]
    public void Memory_Usage_Should_Be_Reasonable_For_Large_Dataset()
    {
        // Arrange
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var initialMemory = GC.GetTotalMemory(false);
        const int rowCount = 10000;
        
        // Act
        var data = PersonTestData.GenerateLargeDataset(rowCount).ToList();
        var options = new TableOptions<TestPerson>
        {
            Data = data,
            Columns = PersonTestData.StandardColumns
        };
        var table = new Table<TestPerson>(options);
        
        // Force any lazy initialization
        _ = table.RowModel.Rows.Count;
        _ = table.AllColumns.Count;
        _ = table.VisibleLeafColumns.Count;
        
        var finalMemory = GC.GetTotalMemory(false);
        var memoryIncrease = finalMemory - initialMemory;

        // Assert
        // Memory increase should be reasonable (less than 50MB for 10k rows)
        var maxMemoryIncrease = 50 * 1024 * 1024; // 50MB
        memoryIncrease.Should().BeLessThan(maxMemoryIncrease,
            $"Memory usage should be reasonable. Used: {memoryIncrease / (1024 * 1024)}MB");
        
        // Verify table is still functional
        VerifyBasicTableProperties(table);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1000)]
    public void Repeated_Operations_Should_Not_Degrade_Performance(int rowCount)
    {
        // Arrange
        var data = PersonTestData.GenerateLargeDataset(rowCount).ToList();
        var options = new TableOptions<TestPerson>
        {
            Data = data,
            Columns = PersonTestData.StandardColumns
        };
        var table = new Table<TestPerson>(options);
        
        const int iterations = 100;
        var timings = new List<long>();

        // Act - Perform the same operation multiple times
        for (int i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Perform some typical operations
            _ = table.AllColumns.Count;
            _ = table.RowModel.Rows.Count;
            table.ResetSorting();
            
            stopwatch.Stop();
            timings.Add(stopwatch.ElapsedMilliseconds);
        }

        // Assert
        var averageTime = timings.Average();
        var maxTime = timings.Max();
        
        // Performance should be consistent and fast
        averageTime.Should().BeLessThan(10, "Average operation time should be less than 10ms");
        maxTime.Should().BeLessThan(50, "Maximum operation time should be less than 50ms");
        
        // Performance should not degrade significantly over time
        var firstHalf = timings.Take(iterations / 2).Average();
        var secondHalf = timings.Skip(iterations / 2).Average();
        
        // Second half should not be more than 50% slower than first half
        secondHalf.Should().BeLessThan(firstHalf * 1.5,
            "Performance should not degrade significantly over repeated operations");
    }

    [Fact]
    public void Large_Column_Count_Should_Not_Impact_Performance_Severely()
    {
        // Arrange - Create a table with many columns
        var data = PersonTestData.GenerateLargeDataset(1000).ToList();
        var manyColumns = new List<ColumnDef<TestPerson>>();
        
        // Add standard columns
        manyColumns.AddRange(PersonTestData.StandardColumns);
        
        // Add many computed columns
        for (int i = 0; i < 50; i++)
        {
            manyColumns.Add(ColumnHelper.Accessor<TestPerson, string>(
                p => $"Computed_{i}_{p.FirstName}", 
                $"computed_{i}", 
                $"Computed {i}"));
        }

        var options = new TableOptions<TestPerson>
        {
            Data = data,
            Columns = manyColumns
        };

        // Act
        var stopwatch = Stopwatch.StartNew();
        var table = new Table<TestPerson>(options);
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000,
            "Table creation with many columns should complete within 1 second");
        
        table.AllColumns.Should().HaveCount(manyColumns.Count);
        VerifyBasicTableProperties(table);
    }

    [Fact]
    public void Concurrent_Access_Should_Not_Cause_Performance_Issues()
    {
        // Arrange
        var data = PersonTestData.GenerateLargeDataset(5000).ToList();
        var options = new TableOptions<TestPerson>
        {
            Data = data,
            Columns = PersonTestData.StandardColumns
        };
        var table = new Table<TestPerson>(options);

        // Act - Simulate concurrent read access
        var tasks = new List<Task>();
        var timings = new ConcurrentBag<long>();

        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                var stopwatch = Stopwatch.StartNew();
                
                // Perform read operations
                for (int j = 0; j < 100; j++)
                {
                    _ = table.AllColumns.Count;
                    _ = table.RowModel.Rows.Count;
                    _ = table.GetColumn("firstName");
                }
                
                stopwatch.Stop();
                timings.Add(stopwatch.ElapsedMilliseconds);
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        var averageTime = timings.Average();
        averageTime.Should().BeLessThan(1000,
            "Concurrent access should not cause severe performance degradation");
        
        // All tasks should complete in reasonable time
        timings.Should().AllSatisfy(timing => 
            timing.Should().BeLessThan(2000, "Each concurrent task should complete within 2 seconds"));
    }
}