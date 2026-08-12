using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PactEf.Core.Models;

namespace PactEf.Capture;

/// <summary>
/// Enriches provider-captured parameter metadata with EF Core model facets
/// (MaxLength, Precision, Scale) by matching SQL column references back to
/// mapped entity properties. Matching is a best-effort text-based mapping from
/// SQL (INSERT column lists and "Column" = @param equality patterns) to table
/// names, since DbCommandInterceptor only exposes the rendered SQL text, not
/// the RelationalCommand's typed parameters.
/// </summary>
internal static class ModelParameterMetadataResolver
{
    private static readonly Regex TableReferenceRegex = new(
        "(?:FROM|JOIN|UPDATE|INTO)\\s+\"(?<table>[^\"]+)\"",
        RegexOptions.Compiled);

    private static readonly Regex InsertRegex = new(
        "INSERT\\s+INTO\\s+\"(?<table>[^\"]+)\"\\s*\\((?<cols>[^)]*)\\)\\s*VALUES\\s*\\((?<params>[^)]*)\\)",
        RegexOptions.Compiled);

    private static readonly Regex EqualityRegex = new(
        "\"(?<col>[^\"]+)\"\\s*=\\s*(?<param>@[A-Za-z0-9_]+)",
        RegexOptions.Compiled);

    public static IReadOnlyList<ParameterMetadata> Enrich(
        IReadOnlyList<ParameterMetadata> parameters, string sql, IModel? model)
    {
        if (model is null || parameters.Count == 0)
            return parameters;

        try
        {
            return EnrichCore(parameters, sql, model);
        }
        catch
        {
            // Model-based enrichment is best-effort; provider metadata always remains usable.
            return parameters;
        }
    }

    private static IReadOnlyList<ParameterMetadata> EnrichCore(
        IReadOnlyList<ParameterMetadata> parameters, string sql, IModel model)
    {
        var tableNames = TableReferenceRegex.Matches(sql)
            .Select(m => m.Groups["table"].Value)
            .ToHashSet();

        if (tableNames.Count == 0)
            return parameters;

        var candidateEntities = model.GetEntityTypes()
            .Where(e => e.GetTableName() is { } table && tableNames.Contains(table))
            .ToList();

        if (candidateEntities.Count == 0)
            return parameters;

        var resolved = new Dictionary<string, IProperty>();

        foreach (Match insert in InsertRegex.Matches(sql))
        {
            var table = insert.Groups["table"].Value;
            var entity = candidateEntities.FirstOrDefault(e => e.GetTableName() == table);
            if (entity is null)
                continue;

            var columns = SplitQuotedIdentifiers(insert.Groups["cols"].Value);
            var paramNames = SplitParamNames(insert.Groups["params"].Value);

            for (var i = 0; i < Math.Min(columns.Count, paramNames.Count); i++)
                TryResolveColumn(entity, columns[i], paramNames[i], resolved);
        }

        foreach (Match equality in EqualityRegex.Matches(sql))
        {
            var column = equality.Groups["col"].Value;
            var paramName = equality.Groups["param"].Value;

            foreach (var entity in candidateEntities)
            {
                if (TryResolveColumn(entity, column, paramName, resolved))
                    break;
            }
        }

        if (resolved.Count == 0)
            return parameters;

        return parameters
            .Select(p => p.Name is not null && resolved.TryGetValue(p.Name, out var property)
                ? WithModelFacets(p, property)
                : p)
            .ToList();
    }

    private static bool TryResolveColumn(
        IEntityType entity, string column, string paramName, Dictionary<string, IProperty> resolved)
    {
        var property = entity.GetProperties()
            .FirstOrDefault(p => p.GetColumnName() == column);

        if (property is null)
            return false;

        resolved[paramName] = property;
        return true;
    }

    private static ParameterMetadata WithModelFacets(ParameterMetadata original, IProperty property) =>
        new()
        {
            Name = original.Name,
            ClrType = original.ClrType,
            DbType = original.DbType,
            StoreType = original.StoreType,
            MaxLength = property.GetMaxLength(),
            Precision = property.GetPrecision(),
            Scale = property.GetScale(),
            IsNullable = original.IsNullable,
            Size = original.Size
        };

    private static List<string> SplitQuotedIdentifiers(string csv) =>
        csv.Split(',')
            .Select(s => s.Trim().Trim('"'))
            .Where(s => s.Length > 0)
            .ToList();

    private static List<string> SplitParamNames(string csv) =>
        csv.Split(',')
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
}
