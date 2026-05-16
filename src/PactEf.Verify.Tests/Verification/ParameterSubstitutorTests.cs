using PactEf.Verify.Verification;

namespace PactEf.Verify.Tests.Verification;

public class ParameterSubstitutorTests
{
    [Theory]
    [InlineData("Int32", "0")]
    [InlineData("Int64", "0")]
    [InlineData("Int16", "0")]
    [InlineData("String", "''")]
    [InlineData("AnsiString", "''")]
    [InlineData("Boolean", "false")]
    [InlineData("Guid", "'00000000-0000-0000-0000-000000000000'")]
    [InlineData("DateTime", "'2000-01-01'")]
    [InlineData("Date", "'2000-01-01'")]
    [InlineData("Decimal", "0.0")]
    [InlineData("Currency", "0.0")]
    [InlineData("Object", "null")]
    public void GetLiteral_ReturnsExpectedLiteral(string dbType, string expected)
    {
        Assert.Equal(expected, ParameterSubstitutor.GetLiteral(dbType));
    }

    [Fact]
    public void Substitute_ReplacesParameterPlaceholders()
    {
        var sql = "SELECT * FROM \"Orders\" WHERE \"Id\" = $1 AND \"Status\" = $2";
        var types = new[] { "Int32", "String" };
        var result = ParameterSubstitutor.Substitute(sql, types);
        Assert.Equal("SELECT * FROM \"Orders\" WHERE \"Id\" = 0 AND \"Status\" = ''", result);
    }

    [Fact]
    public void Substitute_NoParameters_ReturnsSqlUnchanged()
    {
        var sql = "SELECT * FROM \"Orders\"";
        var result = ParameterSubstitutor.Substitute(sql, []);
        Assert.Equal(sql, result);
    }
}
