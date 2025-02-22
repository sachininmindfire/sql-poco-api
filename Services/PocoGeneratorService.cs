using System.Text;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLPocoAPI.Models;

namespace SQLPocoAPI.Services;

public class PocoGeneratorService : IPocoGeneratorService
{
    private readonly ILogger<PocoGeneratorService> _logger;

    public PocoGeneratorService(ILogger<PocoGeneratorService> logger)
    {
        _logger = logger;
    }

    public Task<ConversionResponse> GeneratePocoAsync(ConversionRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.SqlScript))
            {
                return Task.FromResult(new ConversionResponse 
                { 
                    Success = false,
                    Error = "SQL script cannot be empty" 
                });
            }

            // Parse the SQL script
            var parser = new TSql160Parser(false);
            using var reader = new StringReader(request.SqlScript);
            var result = parser.Parse(reader, out var errors);

            if (errors != null && errors.Count > 0)
            {
                return Task.FromResult(new ConversionResponse
                {
                    Success = false,
                    Error = $"SQL parsing error: {string.Join(", ", errors.Select(e => e.Message))}"
                });
            }

            // Find CREATE TABLE statements
            var visitor = new CreateTableVisitor();
            result.Accept(visitor);

            if (visitor.TableInfos.Count == 0)
            {
                return Task.FromResult(new ConversionResponse
                {
                    Success = false,
                    Error = "No valid CREATE TABLE statements found"
                });
            }

            // Generate code for each table
            var generatedCode = new Dictionary<string, string>();
            foreach (var tableInfo in visitor.TableInfos)
            {
                string code = request.Language.ToLower() switch
                {
                    "csharp" => GenerateCSharpCode(tableInfo),
                    "java" => GenerateJavaCode(tableInfo),
                    "typescript" => GenerateTypeScriptCode(tableInfo),
                    "python" => GeneratePythonCode(tableInfo),
                    _ => throw new ArgumentException($"Unsupported language: {request.Language}")
                };
                generatedCode.Add(tableInfo.TableName, code);
            }

            return Task.FromResult(new ConversionResponse
            {
                GeneratedCode = generatedCode,
                Success = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating POCO classes");
            return Task.FromResult(new ConversionResponse
            {
                Success = false,
                Error = $"Error generating POCO classes: {ex.Message}"
            });
        }
    }

    private class CreateTableVisitor : TSqlFragmentVisitor
    {
        public List<(string TableName, List<(string Name, string Type, bool IsNullable)> Columns)> TableInfos { get; } = new();

        public override void Visit(CreateTableStatement node)
        {
            var tableName = node.SchemaObjectName.BaseIdentifier.Value;
            var columns = new List<(string Name, string Type, bool IsNullable)>();

            foreach (var columnDefinition in node.Definition.ColumnDefinitions)
            {
                string columnName = columnDefinition.ColumnIdentifier.Value;
                string dataType = GetColumnType(columnDefinition.DataType);
                
                // Check for NOT NULL constraint by looking for Constraint objects
                bool isNullable = true; // Default to nullable
                if (columnDefinition.Constraints != null)
                {
                    foreach (var constraint in columnDefinition.Constraints)
                    {
                        // Check if any constraint is a NOT NULL constraint
                        if (constraint is ConstraintDefinition constraintDef)
                        {
                            var constraintText = constraintDef.ToString().ToUpper();
                            if (constraintText.Contains("NOT NULL"))
                            {
                                isNullable = false;
                                break;
                            }
                        }
                    }
                }

                columns.Add((columnName, dataType, isNullable));
            }

            TableInfos.Add((tableName, columns));
        }

        private string GetColumnType(DataTypeReference dataType)
        {
            if (dataType is SqlDataTypeReference sqlDataType)
            {
                return sqlDataType.SqlDataTypeOption.ToString();
            }

            return dataType.Name.BaseIdentifier.Value;
        }
    }

    private string GenerateCSharpCode((string TableName, List<(string Name, string Type, bool IsNullable)> Columns) tableInfo)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine();
        sb.AppendLine("public class " + tableInfo.TableName);
        sb.AppendLine("{");

        foreach (var column in tableInfo.Columns)
        {
            string csharpType = GetCSharpType(column.Type, column.IsNullable);
            sb.AppendLine($"    public {csharpType} {column.Name} {{ get; set; }}");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private string GenerateJavaCode((string TableName, List<(string Name, string Type, bool IsNullable)> Columns) tableInfo)
    {
        var sb = new StringBuilder();
        sb.AppendLine("import java.time.LocalDateTime;");
        sb.AppendLine("import java.math.BigDecimal;");
        sb.AppendLine();
        sb.AppendLine("public class " + tableInfo.TableName + " {");

        foreach (var column in tableInfo.Columns)
        {
            string javaType = GetJavaType(column.Type, column.IsNullable);
            sb.AppendLine($"    private {javaType} {column.Name};");
        }

        sb.AppendLine();

        foreach (var column in tableInfo.Columns)
        {
            string javaType = GetJavaType(column.Type, column.IsNullable);
            string capitalizedName = char.ToUpper(column.Name[0]) + column.Name.Substring(1);
            
            sb.AppendLine($"    public {javaType} get{capitalizedName}() {{");
            sb.AppendLine($"        return {column.Name};");
            sb.AppendLine("    }");
            sb.AppendLine();
            
            sb.AppendLine($"    public void set{capitalizedName}({javaType} {column.Name}) {{");
            sb.AppendLine($"        this.{column.Name} = {column.Name};");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private string GenerateTypeScriptCode((string TableName, List<(string Name, string Type, bool IsNullable)> Columns) tableInfo)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"interface {tableInfo.TableName} {{");

        foreach (var column in tableInfo.Columns)
        {
            string tsType = GetTypeScriptType(column.Type);
            sb.AppendLine($"    {column.Name}{(column.IsNullable ? "?" : "")}: {tsType};");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private string GeneratePythonCode((string TableName, List<(string Name, string Type, bool IsNullable)> Columns) tableInfo)
    {
        var sb = new StringBuilder();
        sb.AppendLine("from dataclasses import dataclass");
        sb.AppendLine("from datetime import datetime");
        sb.AppendLine("from typing import Optional");
        sb.AppendLine();
        sb.AppendLine("@dataclass");
        sb.AppendLine($"class {tableInfo.TableName}:");

        foreach (var column in tableInfo.Columns)
        {
            string pythonType = GetPythonType(column.Type);
            if (column.IsNullable)
            {
                pythonType = $"Optional[{pythonType}]";
            }
            sb.AppendLine($"    {column.Name}: {pythonType}");
        }

        return sb.ToString();
    }

    private string GetCSharpType(string sqlType, bool isNullable)
    {
        var type = sqlType.ToUpper() switch
        {
            "BIGINT" => "long",
            "BINARY" => "byte[]",
            "BIT" => "bool",
            "CHAR" => "string",
            "DATE" => "DateTime",
            "DATETIME" => "DateTime",
            "DATETIME2" => "DateTime",
            "DATETIMEOFFSET" => "DateTimeOffset",
            "DECIMAL" => "decimal",
            "FLOAT" => "double",
            "IMAGE" => "byte[]",
            "INT" => "int",
            "MONEY" => "decimal",
            "NCHAR" => "string",
            "NTEXT" => "string",
            "NUMERIC" => "decimal",
            "NVARCHAR" => "string",
            "REAL" => "float",
            "SMALLDATETIME" => "DateTime",
            "SMALLINT" => "short",
            "SMALLMONEY" => "decimal",
            "TEXT" => "string",
            "TIME" => "TimeSpan",
            "TINYINT" => "byte",
            "UNIQUEIDENTIFIER" => "Guid",
            "VARBINARY" => "byte[]",
            "VARCHAR" => "string",
            _ => "object"
        };

        return type == "string" || type == "byte[]" ? type : isNullable ? type + "?" : type;
    }

    private string GetJavaType(string sqlType, bool isNullable)
    {
        var type = sqlType.ToUpper() switch
        {
            "BIGINT" => isNullable ? "Long" : "long",
            "BINARY" => "byte[]",
            "BIT" => isNullable ? "Boolean" : "boolean",
            "CHAR" => "String",
            "DATE" => "LocalDateTime",
            "DATETIME" => "LocalDateTime",
            "DATETIME2" => "LocalDateTime",
            "DATETIMEOFFSET" => "OffsetDateTime",
            "DECIMAL" => "BigDecimal",
            "FLOAT" => isNullable ? "Double" : "double",
            "IMAGE" => "byte[]",
            "INT" => isNullable ? "Integer" : "int",
            "MONEY" => "BigDecimal",
            "NCHAR" => "String",
            "NTEXT" => "String",
            "NUMERIC" => "BigDecimal",
            "NVARCHAR" => "String",
            "REAL" => isNullable ? "Float" : "float",
            "SMALLDATETIME" => "LocalDateTime",
            "SMALLINT" => isNullable ? "Short" : "short",
            "SMALLMONEY" => "BigDecimal",
            "TEXT" => "String",
            "TIME" => "LocalTime",
            "TINYINT" => isNullable ? "Byte" : "byte",
            "UNIQUEIDENTIFIER" => "UUID",
            "VARBINARY" => "byte[]",
            "VARCHAR" => "String",
            _ => "Object"
        };

        return type;
    }

    private string GetTypeScriptType(string sqlType)
    {
        return sqlType.ToUpper() switch
        {
            "BIGINT" => "number",
            "BINARY" => "Uint8Array",
            "BIT" => "boolean",
            "CHAR" => "string",
            "DATE" => "Date",
            "DATETIME" => "Date",
            "DATETIME2" => "Date",
            "DATETIMEOFFSET" => "Date",
            "DECIMAL" => "number",
            "FLOAT" => "number",
            "IMAGE" => "Uint8Array",
            "INT" => "number",
            "MONEY" => "number",
            "NCHAR" => "string",
            "NTEXT" => "string",
            "NUMERIC" => "number",
            "NVARCHAR" => "string",
            "REAL" => "number",
            "SMALLDATETIME" => "Date",
            "SMALLINT" => "number",
            "SMALLMONEY" => "number",
            "TEXT" => "string",
            "TIME" => "string",
            "TINYINT" => "number",
            "UNIQUEIDENTIFIER" => "string",
            "VARBINARY" => "Uint8Array",
            "VARCHAR" => "string",
            _ => "any"
        };
    }

    private string GetPythonType(string sqlType)
    {
        return sqlType.ToUpper() switch
        {
            "BIGINT" => "int",
            "BINARY" => "bytes",
            "BIT" => "bool",
            "CHAR" => "str",
            "DATE" => "datetime",
            "DATETIME" => "datetime",
            "DATETIME2" => "datetime",
            "DATETIMEOFFSET" => "datetime",
            "DECIMAL" => "Decimal",
            "FLOAT" => "float",
            "IMAGE" => "bytes",
            "INT" => "int",
            "MONEY" => "Decimal",
            "NCHAR" => "str",
            "NTEXT" => "str",
            "NUMERIC" => "Decimal",
            "NVARCHAR" => "str",
            "REAL" => "float",
            "SMALLDATETIME" => "datetime",
            "SMALLINT" => "int",
            "SMALLMONEY" => "Decimal",
            "TEXT" => "str",
            "TIME" => "str",
            "TINYINT" => "int",
            "UNIQUEIDENTIFIER" => "str",
            "VARBINARY" => "bytes",
            "VARCHAR" => "str",
            _ => "Any"
        };
    }
}