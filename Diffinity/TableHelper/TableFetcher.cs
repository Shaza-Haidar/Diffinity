using Dapper;
using Diffinity.DbHelper;
using Microsoft.Data.SqlClient;
using System.Text;

namespace Diffinity.TableHelper;
public class TableFetcher
{
    private const string GetTablesNamesQuery = @"
            SELECT s.name AS SchemaName, t.name AS TableName
            FROM sys.tables t
            JOIN sys.schemas s ON t.schema_id = s.schema_id
            ORDER BY s.name, t.name;
        ";
    private const string GetTableInfoQuery = @"
SELECT 
    c.name AS columnName,
    ty.name AS columnType,
    CASE WHEN c.is_nullable = 1 THEN 'YES' ELSE 'NO' END AS isNullable,
    CASE 
        WHEN ty.name IN ('nvarchar', 'nchar') THEN c.max_length / 2
        WHEN ty.name IN ('varchar', 'char') THEN c.max_length
        ELSE NULL
    END AS maxLength,
    CASE 
        WHEN pk.column_id IS NOT NULL THEN 'YES' ELSE 'NO' 
    END AS isPrimaryKey,
    CASE 
        WHEN fk.parent_column_id IS NOT NULL THEN 'YES' ELSE 'NO' 
    END AS isForeignKey,
    CASE WHEN c.is_identity = 1 THEN 'YES' ELSE 'NO' END AS isIdentity,
    CONVERT(nvarchar(100), ic.seed_value) AS identitySeed,
    CONVERT(nvarchar(100), ic.increment_value) AS identityIncrement
    FROM sys.columns c
    JOIN sys.tables t ON c.object_id = t.object_id
    JOIN sys.schemas s ON t.schema_id = s.schema_id
    JOIN sys.types ty ON c.user_type_id = ty.user_type_id
    LEFT JOIN sys.identity_columns ic
        ON c.object_id = ic.object_id AND c.column_id = ic.column_id
    LEFT JOIN (
        SELECT i.object_id, ic.column_id
        FROM sys.indexes i
        JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
        WHERE i.is_primary_key = 1
    ) pk ON c.object_id = pk.object_id AND c.column_id = pk.column_id
    LEFT JOIN (
        SELECT DISTINCT fkc.parent_object_id AS object_id, fkc.parent_column_id AS parent_column_id
        FROM sys.foreign_key_columns fkc
    ) fk ON c.object_id = fk.object_id AND c.column_id = fk.parent_column_id
    WHERE t.name = @tableName
      AND s.name = @schemaName
    ORDER BY c.column_id;
";
    private const string GetForeignKeysQuery = @"
    SELECT 
        fk.name AS ConstraintName,
        SCHEMA_NAME(ref_t.schema_id) AS ReferencedSchema,
        ref_t.name AS ReferencedTable,
        ref_c.name AS ReferencedColumn,
        parent_c.name AS ColumnName
    FROM sys.foreign_keys fk
    INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
    INNER JOIN sys.tables t ON fk.parent_object_id = t.object_id
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    INNER JOIN sys.columns parent_c ON fkc.parent_object_id = parent_c.object_id 
        AND fkc.parent_column_id = parent_c.column_id
    INNER JOIN sys.tables ref_t ON fkc.referenced_object_id = ref_t.object_id
    INNER JOIN sys.columns ref_c ON fkc.referenced_object_id = ref_c.object_id 
        AND fkc.referenced_column_id = ref_c.column_id
    WHERE t.name = @tableName
      AND s.name = @schemaName
    ORDER BY fk.name, fkc.constraint_column_id;
";
    private const string GetIndexesQuery = @"
    SELECT
        i.name AS IndexName,
        i.type_desc AS IndexType,
        i.is_unique AS IsUnique,
        i.is_unique_constraint AS IsUniqueConstraint,
        i.filter_definition AS FilterDefinition,
        keyCols.KeyColumns,
        includeCols.IncludedColumns
    FROM sys.indexes i
    INNER JOIN sys.tables t ON i.object_id = t.object_id
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    OUTER APPLY (
        SELECT STRING_AGG(
            QUOTENAME(c.name) + CASE WHEN ic.is_descending_key = 1 THEN ' DESC' ELSE ' ASC' END,
            ', '
        ) WITHIN GROUP (ORDER BY ic.key_ordinal) AS KeyColumns
        FROM sys.index_columns ic
        INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
        WHERE ic.object_id = i.object_id
          AND ic.index_id = i.index_id
          AND ic.key_ordinal > 0
    ) keyCols
    OUTER APPLY (
        SELECT STRING_AGG(QUOTENAME(c.name), ', ') WITHIN GROUP (ORDER BY ic.index_column_id) AS IncludedColumns
        FROM sys.index_columns ic
        INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
        WHERE ic.object_id = i.object_id
          AND ic.index_id = i.index_id
          AND ic.is_included_column = 1
    ) includeCols
    WHERE t.name = @tableName
      AND s.name = @schemaName
      AND i.index_id > 0
      AND i.is_hypothetical = 0
      AND i.is_primary_key = 0
    ORDER BY i.name;
";
    /// <summary>
    /// Retrieves the names of all tables from the source database.
    /// </summary>
    /// <param name="sourceConnectionString"></param>
    /// <returns></returns>
    public static List<(string schema, string name)> GetTablesNames(string sourceConnectionString)
    {
        using var sourceConnection = new SqlConnection(sourceConnectionString);
        var list = sourceConnection.Query<(string schema, string name)>(GetTablesNamesQuery).AsList();
        return list;
    }

