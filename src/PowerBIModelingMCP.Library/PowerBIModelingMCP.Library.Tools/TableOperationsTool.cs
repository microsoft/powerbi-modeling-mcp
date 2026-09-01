using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;
using PowerBIModelingMCP.Library.Common.Telemetry;
using PowerBIModelingMCP.Library.Contracts;
using PowerBIModelingMCP.Library.Core;

namespace PowerBIModelingMCP.Library.Tools;

[McpServerToolType]
public class TableOperationsTool
{
	public const string ToolName = "table_operations";

	private readonly ILogger<TableOperationsTool> _logger;

	private readonly IWriteGuard _writeGuard;

	private readonly IEnhancedRefreshService? _enhancedRefreshService;

	public static readonly ToolMetadata toolMetadata = new ToolMetadata
	{
		Operations = new Dictionary<string, OperationMetadata>(StringComparer.OrdinalIgnoreCase)
		{
			["Create"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Definitions" },
				Description = "Create one or more tables in the semantic model.\nMandatory properties: Definitions (list with Name, and one of DaxExpression, MExpression, EntityName, or SqlQuery for each).\n- DaxExpression: For calculated tables\n- MExpression: For M-script tables\n- EntityName: For entity-based tables (requires either ExpressionSourceName or DataSourceName, optionally SchemaName)\n- SqlQuery: For query-based tables (requires DataSourceName)\nOptional: Description, DataCategory, IsHidden, ShowAsVariationsOnly, IsPrivate, AlternateSourcePrecedence, ExcludeFromModelRefresh, LineageTag, SourceLineageTag, SystemManaged, PartitionName, Mode, Annotations, ExtendedProperties.\nIMPORTANT: For MExpression, SqlQuery, and EntityName tables, you MUST specify Columns with Name and DataType for each column - the schema cannot be auto-inferred from the expression. SourceColumn defaults to Name if not specified. For DaxExpression (calculated) tables, do NOT specify Columns as they are auto-derived from the DAX expression.\nBatch options: Set Options.ContinueOnError to continue processing remaining items after failures (default: false). Set Options.UseTransaction for transactional behavior (default: true).",
				CommonMistakes = new string[4] { "Not providing Columns with Name and DataType for MExpression, SqlQuery, or EntityName tables", "Not providing one of DaxExpression, MExpression, EntityName, or SqlQuery in Definitions", "Not providing either ExpressionSourceName or DataSourceName when using EntityName", "Not providing DataSourceName when using SqlQuery" },
				Tips = new string[3] { "'Measures' is a reserved word and cannot be used as a table name", "DirectLake mode requires EntityName with ExpressionSourceName (and optionally SchemaName)", "For single-item operations, provide a list with one element" },
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            { \n                \"Name\": \"Sales\", \n                \"DaxExpression\": \"SUMMARIZECOLUMNS(DimDate[Year], DimProduct[ProductName], DimCustomer[CustomerName], \\\"Sales\\\", [Sales])\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            {\n                \"Name\": \"Sales\",\n                \"Mode\": \"Import\",\n                \"MExpression\": \"let Source = Excel.CurrentWorkbook(){[Name=\\\"SalesData\\\"]}[Content], FilteredRows = Table.SelectRows(Source, each [Region] = \\\"West\\\") in FilteredRows\",\n                \"Columns\": [\n                    {\n                        \"Name\": \"Region\",\n                        \"DataType\": \"String\",\n                        \"IsNullable\": true,\n                        \"Ordinal\": 0\n                    },\n                    {\n                        \"Name\": \"Amount\",\n                        \"DataType\": \"Decimal\",\n                        \"IsNullable\": true,\n                        \"Ordinal\": 1\n                    }\n                ]\n            },\n            { \n                \"Name\": \"Customer\",\n                \"Mode\": \"DirectLake\",\n                \"EntityName\": \"Customer\",\n                \"SchemaName\": \"dbo\",\n                \"ExpressionSourceName\": \"SalesLakehouse\",\n                \"Columns\": [\n                    {\n                        \"Name\": \"CustomerId\",\n                        \"DataType\": \"Int64\",\n                        \"IsNullable\": false,\n                        \"IsKey\": true,\n                        \"Ordinal\": 0\n                    },\n                    {\n                        \"Name\": \"CustomerName\",\n                        \"DataType\": \"String\",\n                        \"IsNullable\": true,\n                        \"Ordinal\": 1\n                    },\n                    {\n                        \"Name\": \"Email\",\n                        \"DataType\": \"String\",\n                        \"IsNullable\": true,\n                        \"Ordinal\": 2\n                    }\n                ]\n            }\n        ],\n        \"Options\": { \"ContinueOnError\": true }\n    }\n}" }
			},
			["CreateFieldParameter"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "FieldParameterDefinitions" },
				Description = "Create one or more field parameter tables.\nField parameters are calculated tables that let users dynamically switch which fields appear in a visual.\nMandatory properties: FieldParameterDefinitions (list with Name and Fields for each).\nEach field in Fields requires Name and ObjectType (Column or Measure). TableName is required for Column references. For Measure references, TableName is optional — if omitted, the measure is resolved globally by name (measure names are globally unique). If TableName is supplied for a Measure, the measure must exist in that specific table. DisplayName is optional and defaults to the referenced object's name.\nThe operation generates the calculated-table DAX, creates the three CalculatedTableColumn objects with required metadata, and persists through the standard transaction path.\nSubsequent operations (Get, Rename, Delete, ExportTMDL, column_operations.Get) work on the created table without special cases.\nBatch options: Set Options.ContinueOnError to continue processing remaining items after failures (default: false). Set Options.UseTransaction for transactional behavior (default: true).",
				CommonMistakes = new string[4] { "Not providing FieldParameterDefinitions", "Omitting ObjectType or using an unsupported value (must be Column or Measure)", "Providing an incorrect TableName for a Measure (the measure must exist in that specific table if TableName is given)", "Omitting TableName for a Column (required for columns, optional for measures)" },
				Tips = new string[4] { "Use column_operations or partition_operations to make follow-up changes after creation", "DisplayName overrides the label shown in the field parameter; defaults to the referenced object name", "For Measure references, TableName can be omitted — the measure is found automatically by name across the entire model", "For single-item operations, provide a list with one element" },
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"CreateFieldParameter\",\n        \"FieldParameterDefinitions\": [\n            {\n                \"Name\": \"Slice by\",\n                \"Fields\": [\n                    {\n                        \"TableName\": \"Product\",\n                        \"Name\": \"Product Name\",\n                        \"ObjectType\": \"Column\"\n                    },\n                    {\n                        \"TableName\": \"Internet Sales\",\n                        \"Name\": \"Internet Sales Amount\",\n                        \"ObjectType\": \"Measure\",\n                        \"DisplayName\": \"Revenue\"\n                    }\n                ]\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"CreateFieldParameter\",\n        \"FieldParameterDefinitions\": [\n            {\n                \"Name\": \"Slice by Metric\",\n                \"Fields\": [\n                    {\n                        \"TableName\": \"Product\",\n                        \"Name\": \"Product Name\",\n                        \"ObjectType\": \"Column\"\n                    },\n                    {\n                        \"Name\": \"Total Sales Amount\",\n                        \"ObjectType\": \"Measure\",\n                        \"DisplayName\": \"Revenue\"\n                    }\n                ]\n            }\n        ]\n    }\n}" }
			},
			["Update"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Definitions" },
				Description = "Update properties of one or more existing tables.\nMandatory properties: Definitions (list with Name for each).\nOptional: Description, DataCategory, IsHidden, ShowAsVariationsOnly, IsPrivate, AlternateSourcePrecedence, ExcludeFromModelRefresh, LineageTag, SourceLineageTag, SystemManaged, Annotations, ExtendedProperties.\nsingle-partition source updates: DaxExpression for calculated tables, MExpression for M tables, or EntityName with exactly one of ExpressionSourceName/DataSourceName for entity tables are supported when the table has exactly one partition and the requested source kind matches the existing partition source.\nNot supported: SqlQuery, PartitionName, Mode, and Columns. For a multi-partition table or any partition-specific edit, use partition_operations.Update with TableName, Name, SourceType, and the source-specific field. MExpression changes warn that column schema is not auto-synchronized.\nBatch options: Set Options.ContinueOnError to continue processing remaining items after failures (default: false). Set Options.UseTransaction for transactional behavior (default: true).",
				CommonMistakes = new string[4] { "Attempting a table-level source update on a multi-partition table; use partition_operations.Update with explicit TableName and Name", "Providing more than one source kind (DaxExpression, MExpression, EntityName) in the same table update", "Providing Columns during table update; use column_operations for schema changes", "Using PartitionName or Mode in table update; use partition_operations.Update for partition properties" },
				Tips = new string[3] { "Use table_operations.Update for same-kind source changes on single-partition tables: DaxExpression for calculated, MExpression for M, or EntityName with exactly one source reference for entity", "Use partition_operations.Update for multi-partition tables because expressions are stored on partitions and the partition Name disambiguates the target", "After MExpression updates, review columns and use column_operations if schema needs to change" },
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Update\",\n        \"Definitions\": [\n            { \n                \"Name\": \"Sales\", \n                \"Description\": \"Description of the Sales table\",\n                \"ShowAsVariationsOnly\": true\n            },\n            {\n                \"Name\": \"Customer\",\n                \"IsHidden\": true\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Update\",\n        \"Definitions\": [\n            {\n                \"Name\": \"Calculated Calendar\",\n                \"DaxExpression\": \"REPLACE_WITH_DAX_TABLE_EXPRESSION\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Update\",\n        \"Definitions\": [\n            {\n                \"Name\": \"Date\",\n                \"MExpression\": \"REPLACE_WITH_M_EXPRESSION\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Update\",\n        \"Definitions\": [\n            {\n                \"Name\": \"Customer\",\n                \"EntityName\": \"REPLACE_WITH_ENTITY_NAME\",\n                \"ExpressionSourceName\": \"REPLACE_WITH_EXPRESSION_SOURCE_NAME\"\n            }\n        ]\n    }\n}" }
			},
			["Delete"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Delete one or more tables from the semantic model.\nMandatory properties: References (list with Name for each table to delete).\nOptional: ShouldCascadeDelete (at request level, default: true) - when true, dependent objects (columns, relationships, etc.) will be automatically deleted.\nBatch options: Set Options.ContinueOnError to continue processing remaining items after failures (default: false). Set Options.UseTransaction for transactional behavior (default: true).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Delete\",\n        \"References\": [\n            { \"Name\": \"ObsoleteTable\" },\n            { \"Name\": \"TempTable\" }\n        ],\n        \"ShouldCascadeDelete\": true\n    }\n}" }
			},
			["Get"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Retrieve detailed information about one or more tables.\nMandatory properties: References (list with Name for each table to retrieve).\nReturns table properties, columns, measures, hierarchies, and partition details.\nBatch options: Set Options.ContinueOnError to continue processing remaining items after failures (default: false).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Get\",\n        \"References\": [\n            { \"Name\": \"Sales\" },\n            { \"Name\": \"Customer\" }\n        ]\n    }\n}" }
			},
			["List"] = new OperationMetadata
			{
				Description = "List all tables in the semantic model.\nMandatory properties: None.\nOptional: None.\nReturns summary information for all tables including column count, measure count, hierarchy count, and partition count.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"List\"\n    }\n}" }
			},
			["RefreshWithXMLA"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Refresh data for a specific table synchronously using XMLA/TOM.\nBlocks until the refresh completes. Best for quick recalculations.\nFor long-running data refreshes, use RefreshWithAPI instead.\nThis operation reloads data from the underlying data source(s).\nMandatory properties: References (single item with Name).\nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"RefreshWithXMLA\",\n        \"References\": [{ \"Name\": \"Sales\" }]\n    }\n}" }
			},
			["RefreshWithAPI"] = new OperationMetadata
			{
				Description = "Start an asynchronous data refresh for a table via the Power BI Enhanced Refresh REST API.\nReturns immediately with a request ID. Use CheckStatusOfRefreshWithAPI to monitor progress.\nMandatory properties: References (single item with Name for the table to refresh).\nOptional: RefreshType (Automatic, Full, DataOnly, Calculate).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"RefreshWithAPI\",\n        \"References\": [{ \"Name\": \"Sales\" }],\n        \"RefreshType\": \"Full\"\n    }\n}" }
			},
			["CheckStatusOfRefreshWithAPI"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "RequestId" },
				Description = "Check the status of an async refresh started with RefreshWithAPI.\nMandatory properties: RequestId (from RefreshWithAPI response).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"CheckStatusOfRefreshWithAPI\",\n        \"RequestId\": \"abc-123-def\"\n    }\n}" }
			},
			["CancelRefreshWithAPI"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "RequestId" },
				Description = "Cancel an in-progress async refresh started with RefreshWithAPI.\nOnly one refresh can run per dataset at a time â€” cancel is needed to start a new one.\nMandatory properties: RequestId (from RefreshWithAPI response).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"CancelRefreshWithAPI\",\n        \"RequestId\": \"abc-123-def\"\n    }\n}" }
			},
			["Rename"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "RenameDefinitions" },
				Description = "Rename one or more tables and automatically update all references.\nMandatory properties: RenameDefinitions (list with CurrentName and NewName for each).\nAll DAX expressions, relationships, and other references will be automatically updated.\nBatch options: Set Options.ContinueOnError to continue processing remaining items after failures (default: false). Set Options.UseTransaction for transactional behavior (default: true).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Rename\",\n        \"RenameDefinitions\": [\n            { \n                \"CurrentName\": \"OldTableName\", \n                \"NewName\": \"NewTableName\"\n            },\n            {\n                \"CurrentName\": \"Legacy_Sales\",\n                \"NewName\": \"Sales\"\n            }\n        ]\n    }\n}" }
			},
			["GetSchema"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Retrieve the schema information for a specific table.\nMandatory properties: References (single item with Name).\nOptional: None.\nReturns detailed column definitions, data types, and relationships.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"GetSchema\",\n        \"References\": [{ \"Name\": \"Sales\" }]\n    }\n}" }
			},
			["ExportTMDL"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Export table definition to TMDL (YAML-like syntax) format for human-readable declarative model definition.\nMandatory properties: References (single item with Name).\nOptional: TmdlExportOptions (TmdlSerializationOptions.IncludeChildren defaults to false).\nTMDL is ideal for version control and collaborative development.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ExportTMDL\",\n        \"TmdlExportOptions\": {\n            \"TmdlSerializationOptions\": {\n                \"IncludeChildren\": true\n            }\n        },\n        \"References\": [{ \"Name\": \"Sales\" }]\n    }\n}" }
			},
			["ExportTMSL"] = new OperationMetadata
			{
				RequiredParams = new string[2] { "References", "TmslExportOptions" },
				Description = "Export table to TMSL (JSON syntax) script format with specified operation type for generating executable scripts.\nMandatory properties: References (single item with Name), TmslExportOptions (with TmslOperationType).\nOptional: RefreshType (for Refresh operations), IncludeRestricted (for Refresh operations).\nTMSL generates JSON scripts that can be executed against Analysis Services.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ExportTMSL\",\n        \"References\": [{ \"Name\": \"Sales\" }],\n        \"TmslExportOptions\": {\n            \"TmslOperationType\": \"CreateOrReplace\"\n        }\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"ExportTMSL\",\n        \"References\": [{ \"Name\": \"Sales\" }],\n        \"TmslExportOptions\": {\n            \"TmslOperationType\": \"Refresh\",\n            \"RefreshType\": \"Full\",\n            \"IncludeRestricted\": true\n        }\n    }\n}" }
			},
			["MarkAsDateTable"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "MarkAsDateTableDefinitions" },
				Description = "Mark one or more tables as date tables in the semantic model.\nMandatory properties: MarkAsDateTableDefinitions (list with TableName for each).\nOptional: DateColumnName â€” when omitted, auto-detection finds a suitable DateTime column based on uniqueness, key status, or relationship position.\nSetting a table as a date table enables time intelligence functions (DATESYTD, TOTALYTD, SAMEPERIODLASTYEAR, etc.).\nBatch options: Set Options.ContinueOnError to continue processing remaining items after failures (default: false). Set Options.UseTransaction for transactional behavior (default: true).",
				CommonMistakes = new string[2] { "Specifying a column that does not have DateTime data type", "Not providing MarkAsDateTableDefinitions" },
				Tips = new string[3] { "DateColumnName is optional â€” the engine will auto-detect a suitable DateTime column if one can be unambiguously identified", "The date column should contain unique, contiguous date values covering the full range of dates in your data", "Auto-detection looks for DateTime columns that are unique, are key columns, or are on the one-end of a relationship" },
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"MarkAsDateTable\",\n        \"MarkAsDateTableDefinitions\": [\n            { \"TableName\": \"Date\", \"DateColumnName\": \"Date\" }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"MarkAsDateTable\",\n        \"MarkAsDateTableDefinitions\": [\n            { \"TableName\": \"Calendar\" },\n            { \"TableName\": \"FiscalCalendar\", \"DateColumnName\": \"FiscalDate\" }\n        ],\n        \"Options\": { \"ContinueOnError\": true }\n    }\n}" }
			},
			["Help"] = new OperationMetadata
			{
				Description = "Display comprehensive help information about the table operations tool and all available operations.\nMandatory properties: None.\nOptional: None.\nReturns detailed documentation for each operation with examples.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Help\"\n    }\n}" }
			}
		}
	};

	public TableOperationsTool(ILogger<TableOperationsTool> logger, IWriteGuard writeGuard, IEnhancedRefreshService? enhancedRefreshService = null)
	{
		_logger = logger;
		_writeGuard = writeGuard;
		_enhancedRefreshService = enhancedRefreshService;
	}

	private static TableOperationResponse MapBatchResponse(BatchOperationResponse batchResponse, string operation)
	{
		TableOperationResponse obj = new TableOperationResponse
		{
			Success = batchResponse.Success,
			Message = batchResponse.Message,
			Operation = operation,
			Summary = batchResponse.Summary,
			Results = batchResponse.Results,
			Warnings = batchResponse.Warnings
		};
		obj.Exceptions.AddRange(batchResponse.Exceptions);
		return obj;
	}

	[McpServerTool(Name = "table_operations", Title = "Table Operations", ReadOnly = false, Destructive = true)]
	[YamlToolDescription("table_operations")]
	public async Task<CallToolResult> ExecuteTableOperation(McpServer mcpServer, TableOperationRequest request)
	{
		_logger.LogDebug("Executing {ToolName}.{Operation}: Connection={ConnectionName}", "TableOperationsTool", request.Operation, request.ConnectionName ?? "(last used)");
		string[] validOperations = new string[16]
		{
			"CREATE", "CREATEFIELDPARAMETER", "UPDATE", "DELETE", "GET", "LIST", "REFRESHWITHXMLA", "REFRESHWITHAPI", "CHECKSTATUSOFREFRESHWITHAPI", "CANCELREFRESHWITHAPI",
			"RENAME", "MARKASDATETABLE", "GETSCHEMA", "EXPORTTMDL", "EXPORTTMSL", "HELP"
		};
		string[] writeOperations = new string[8] { "CREATE", "CREATEFIELDPARAMETER", "UPDATE", "DELETE", "REFRESHWITHXMLA", "REFRESHWITHAPI", "RENAME", "MARKASDATETABLE" };
		string op = request.Operation.ToUpperInvariant();
		ToolCallAnnotations annotations = ToolCallAnnotations.Create("table_operations", request.Operation, !Enumerable.Contains(writeOperations, op));
		CallToolResult result = null;
		try
		{
			if (!Enumerable.Contains(validOperations, op))
			{
				_logger.LogWarning("Invalid operation '{Operation}' requested for {ToolName}. Valid operations: {ValidOperations}", request.Operation, "TableOperationsTool", string.Join(", ", validOperations));
				CallToolResult result2;
				result = (result2 = CallToolResultHelper.FromResponse(TableOperationResponse.Forbidden(request.Operation, "Invalid operation: " + request.Operation + ". Supported operations: " + string.Join(", ", validOperations)), annotations));
				return result2;
			}
			var (flag, text) = ValidateRequest(request.Operation, request);
			if (!flag)
			{
				_logger.LogWarning("Invalid request for {Operation} operation: {ValidationError}", request.Operation, text);
				CallToolResult result2;
				result = (result2 = CallToolResultHelper.Error(request.Operation, text, annotations, ErrorSource.User));
				return result2;
			}
			if (Enumerable.Contains(writeOperations, request.Operation.ToUpperInvariant()))
			{
				WriteOperationResult writeOperationResult = await _writeGuard.ExecuteWriteOperationWithGuards(mcpServer, request.ConnectionName, request.Operation);
				if (!writeOperationResult.Success)
				{
					_logger.LogWarning("{ToolName}.{Operation} blocked by write guard: {Reason}", "TableOperationsTool", request.Operation, writeOperationResult.Message);
					CallToolResult result2;
					result = (result2 = CallToolResultHelper.FromResponse(TableOperationResponse.Forbidden(request.Operation, writeOperationResult.Message), annotations));
					return result2;
				}
			}
			bool isWriteEnabled = _writeGuard.IsWriteEnabled;
			CallToolResult result3;
			result = (result3 = request.Operation.ToUpperInvariant() switch
			{
				"CREATE" => CallToolResultHelper.FromResponse(await HandleCreateOperation(request), annotations, null, minimalSuccessPayload: true), 
				"CREATEFIELDPARAMETER" => CallToolResultHelper.FromResponse(await HandleCreateFieldParameterOperation(request), annotations, null, minimalSuccessPayload: true), 
				"UPDATE" => CallToolResultHelper.FromResponse(await HandleUpdateOperation(request), annotations, null, minimalSuccessPayload: true), 
				"DELETE" => CallToolResultHelper.FromResponse(await HandleDeleteOperation(request), annotations, null, minimalSuccessPayload: true), 
				"GET" => CallToolResultHelper.FromResponse(await HandleGetOperation(request), annotations), 
				"LIST" => CallToolResultHelper.FromResponse(await HandleListOperation(request), annotations), 
				"REFRESHWITHXMLA" => CallToolResultHelper.FromResponse(await HandleRefreshWithXMLAOperation(request), annotations, null, minimalSuccessPayload: true), 
				"REFRESHWITHAPI" => CallToolResultHelper.FromResponse(await HandleRefreshWithAPIOperation(request), annotations), 
				"CHECKSTATUSOFREFRESHWITHAPI" => CallToolResultHelper.FromResponse(await HandleCheckStatusOfRefreshWithAPIOperation(request), annotations), 
				"CANCELREFRESHWITHAPI" => CallToolResultHelper.FromResponse(await HandleCancelRefreshWithAPIOperation(request), annotations), 
				"RENAME" => CallToolResultHelper.FromResponse(await HandleRenameOperation(request), annotations, null, minimalSuccessPayload: true), 
				"MARKASDATETABLE" => CallToolResultHelper.FromResponse(await HandleMarkAsDateTableOperation(request), annotations, null, minimalSuccessPayload: true), 
				"GETSCHEMA" => CallToolResultHelper.FromResponse(await HandleGetSchemaOperation(request), annotations), 
				"EXPORTTMDL" => CallToolResultHelper.FromExportResponse(await HandleExportTMDLOperation(request), (request.References?.FirstOrDefault()?.Name ?? "table") + ".tmdl", "text/plain", annotations), 
				"EXPORTTMSL" => CallToolResultHelper.FromExportResponse(await HandleExportTMSLOperation(request), (request.References?.FirstOrDefault()?.Name ?? "table") + ".json", "text/plain", annotations), 
				"HELP" => CallToolResultHelper.FromResponse(HandleHelpOperation(request, isWriteEnabled ? validOperations : validOperations.Except(writeOperations).ToArray()), annotations), 
				_ => CallToolResultHelper.FromResponse(TableOperationResponse.Forbidden(request.Operation, "Operation " + request.Operation + " is not implemented"), annotations), 
			});
			return result3;
		}
		catch (Exception ex)
		{
			_logger.LogOperationError("TableOperationsTool", request.Operation, ex);
			string message = op switch
			{
				"CREATE" => "Error creating tables: " + ex.GetErrorMessage(), 
				"CREATEFIELDPARAMETER" => "Error creating field parameter tables: " + ex.GetErrorMessage(), 
				"UPDATE" => "Error updating tables: " + ex.GetErrorMessage(), 
				"DELETE" => "Error deleting tables: " + ex.GetErrorMessage(), 
				"GET" => "Error getting tables: " + ex.GetErrorMessage(), 
				"LIST" => "Error listing tables: " + ex.GetErrorMessage(), 
				"REFRESHWITHXMLA" => "Error refreshing table: " + ex.GetErrorMessage(), 
				"REFRESHWITHAPI" => "Failed to start refresh: " + ex.GetErrorMessage(), 
				"CHECKSTATUSOFREFRESHWITHAPI" => "Failed to check refresh status: " + ex.GetErrorMessage(), 
				"CANCELREFRESHWITHAPI" => "Failed to cancel refresh: " + ex.GetErrorMessage(), 
				"RENAME" => "Error renaming tables: " + ex.GetErrorMessage(), 
				"MARKASDATETABLE" => "Error marking table as date table: " + ex.GetErrorMessage(), 
				"GETSCHEMA" => "Error getting table schema: " + ex.GetErrorMessage(), 
				"EXPORTTMDL" => "Error getting table TMDL: " + ex.GetErrorMessage(), 
				"EXPORTTMSL" => "Error generating table TMSL: " + ex.GetErrorMessage(), 
				_ => "Error executing table operation: " + ex.GetErrorMessage(), 
			};
			CallToolResult result2;
			result = (result2 = CallToolResultHelper.FromResponse(new TableOperationResponse
			{
				Success = false,
				Message = message,
				Operation = request.Operation
			}, annotations, ex));
			return result2;
		}
		finally
		{
			_logger.LogToolCallCompleted(annotations.Title, !annotations.ReadOnlyHint, result?.IsError ?? true, ConnectionOperations.ResolveSemanticModelId());
		}
	}

	private async Task<TableOperationResponse> HandleCreateOperation(TableOperationRequest request)
	{
		BatchOperationResponse batchOperationResponse = await TableOperations.CreateTables(request.ConnectionName, request.Definitions, request.Options, _writeGuard);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Success={Success}", "TableOperationsTool", request.Operation, request.ConnectionName, batchOperationResponse.Success);
		return MapBatchResponse(batchOperationResponse, request.Operation);
	}

	private async Task<TableOperationResponse> HandleCreateFieldParameterOperation(TableOperationRequest request)
	{
		BatchOperationResponse batchOperationResponse = await TableOperations.CreateFieldParameterTables(request.ConnectionName, request.FieldParameterDefinitions, request.Options, _writeGuard);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Success={Success}", "TableOperationsTool", request.Operation, request.ConnectionName, batchOperationResponse.Success);
		return MapBatchResponse(batchOperationResponse, request.Operation);
	}

	private async Task<TableOperationResponse> HandleUpdateOperation(TableOperationRequest request)
	{
		BatchOperationResponse batchOperationResponse = await TableOperations.UpdateTablesInternal(request.ConnectionName, request.Definitions, request.Options, _writeGuard);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Success={Success}", "TableOperationsTool", request.Operation, request.ConnectionName, batchOperationResponse.Success);
		return MapBatchResponse(batchOperationResponse, request.Operation);
	}

	private async Task<TableOperationResponse> HandleDeleteOperation(TableOperationRequest request)
	{
		BatchOperationResponse batchOperationResponse = await TableOperations.DeleteTables(request.ConnectionName, request.References, request.ShouldCascadeDelete, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Success={Success}", "TableOperationsTool", request.Operation, request.ConnectionName, batchOperationResponse.Success);
		return MapBatchResponse(batchOperationResponse, request.Operation);
	}

	private async Task<TableOperationResponse> HandleGetOperation(TableOperationRequest request)
	{
		BatchOperationResponse batchOperationResponse = await TableOperations.GetTables(request.ConnectionName, request.References, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Success={Success}", "TableOperationsTool", request.Operation, request.ConnectionName, batchOperationResponse.Success);
		return MapBatchResponse(batchOperationResponse, request.Operation);
	}

	private async Task<TableOperationResponse> HandleListOperation(TableOperationRequest request)
	{
		List<TableList> list = await TableOperations.ListTables(request.ConnectionName);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}", "TableOperationsTool", request.Operation, request.ConnectionName, list.Count);
		return new TableOperationResponse
		{
			Success = true,
			Message = $"Found {list.Count} tables",
			Operation = request.Operation,
			Data = list
		};
	}

	private async Task<TableOperationResponse> HandleRefreshWithXMLAOperation(TableOperationRequest request)
	{
		TableReference reference = request.References.First();
		await TableOperations.RefreshTable(request.ConnectionName, reference.Name);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "TableOperationsTool", request.Operation, request.ConnectionName);
		return new TableOperationResponse
		{
			Success = true,
			Message = "Table '" + reference.Name + "' refreshed successfully",
			Operation = request.Operation
		};
	}

	private async Task<TableOperationResponse> HandleRefreshWithAPIOperation(TableOperationRequest request)
	{
		string tableName = request.References?.FirstOrDefault()?.Name;
		TableOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(request.ConnectionName))
		{
			if (!connectionInfo.IsCloudConnection)
			{
				result = new TableOperationResponse
				{
					Success = false,
					Message = "This operation is only supported for Fabric cloud connections. Use RefreshWithXMLA instead.",
					Operation = request.Operation
				};
			}
			else
			{
				string text = connectionInfo.Database?.ID;
				if (string.IsNullOrEmpty(text))
				{
					result = new TableOperationResponse
					{
						Success = false,
						Message = "No database ID available.",
						Operation = request.Operation
					};
				}
				else
				{
					string workspaceId = connectionInfo.WorkspaceId;
					EnhancedRefreshResult enhancedRefreshResult = await (_enhancedRefreshService ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("RefreshWithAPI requires IEnhancedRefreshService to be registered.")).StartRefreshAsync(workspaceId, text, request.RefreshType ?? "Automatic", tableName);
					result = new TableOperationResponse
					{
						Success = enhancedRefreshResult.Success,
						Message = enhancedRefreshResult.Message + ((enhancedRefreshResult.RequestId != null) ? (" Use CheckStatusOfRefreshWithAPI with RequestId '" + enhancedRefreshResult.RequestId + "' to monitor progress.") : string.Empty),
						Operation = request.Operation,
						Data = new { enhancedRefreshResult.RequestId }
					};
				}
			}
		}
		return result;
	}

	private async Task<TableOperationResponse> HandleCheckStatusOfRefreshWithAPIOperation(TableOperationRequest request)
	{
		if (string.IsNullOrEmpty(request.RequestId))
		{
			return new TableOperationResponse
			{
				Success = false,
				Message = "RequestId is required. Use the request ID returned by RefreshWithAPI.",
				Operation = request.Operation
			};
		}
		TableOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(request.ConnectionName))
		{
			if (!connectionInfo.IsCloudConnection)
			{
				result = new TableOperationResponse
				{
					Success = false,
					Message = "This operation is only supported for Fabric cloud connections. Use RefreshWithXMLA instead.",
					Operation = request.Operation
				};
			}
			else
			{
				string text = connectionInfo.Database?.ID;
				if (string.IsNullOrEmpty(text))
				{
					result = new TableOperationResponse
					{
						Success = false,
						Message = "No database ID available.",
						Operation = request.Operation
					};
				}
				else
				{
					string workspaceId = connectionInfo.WorkspaceId;
					EnhancedRefreshStatusResult enhancedRefreshStatusResult = await (_enhancedRefreshService ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("RefreshWithAPI requires IEnhancedRefreshService to be registered.")).GetRefreshStatusAsync(workspaceId, text, request.RequestId);
					result = new TableOperationResponse
					{
						Success = (enhancedRefreshStatusResult.Status != "Failed" && enhancedRefreshStatusResult.Status != "Error"),
						Message = enhancedRefreshStatusResult.Message,
						Operation = request.Operation,
						Data = new { enhancedRefreshStatusResult.Status, enhancedRefreshStatusResult.RequestId, enhancedRefreshStatusResult.StartTime, enhancedRefreshStatusResult.EndTime }
					};
				}
			}
		}
		return result;
	}

	private async Task<TableOperationResponse> HandleCancelRefreshWithAPIOperation(TableOperationRequest request)
	{
		if (string.IsNullOrEmpty(request.RequestId))
		{
			return new TableOperationResponse
			{
				Success = false,
				Message = "RequestId is required.",
				Operation = request.Operation
			};
		}
		TableOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(request.ConnectionName))
		{
			if (!connectionInfo.IsCloudConnection)
			{
				result = new TableOperationResponse
				{
					Success = false,
					Message = "This operation is only supported for Fabric cloud connections. Use RefreshWithXMLA instead.",
					Operation = request.Operation
				};
			}
			else
			{
				string text = connectionInfo.Database?.ID;
				if (string.IsNullOrEmpty(text))
				{
					result = new TableOperationResponse
					{
						Success = false,
						Message = "No database ID available.",
						Operation = request.Operation
					};
				}
				else
				{
					string workspaceId = connectionInfo.WorkspaceId;
					await (_enhancedRefreshService ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("RefreshWithAPI requires IEnhancedRefreshService to be registered.")).CancelRefreshAsync(workspaceId, text, request.RequestId);
					result = new TableOperationResponse
					{
						Success = true,
						Message = "Refresh " + request.RequestId + " cancelled successfully.",
						Operation = request.Operation
					};
				}
			}
		}
		return result;
	}

	private async Task<TableOperationResponse> HandleRenameOperation(TableOperationRequest request)
	{
		BatchOperationResponse batchOperationResponse = await TableOperations.RenameTables(request.ConnectionName, request.RenameDefinitions, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Success={Success}", "TableOperationsTool", request.Operation, request.ConnectionName, batchOperationResponse.Success);
		return MapBatchResponse(batchOperationResponse, request.Operation);
	}

	private async Task<TableOperationResponse> HandleMarkAsDateTableOperation(TableOperationRequest request)
	{
		BatchOperationResponse batchOperationResponse = await TableOperations.MarkAsDateTables(request.ConnectionName, request.MarkAsDateTableDefinitions, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Success={Success}", "TableOperationsTool", request.Operation, request.ConnectionName, batchOperationResponse.Success);
		return MapBatchResponse(batchOperationResponse, request.Operation);
	}

	private async Task<TableOperationResponse> HandleGetSchemaOperation(TableOperationRequest request)
	{
		TableReference reference = request.References.First();
		Dictionary<string, object> data = await TableOperations.GetTableSchema(request.ConnectionName, reference.Name);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "TableOperationsTool", request.Operation, request.ConnectionName);
		return new TableOperationResponse
		{
			Success = true,
			Message = "Table schema for '" + reference.Name + "' retrieved successfully",
			Operation = request.Operation,
			Data = data
		};
	}

	private async Task<TableOperationResponse> HandleExportTMDLOperation(TableOperationRequest request)
	{
		ExportValidationResult validation = ExportValidationHelper.ValidateReferences(request.References, "Table");
		if (!validation.IsValid)
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new TableOperationResponse
			{
				Success = false,
				Operation = "ExportTMDL",
				Message = validation.ErrorMessage,
				Help = value
			};
		}
		TableReference reference = request.References.First();
		ExportValidationResult exportValidationResult = ExportValidationHelper.ValidateName(reference.Name, "Table");
		if (!exportValidationResult.IsValid)
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value2);
			return new TableOperationResponse
			{
				Success = false,
				Operation = "ExportTMDL",
				Message = exportValidationResult.ErrorMessage,
				Help = value2
			};
		}
		TmdlExportResult tmdlExportResult = await TableOperations.ExportTMDL(request.ConnectionName, reference.Name, request.TmdlExportOptions ?? new ExportTmdl());
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "TableOperationsTool", request.Operation, request.ConnectionName);
		string text = ExportValidationHelper.FormatSuccessMessage("Table", reference.Name, validation.WarningMessage);
		List<string> warnings = ((!string.IsNullOrEmpty(validation.WarningMessage)) ? new List<string> { validation.WarningMessage } : null);
		return new TableOperationResponse
		{
			Success = tmdlExportResult.Success,
			Message = (tmdlExportResult.Success ? text : (tmdlExportResult.ErrorMessage ?? "Failed to export TMDL")),
			ErrorSource = tmdlExportResult.ErrorSource,
			Operation = request.Operation,
			Data = tmdlExportResult.Content,
			Warnings = warnings
		};
	}

	private async Task<TableOperationResponse> HandleExportTMSLOperation(TableOperationRequest request)
	{
		ExportValidationResult validation = ExportValidationHelper.ValidateReferences(request.References, "Table", "ExportTMSL");
		if (!validation.IsValid)
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new TableOperationResponse
			{
				Success = false,
				Operation = "ExportTMSL",
				Message = validation.ErrorMessage,
				Help = value
			};
		}
		if (string.IsNullOrWhiteSpace(request.TmslExportOptions?.TmslOperationType))
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value2);
			return new TableOperationResponse
			{
				Success = false,
				Operation = "ExportTMSL",
				Message = "TmslOperationType is required in TmslExportOptions. Valid values: Create, CreateOrReplace, Alter, Delete, Refresh",
				Help = value2
			};
		}
		TableReference reference = request.References.First();
		TmslExportResult tmslExportResult = await TableOperations.ExportTMSL(request.ConnectionName, reference.Name, request.TmslExportOptions);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, OperationType={OperationType}, Success={Success}", "TableOperationsTool", request.Operation, request.ConnectionName, request.TmslExportOptions.TmslOperationType, tmslExportResult.Success);
		string message = (tmslExportResult.Success ? ExportValidationHelper.FormatTmslSuccessMessage("Table", reference.Name, request.TmslExportOptions.TmslOperationType, validation.WarningMessage) : (tmslExportResult.ErrorMessage ?? "Unknown error occurred"));
		List<string> warnings = ((!string.IsNullOrEmpty(validation.WarningMessage)) ? new List<string> { validation.WarningMessage } : null);
		return new TableOperationResponse
		{
			Success = tmslExportResult.Success,
			Message = message,
			ErrorSource = tmslExportResult.ErrorSource,
			Operation = request.Operation,
			Data = tmslExportResult.Content,
			Warnings = warnings
		};
	}

	private TableOperationResponse HandleHelpOperation(TableOperationRequest request, string[] operations)
	{
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Operations={OperationCount}", "TableOperationsTool", request.Operation, request.ConnectionName, operations.Length);
		TableOperationResponse tableOperationResponse = new TableOperationResponse();
		tableOperationResponse.Success = true;
		tableOperationResponse.Message = "Help information for the table operations";
		tableOperationResponse.Operation = request.Operation;
		tableOperationResponse.Help = new
		{
			ToolName = "table_operations",
			Description = "Perform operations on semantic model tables.",
			SupportedOperations = operations,
			Examples = toolMetadata.Operations.Where<KeyValuePair<string, OperationMetadata>>((KeyValuePair<string, OperationMetadata> p) => operations.Contains<string>(p.Key, StringComparer.OrdinalIgnoreCase)),
			Notes = new string[9] { "The Operation parameter specifies which operation to perform.", "The ConnectionName parameter is optional and uses the last used connection if not provided.", "For Create/Update operations, use the Definitions property with a list of TableDefinition objects.", "For Delete/Get/Refresh/GetSchema/ExportTMDL/ExportTMSL operations, use the References property.", "For Rename operations, use the RenameDefinitions property.", "For MarkAsDateTable operations, use the MarkAsDateTableDefinitions property.", "For CreateFieldParameter operations, use the FieldParameterDefinitions property with a list of FieldParameterDefinition objects.", "Use Options.ContinueOnError to control error handling in batch operations.", "Use Options.UseTransaction to control transactional behavior." }
		};
		return tableOperationResponse;
	}

	private (bool isValid, string? errorMessage) ValidateRequest(string operation, TableOperationRequest request)
	{
		if (!toolMetadata.Operations.TryGetValue(operation, out OperationMetadata value))
		{
			return (isValid: true, errorMessage: null);
		}
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		string[] requiredParams = value.RequiredParams;
		int num = 0;
		while (num < requiredParams.Length)
		{
			string text = requiredParams[num];
			if (text == null)
			{
				goto IL_01bb;
			}
			int length = text.Length;
			bool flag;
			if (length <= 11)
			{
				if (length != 10)
				{
					if (length != 11 || !(text == "Definitions"))
					{
						goto IL_01bb;
					}
					flag = request.Definitions?.Any() ?? false;
				}
				else
				{
					if (!(text == "References"))
					{
						goto IL_01bb;
					}
					flag = request.References?.Any() ?? false;
				}
			}
			else if (length != 17)
			{
				if (length != 25)
				{
					if (length != 26 || !(text == "MarkAsDateTableDefinitions"))
					{
						goto IL_01bb;
					}
					flag = request.MarkAsDateTableDefinitions?.Any() ?? false;
				}
				else
				{
					if (!(text == "FieldParameterDefinitions"))
					{
						goto IL_01bb;
					}
					flag = request.FieldParameterDefinitions?.Any() ?? false;
				}
			}
			else
			{
				char c = text[2];
				if (c != 'd')
				{
					if (c != 'n')
					{
						if (c != 's' || !(text == "TmslExportOptions"))
						{
							goto IL_01bb;
						}
						flag = request.TmslExportOptions != null;
					}
					else
					{
						if (!(text == "RenameDefinitions"))
						{
							goto IL_01bb;
						}
						flag = request.RenameDefinitions?.Any() ?? false;
					}
				}
				else
				{
					if (!(text == "TmdlExportOptions"))
					{
						goto IL_01bb;
					}
					flag = request.TmdlExportOptions != null;
				}
			}
			goto IL_01be;
			IL_01be:
			if (!flag)
			{
				list.Add(text);
			}
			num++;
			continue;
			IL_01bb:
			flag = true;
			goto IL_01be;
		}
		requiredParams = value.ForbiddenParams;
		foreach (string text2 in requiredParams)
		{
			if (text2 switch
			{
				"Definitions" => request.Definitions?.Any() ?? false, 
				"References" => request.References?.Any() ?? false, 
				"RenameDefinitions" => request.RenameDefinitions?.Any() ?? false, 
				"TmdlExportOptions" => request.TmdlExportOptions != null, 
				"TmslExportOptions" => request.TmslExportOptions != null, 
				"MarkAsDateTableDefinitions" => request.MarkAsDateTableDefinitions?.Any() ?? false, 
				_ => false, 
			})
			{
				list2.Add(text2);
			}
		}
		if (list.Any())
		{
			return (isValid: false, errorMessage: "Missing required parameters needed for " + operation + " operation: " + string.Join(", ", list));
		}
		if (list2.Any())
		{
			return (isValid: false, errorMessage: "Forbidden parameters not allowed for " + operation + " operation: " + string.Join(", ", list2));
		}
		return (isValid: true, errorMessage: null);
	}
}
