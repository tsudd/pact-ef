using PactEf.Core.Models;
using PactEf.Core.Serialization;

namespace PactEf.Core.Tests.Serialization;

public class SnapshotSerializerTests
{
    [Fact]
    public void Serialize_ThenDeserialize_RoundTrips()
    {
        // Arrange
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

        // Act
        var json = SnapshotSerializer.Serialize(snapshot);
        var result = SnapshotSerializer.Deserialize(json);

        // Assert
        Assert.Equal(snapshot.ConsumerName, result.ConsumerName);
        Assert.Equal(snapshot.DbSchemaVersion, result.DbSchemaVersion);
        Assert.Equal(2, result.Queries.Count);
        Assert.Contains(result.Queries, q => q.Sql == "SELECT \"Id\" FROM \"Orders\"" && q.TestName == null);
        Assert.Contains(result.Queries, q => q.TestName == "Test_GetById");
    }

    [Fact]
    public void Serialize_OrdersQueriesBySqlText()
    {
        // Arrange
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

        // Act
        var json = SnapshotSerializer.Serialize(snapshot);
        var result = SnapshotSerializer.Deserialize(json);

        // Assert
        Assert.Equal("SELECT \"A\"", result.Queries[0].Sql);
        Assert.Equal("SELECT \"Z\"", result.Queries[1].Sql);
    }

    [Fact]
    public void Serialize_WritesSchemaVersion2AndParametersArray()
    {
        // Arrange
        var snapshot = new SnapshotFile
        {
            ConsumerName = "TestConsumer",
            CapturedAt = DateTimeOffset.UtcNow,
            Queries =
            [
                new QueryEntry
                {
                    Sql = "SELECT \"Id\" FROM \"Orders\" WHERE \"Id\" = @__id_0",
                    ParameterTypes = ["Int32"],
                    Parameters = [new ParameterMetadata { Name = "@__id_0", ClrType = "Int32", MaxLength = 255 }]
                }
            ]
        };

        // Act
        var json = SnapshotSerializer.Serialize(snapshot);

        // Assert
        Assert.Contains("\"schemaVersion\": \"2.0\"", json);
        Assert.Contains("\"parameters\"", json);
        Assert.Contains("\"name\": \"@__id_0\"", json);
        Assert.Contains("\"clrType\": \"Int32\"", json);
        Assert.Contains("\"maxLength\": 255", json);
    }

    [Fact]
    public void Serialize_ThenDeserialize_RoundTripsParameterMetadata()
    {
        // Arrange
        var snapshot = new SnapshotFile
        {
            ConsumerName = "TestConsumer",
            CapturedAt = DateTimeOffset.UtcNow,
            Queries =
            [
                new QueryEntry
                {
                    Sql = "SELECT \"Id\" FROM \"Orders\" WHERE \"Id\" = @__id_0",
                    ParameterTypes = ["Int32"],
                    Parameters =
                    [
                        new ParameterMetadata
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
                        }
                    ]
                }
            ]
        };

        // Act
        var json = SnapshotSerializer.Serialize(snapshot);
        var result = SnapshotSerializer.Deserialize(json);

        // Assert
        var parameter = Assert.Single(result.Queries[0].Parameters);
        Assert.Equal("@__id_0", parameter.Name);
        Assert.Equal("Int32", parameter.ClrType);
        Assert.Equal("Integer", parameter.DbType);
        Assert.Equal("integer", parameter.StoreType);
        Assert.Equal(255, parameter.MaxLength);
        Assert.Equal(10, parameter.Precision);
        Assert.Equal(2, parameter.Scale);
        Assert.False(parameter.IsNullable);
        Assert.Equal(4, parameter.Size);
    }

    [Fact]
    public void Deserialize_LegacyV1Snapshot_ProjectsParameterTypesIntoParameterMetadata()
    {
        // Arrange - a v1 snapshot with no "parameters" field, only "parameterTypes"
        const string json = """
            {
              "schemaVersion": "1.0",
              "consumerName": "SampleConsumer",
              "capturedAt": "2026-05-18T16:42:30Z",
              "dbSchemaVersion": "20260514000000_InitialCreate",
              "queries": [
                {
                  "sql": "SELECT o.\"Id\" FROM \"Orders\" AS o WHERE o.\"Id\" = @__id_0 LIMIT 1",
                  "parameterTypes": ["Int32"],
                  "executionCount": 1
                }
              ]
            }
            """;

        // Act
        var result = SnapshotSerializer.Deserialize(json);

        // Assert
        var query = Assert.Single(result.Queries);
        var parameter = Assert.Single(query.Parameters);
        Assert.Equal("Int32", parameter.ClrType);
        Assert.Null(parameter.Name);
        Assert.Null(parameter.DbType);
        Assert.Null(parameter.StoreType);
        Assert.Null(parameter.MaxLength);
        Assert.Null(parameter.Precision);
        Assert.Null(parameter.Scale);
        Assert.Null(parameter.IsNullable);
        Assert.Null(parameter.Size);
    }

    [Fact]
    public void Deserialize_LegacySnapshotWithNoParameters_LeavesEmptyQueryUnaffected()
    {
        // Arrange
        const string json = """
            {
              "schemaVersion": "1.0",
              "consumerName": "SampleConsumer",
              "capturedAt": "2026-05-18T16:42:30Z",
              "queries": [
                {
                  "sql": "SELECT \"Id\" FROM \"Orders\"",
                  "parameterTypes": [],
                  "executionCount": 1
                }
              ]
            }
            """;

        // Act
        var result = SnapshotSerializer.Deserialize(json);

        // Assert
        Assert.Empty(result.Queries[0].Parameters);
    }
}
