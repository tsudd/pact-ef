using PactEf.Capture;
using PactEf.Core.Models;

namespace PactEf.Capture.Tests;

public class QueryBufferTests
{
    [Fact]
    public void Add_SingleEntry_StoredInBuffer()
    {
        var buffer = new QueryBuffer();
        buffer.Add(new QueryEntry { Sql = "SELECT 1", ParameterTypes = [] });

        var entries = buffer.GetAll();
        Assert.Single(entries);
        Assert.Equal("SELECT 1", entries[0].Sql);
    }

    [Fact]
    public void Add_SameSqlTwice_DeduplicatesAndIncrementsCount()
    {
        var buffer = new QueryBuffer();
        buffer.Add(new QueryEntry { Sql = "SELECT 1", ParameterTypes = [] });
        buffer.Add(new QueryEntry { Sql = "SELECT 1", ParameterTypes = [] });

        var entries = buffer.GetAll();
        Assert.Single(entries);
        Assert.Equal(2, entries[0].ExecutionCount);
    }

    [Fact]
    public void Add_DifferentSql_StoresBothEntries()
    {
        var buffer = new QueryBuffer();
        buffer.Add(new QueryEntry { Sql = "SELECT 1", ParameterTypes = [] });
        buffer.Add(new QueryEntry { Sql = "SELECT 2", ParameterTypes = [] });

        Assert.Equal(2, buffer.GetAll().Count);
    }

    [Fact]
    public void Add_SameSqlWithDifferentTestNames_DeduplicatesKeepingFirstTestName()
    {
        var buffer = new QueryBuffer();
        buffer.Add(new QueryEntry { Sql = "SELECT 1", ParameterTypes = [], TestName = "Test_A", TestClass = "Class_A" });
        buffer.Add(new QueryEntry { Sql = "SELECT 1", ParameterTypes = [], TestName = "Test_B", TestClass = "Class_B" });

        var entries = buffer.GetAll();
        Assert.Single(entries);
        Assert.Equal(2, entries[0].ExecutionCount);
        Assert.Equal("Test_A", entries[0].TestName);
    }
}
