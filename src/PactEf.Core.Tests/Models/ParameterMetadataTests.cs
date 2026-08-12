using PactEf.Core.Models;

namespace PactEf.Core.Tests.Models;

public class ParameterMetadataTests
{
    [Fact]
    public void Construction_AllFieldsUnset_DefaultToNull()
    {
        var metadata = new ParameterMetadata();

        Assert.Null(metadata.Name);
        Assert.Null(metadata.ClrType);
        Assert.Null(metadata.DbType);
        Assert.Null(metadata.StoreType);
        Assert.Null(metadata.MaxLength);
        Assert.Null(metadata.Precision);
        Assert.Null(metadata.Scale);
        Assert.Null(metadata.IsNullable);
        Assert.Null(metadata.Size);
    }

    [Fact]
    public void Construction_AllFieldsSet_RoundTripsValues()
    {
        var metadata = new ParameterMetadata
        {
            Name = "@__id_0",
            ClrType = "Int32",
            DbType = "Integer",
            StoreType = "integer",
            MaxLength = 255,
            Precision = 10,
            Scale = 2,
            IsNullable = false,
            Size = 4
        };

        Assert.Equal("@__id_0", metadata.Name);
        Assert.Equal("Int32", metadata.ClrType);
        Assert.Equal("Integer", metadata.DbType);
        Assert.Equal("integer", metadata.StoreType);
        Assert.Equal(255, metadata.MaxLength);
        Assert.Equal(10, metadata.Precision);
        Assert.Equal(2, metadata.Scale);
        Assert.False(metadata.IsNullable);
        Assert.Equal(4, metadata.Size);
    }

    [Fact]
    public void QueryEntry_Parameters_DefaultsToEmpty()
    {
        var entry = new QueryEntry
        {
            Sql = "SELECT 1",
            ParameterTypes = []
        };

        Assert.Empty(entry.Parameters);
    }

    [Fact]
    public void QueryEntry_Parameters_CanBeSet()
    {
        var entry = new QueryEntry
        {
            Sql = "SELECT \"Id\" FROM \"Orders\" WHERE \"Id\" = @__id_0",
            ParameterTypes = ["Int32"],
            Parameters = [new ParameterMetadata { Name = "@__id_0", ClrType = "Int32" }]
        };

        Assert.Single(entry.Parameters);
        Assert.Equal("@__id_0", entry.Parameters[0].Name);
    }
}
