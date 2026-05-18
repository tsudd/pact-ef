using PactEf.Capture;
using PactEf.Core.Models;

namespace PactEf.Capture.Tests;

public class QueryBufferTests
{
    [Fact]
    public void Add_SingleEntry_StoredInBuffer()
    {
        // Arrange
        var buffer = new QueryBuffer();
        buffer.Add(new QueryEntry { Sql = "SELECT 1", ParameterTypes = [] });

        // Act
        var entries = buffer.GetAll();

        // Assert
        Assert.Single(entries);
        Assert.Equal("SELECT 1", entries[0].Sql);
    }

    [Fact]
    public void Add_SameSqlTwice_DeduplicatesAndIncrementsCount()
    {
        // Arrange
        var buffer = new QueryBuffer();
        buffer.Add(new QueryEntry { Sql = "SELECT 1", ParameterTypes = [] });
        buffer.Add(new QueryEntry { Sql = "SELECT 1", ParameterTypes = [] });

        // Act
        var entries = buffer.GetAll();

        // Assert
        Assert.Single(entries);
        Assert.Equal(2, entries[0].ExecutionCount);
    }

    [Fact]
    public void Add_DifferentSql_StoresBothEntries()
    {
        // Arrange
        var buffer = new QueryBuffer();
        buffer.Add(new QueryEntry { Sql = "SELECT 1", ParameterTypes = [] });
        buffer.Add(new QueryEntry { Sql = "SELECT 2", ParameterTypes = [] });

        // Act & Assert
        Assert.Equal(2, buffer.GetAll().Count);
    }

    [Fact]
    public void Add_SameSqlWithDifferentTestNames_DeduplicatesKeepingFirstTestName()
    {
        // Arrange
        var buffer = new QueryBuffer();
        buffer.Add(new QueryEntry { Sql = "SELECT 1", ParameterTypes = [], TestName = "Test_A", TestClass = "Class_A" });
        buffer.Add(new QueryEntry { Sql = "SELECT 1", ParameterTypes = [], TestName = "Test_B", TestClass = "Class_B" });

        // Act
        var entries = buffer.GetAll();

        // Assert
        Assert.Single(entries);
        Assert.Equal(2, entries[0].ExecutionCount);
        Assert.Equal("Test_A", entries[0].TestName);
    }
}