    /// <summary>
    /// Returns the info of a table from both source and destination databases.
    /// </summary>
    /// <param name="fullTableName"></param>
    /// <returns></returns>
    public static (List<tableDto> sourceTableColumns, List<tableDto> destinationTableColumns,
                   List<ForeignKeyDto> sourceForeignKeys, List<ForeignKeyDto> destinationForeignKeys,
                   List<IndexDto> sourceIndexes, List<IndexDto> destinationIndexes)
        GetTableInfo(string sourceConnectionString, string destinationConnectionString, string schema, string TableName)
    {
        using SqlConnection sourceConnection = new SqlConnection(sourceConnectionString);
        using SqlConnection destinationConnection = new SqlConnection(destinationConnectionString);

        var sourceInfo = sourceConnection.Query<tableDto>(GetTableInfoQuery, new { tableName = TableName, schemaName = schema }).ToList();
        var destinationInfo = destinationConnection.Query<tableDto>(GetTableInfoQuery, new { tableName = TableName, schemaName = schema }).ToList();

        var sourceFKs = sourceConnection.Query<ForeignKeyDto>(GetForeignKeysQuery, new { tableName = TableName, schemaName = schema }).ToList();
        var destinationFKs = destinationConnection.Query<ForeignKeyDto>(GetForeignKeysQuery, new { tableName = TableName, schemaName = schema }).ToList();
        var sourceIndexes = sourceConnection.Query<IndexDto>(GetIndexesQuery, new { tableName = TableName, schemaName = schema }).ToList();
        var destinationIndexes = destinationConnection.Query<IndexDto>(GetIndexesQuery, new { tableName = TableName, schemaName = schema }).ToList();

        return (sourceInfo, destinationInfo, sourceFKs, destinationFKs, sourceIndexes, destinationIndexes);
    }
}
public class tableDto
{
    public string columnName { get; set; }
    public string columnType { get; set; }
    public string isNullable { get; set; }
    public string maxLength { get; set; }
    public string isPrimaryKey { get; set; }
    public string isForeignKey { get; set; }
    public string isIdentity { get; set; }
    public string identitySeed { get; set; }
    public string identityIncrement { get; set; }
}

public class ForeignKeyDto
{
    public string ConstraintName { get; set; }
    public string ReferencedSchema { get; set; }
    public string ReferencedTable { get; set; }
    public string ReferencedColumn { get; set; }
    public string ColumnName { get; set; }
}

public class IndexDto
{
    public string IndexName { get; set; }
    public string IndexType { get; set; }
    public bool IsUnique { get; set; }
    public bool IsUniqueConstraint { get; set; }
    public string? FilterDefinition { get; set; }
    public string? KeyColumns { get; set; }
    public string? IncludedColumns { get; set; }
}
