using System.Data;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using NpgsqlTypes;
using PactEf.Capture;

namespace PactEf.Capture.Tests;

[Collection("EnvVarTests")]
public class PactEfCaptureInterceptorTests : IDisposable
{
    public PactEfCaptureInterceptorTests()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        Environment.SetEnvironmentVariable("PACTEF_CAPTURE_DISABLED", null);
    }

    [Fact]
    public void Create_WhenEnvironmentIsTesting_ReturnsRealInterceptor()
    {
        var interceptor = PactEfCaptureInterceptor.Create(o => o.ConsumerName = "Test");
        Assert.IsType<PactEfCaptureInterceptor>(interceptor);
    }

    [Fact]
    public void Create_WhenEnvironmentNotTesting_ReturnsNullInterceptor()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        var interceptor = PactEfCaptureInterceptor.Create(o => o.ConsumerName = "Test");
        Assert.IsType<NullCaptureInterceptor>(interceptor);
    }

    [Fact]
    public void Create_WhenDisabled_ReturnsNullInterceptor()
    {
        Environment.SetEnvironmentVariable("PACTEF_CAPTURE_DISABLED", "true");
        var interceptor = PactEfCaptureInterceptor.Create(o => o.ConsumerName = "Test");
        Assert.IsType<NullCaptureInterceptor>(interceptor);
    }

    [Fact]
    public void ToParameterMetadata_NpgsqlParameter_MapsNameDbTypeStoreTypeSizeAndIsNullable()
    {
        using var command = new NpgsqlCommand();
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@p0";
        parameter.NpgsqlDbType = NpgsqlDbType.Varchar;
        parameter.Value = "hello";
        parameter.Size = 50;
        parameter.IsNullable = true;

        var metadata = PactEfCaptureInterceptor.ToParameterMetadata(parameter);

        Assert.Equal("@p0", metadata.Name);
        Assert.Equal("String", metadata.ClrType);
        Assert.Equal(DbType.String.ToString(), metadata.DbType);
        Assert.Equal(NpgsqlDbType.Varchar.ToString(), metadata.StoreType);
        Assert.Equal(50, metadata.Size);
        Assert.True(metadata.IsNullable);
        Assert.Null(metadata.MaxLength);
    }

    [Fact]
    public void ToParameterMetadata_ZeroSize_MapsSizeAsNullNotMaxLength()
    {
        using var command = new NpgsqlCommand();
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@p0";
        parameter.NpgsqlDbType = NpgsqlDbType.Integer;
        parameter.Value = 42;

        var metadata = PactEfCaptureInterceptor.ToParameterMetadata(parameter);

        Assert.Null(metadata.Size);
        Assert.Null(metadata.MaxLength);
    }

    [Theory]
    [InlineData("ALTER TABLE \"OrderItems\" ALTER COLUMN \"Description\" SET NOT NULL;")]
    [InlineData(
        "UPDATE \"OrderItems\" SET \"Description\" = '' WHERE \"Description\" IS NULL;\n" +
        "ALTER TABLE \"OrderItems\" ALTER COLUMN \"Description\" SET NOT NULL;\n" +
        "ALTER TABLE \"OrderItems\" ALTER COLUMN \"Description\" SET DEFAULT '';")]
    [InlineData("CREATE TABLE \"Foo\" (\"Id\" integer NOT NULL);")]
    [InlineData("DROP TABLE \"Foo\";")]
    [InlineData("CREATE INDEX \"IX_Foo\" ON \"Foo\" (\"Id\");")]
    public void IsDdl_MigrationStatements_ReturnsTrue(string sql)
    {
        Assert.True(PactEfCaptureInterceptor.IsDdl(sql));
    }

    [Theory]
    [InlineData("SELECT * FROM \"OrderItems\" WHERE \"Id\" = @p0")]
    [InlineData("INSERT INTO \"OrderItems\" (\"Description\") VALUES (@p0)")]
    [InlineData("UPDATE \"OrderItems\" SET \"Description\" = @p0 WHERE \"Id\" = @p1")]
    [InlineData("DELETE FROM \"OrderItems\" WHERE \"Id\" = @p0")]
    public void IsDdl_ConsumerStatements_ReturnsFalse(string sql)
    {
        Assert.False(PactEfCaptureInterceptor.IsDdl(sql));
    }
}
