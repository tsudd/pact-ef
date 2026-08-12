using PactEf.Verify.Verification;

namespace PactEf.Verify.Tests.Verification;

public class SqlColumnReferenceResolverTests
{
    [Fact]
    public void Resolve_InsertStatement_MapsParamNameToTableAndColumn()
    {
        const string sql = "INSERT INTO \"Orders\" (\"Status\") VALUES (@p0)";

        var resolved = SqlColumnReferenceResolver.Resolve(sql);

        var (table, column) = resolved["@p0"];
        Assert.Equal("Orders", table);
        Assert.Equal("Status", column);
    }

    [Fact]
    public void Resolve_EqualityWhereClause_MapsParamNameUsingNearestTableReference()
    {
        const string sql = "SELECT o.\"Id\" FROM \"Orders\" AS o WHERE o.\"Status\" = @__status_0";

        var resolved = SqlColumnReferenceResolver.Resolve(sql);

        var (table, column) = resolved["@__status_0"];
        Assert.Equal("Orders", table);
        Assert.Equal("Status", column);
    }

    [Fact]
    public void Resolve_MultipleInsertColumns_MapsEachParamPositionally()
    {
        const string sql = "INSERT INTO \"Orders\" (\"Status\", \"Note\") VALUES (@p0, @p1)";

        var resolved = SqlColumnReferenceResolver.Resolve(sql);

        Assert.Equal(("Orders", "Status"), resolved["@p0"]);
        Assert.Equal(("Orders", "Note"), resolved["@p1"]);
    }

    [Fact]
    public void Resolve_NoRecognizedPattern_ReturnsEmpty()
    {
        const string sql = "SELECT 1";

        var resolved = SqlColumnReferenceResolver.Resolve(sql);

        Assert.Empty(resolved);
    }
}
