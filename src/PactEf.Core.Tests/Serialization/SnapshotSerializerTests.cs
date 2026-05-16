using PactEf.Core.Models;
using PactEf.Core.Serialization;

namespace PactEf.Core.Tests.Serialization;

public class SnapshotSerializerTests
{
    [Fact]
    public void Serialize_ThenDeserialize_RoundTrips()
    {
        var snapshot = new SnapshotFile
        {
            ConsumerName = "TestConsumer",
            CapturedAt = new DateTimeOffset(2026, 5, 14, 10, 0, 0, TimeSpan.Zero),
            DbSchemaVersion = "20260512183045",
            Queries =
            [
                new QueryEntry
                {
                    Sql = "SELECT \"Id\" FROM \"Orders\"",
                    ParameterTypes = [],
                    ExecutionCount = 2
                },
                new QueryEntry
                {
                    Sql = "SELECT \"Id\" FROM \"Orders\" WHERE \"Id\" = $1",
                    ParameterTypes = ["integer"],
                    ExecutionCount = 1,
                    TestName = "Test_GetById",
                    TestClass = "MyTests"
                }
            ]
        };

        var json = SnapshotSerializer.Serialize(snapshot);
        var result = SnapshotSerializer.Deserialize(json);

        Assert.Equal(snapshot.ConsumerName, result.ConsumerName);
        Assert.Equal(snapshot.DbSchemaVersion, result.DbSchemaVersion);
        Assert.Equal(2, result.Queries.Count);
        Assert.Contains(result.Queries, q => q.Sql == "SELECT \"Id\" FROM \"Orders\"" && q.TestName == null);
        Assert.Contains(result.Queries, q => q.TestName == "Test_GetById");
    }

    [Fact]
    public void Serialize_OrdersQueriesBySqlText()
    {
        var snapshot = new SnapshotFile
        {
            ConsumerName = "TestConsumer",
            CapturedAt = DateTimeOffset.UtcNow,
            Queries =
            [
                new QueryEntry { Sql = "SELECT \"Z\"", ParameterTypes = [] },
                new QueryEntry { Sql = "SELECT \"A\"", ParameterTypes = [] }
            ]
        };

        var json = SnapshotSerializer.Serialize(snapshot);
        var result = SnapshotSerializer.Deserialize(json);

        Assert.Equal("SELECT \"A\"", result.Queries[0].Sql);
        Assert.Equal("SELECT \"Z\"", result.Queries[1].Sql);
    }
}
