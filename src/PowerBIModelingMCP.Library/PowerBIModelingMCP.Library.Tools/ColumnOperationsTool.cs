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
public class ColumnOperationsTool
{
	public const string ToolName = "column_operations";

	private readonly ILogger<ColumnOperationsTool> _logger;

	public static readonly ToolMetadata toolMetadata = new ToolMetadata
	{
		Operations = new Dictionary<string, OperationMetadata>(StringComparer.OrdinalIgnoreCase)
		{
			["Create"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Definitions" },
				Description = "Create one or more columns. \nMandatory properties: Definitions (list of ColumnDefinition objects with Name, TableName, and either Expression or SourceColumn). \nOptional: DataType, DataCategory, FormatString, SummarizeBy, DefaultLabel, DefaultImage, IsHidden, IsUnique, IsKey, IsNullable, DisplayFolder, SortByColumn, SourceProviderType, Description, IsAvailableInMDX, Alignment, TableDetailPosition, Annotations, ExtendedProperties, AlternateOf, GroupByColumns, Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            { \n                \"Name\": \"Order Year\", \n                \"TableName\": \"Sales\", \n                \"Expression\": \"YEAR([Order Date])\" \n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            { \n                \"Name\": \"Order Year\", \n                \"TableName\": \"Sales\", \n                \"Expression\": \"YEAR([Order Date])\" \n            },\n            { \n                \"Name\": \"Order Month\", \n                \"TableName\": \"Sales\", \n                \"Expression\": \"FORMAT([Order Date], \\\"MMM\\\")\" \n            }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": false,\n            \"UseTransaction\": true\n        }\n    }\n}" }
			},
			["Update"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Definitions" },
				Description = "Update one or more existing columns. Names cannot be changed and must use the Rename operation instead. \nMandatory properties: Definitions (list of ColumnDefinition objects with Name, TableName). \nOptional: Expression, SourceColumn, DataType, DataCategory, FormatString, SummarizeBy, DefaultLabel, DefaultImage, IsHidden, IsUnique, IsKey, IsNullable, DisplayFolder, SortByColumn, SourceProviderType, Description, IsAvailableInMDX, Alignment, TableDetailPosition, Annotations, ExtendedProperties, AlternateOf, GroupByColumns, Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Update\",\n        \"Definitions\": [\n            { \n                \"Name\": \"Order Year\", \n                \"TableName\": \"Sales\", \n                \"Description\": \"The year the order was placed\" \n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Update\",\n        \"Definitions\": [\n            { \n                \"Name\": \"Order Year\", \n                \"TableName\": \"Sales\", \n                \"IsHidden\": false \n            },\n            { \n                \"Name\": \"Order Month\", \n                \"TableName\": \"Sales\", \n                \"IsHidden\": false \n            }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": true,\n            \"UseTransaction\": true\n        }\n    }\n}" }
			},
			["Delete"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Delete one or more columns from tables. \nMandatory properties: References (list of ColumnReference objects with TableName, Name). \nOptional: ShouldCascadeDelete, Options (ContinueOnError, UseTransaction).\nNote: Cannot delete the last column from non-calculated tables (MExpression, SqlQuery, EntityName) - at least one column must remain.",
				CommonMistakes = new string[1] { "Attempting to delete the last column from a non-calculated table (at least one column must remain)" },
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Delete\",\n        \"References\": [\n            {\n                \"TableName\": \"Sales\",\n                \"Name\": \"ObsoleteColumn\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Delete\",\n        \"References\": [\n            {\n                \"TableName\": \"Sales\",\n                \"Name\": \"ObsoleteColumn1\"\n            },\n            {\n                \"TableName\": \"Sales\",\n                \"Name\": \"ObsoleteColumn2\"\n            }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": true,\n            \"UseTransaction\": true\n        }\n    }\n}" }
			},
			["Get"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Get details of one or more columns. \nMandatory properties: References (list of ColumnReference objects with TableName, Name). \nOptional: Options (ContinueOnError).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Get\",\n        \"References\": [\n            {\n                \"TableName\": \"Sales\",\n                \"Name\": \"Region\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Get\",\n        \"References\": [\n            {\n                \"TableName\": \"Sales\",\n                \"Name\": \"Region\"\n            },\n            {\n                \"TableName\": \"Sales\",\n                \"Name\": \"Country\"\n            }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": true\n        }\n    }\n}" }
			},
			["List"] = new OperationMetadata
			{
				RequiredParams = Array.Empty<string>(),
				Description = "List all columns in specified tables, or all columns in all tables if no filter specified. \nMandatory properties: None. \nOptional: Filter (with TableNames and/or DisplayFolders arrays to filter results), MaxResults.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"List\",\n        \"Filter\": { \"TableNames\": [\"Sales\"] }\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"List\"\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"List\",\n        \"Filter\": { \"TableNames\": [\"Sales\", \"Products\"], \"DisplayFolders\": [\"Dimensions\", \"Attributes\"] }\n    }\n}" }
			},
			["Rename"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "RenameDefinitions" },
				Description = "Rename one or more columns. \nMandatory properties: RenameDefinitions (list of ColumnRename objects with TableName, CurrentName, NewName). \nOptional: Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Rename\",\n        \"RenameDefinitions\": [\n            { \n                \"TableName\": \"Sales\",\n                \"CurrentName\": \"OldColumnName\", \n                \"NewName\": \"NewColumnName\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Rename\",\n        \"RenameDefinitions\": [\n            { \n                \"TableName\": \"Sales\",\n                \"CurrentName\": \"OldColumn1\", \n                \"NewName\": \"NewColumn1\"\n            },\n            { \n                \"TableName\": \"Sales\",\n                \"CurrentName\": \"OldColumn2\", \n                \"NewName\": \"NewColumn2\"\n            }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": false,\n            \"UseTransaction\": true\n        }\n    }\n}" }
			},
			["ExportTMDL"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Export column to TMDL format. \nMandatory properties: References (single ColumnReference with TableName and Name). \nOptional: TmdlExportOptions (TmdlSerializationOptions.IncludeChildren defaults to false).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ExportTMDL\",\n        \"TmdlExportOptions\": {\n            \"TmdlSerializationOptions\": {\n                \"IncludeChildren\": true\n            }\n        },\n        \"References\": [{ \"TableName\": \"Sales\", \"Name\": \"Region\" }]\n    }\n}" }
			},
			["Help"] = new OperationMetadata
			{
				Description = "Describe the tool and its operations. \nMandatory properties: None. \nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Help\"\n    }\n}" }
			}
		}
	};

	public ColumnOperationsTool(ILogger<ColumnOperationsTool> logger)
	{
		_logger = logger;
	}

	[McpServerTool(Name = "column_operations", Title = "Column Operations", ReadOnly = false, Destructive = true)]
	[YamlToolDescription("column_operations")]
	public async Task<CallToolResult> ExecuteColumnOperation(McpServer mcpServer, ColumnOperationRequest request, IWriteGuard writeGuard)
	{
		_logger.LogDebug("Executing {ToolName}.{Operation}: Connection={ConnectionName}", "ColumnOperationsTool", request.Operation, request.ConnectionName ?? "(last used)");
		string[] validOperations = new string[8] { "CREATE", "UPDATE", "DELETE", "GET", "LIST", "RENAME", "EXPORTTMDL", "HELP" };
		string[] writeOperations = new string[4] { "CREATE", "UPDATE", "DELETE", "RENAME" };
		string op = request.Operation.ToUpperInvariant();
		ToolCallAnnotations annotations = ToolCallAnnotations.Create("column_operations", request.Operation, !Enumerable.Contains(writeOperations, op));
		CallToolResult result = null;
		try
		{
			if (!Enumerable.Contains(validOperations, op))
			{
				_logger.LogWarning("Invalid operation '{Operation}' requested for {ToolName}. Valid operations: {ValidOperations}", request.Operation, "ColumnOperationsTool", string.Join(", ", validOperations));
				CallToolResult result2;
				result = (result2 = CallToolResultHelper.FromResponse(ColumnOperationResponse.Forbidden(request.Operation, "Invalid operation: " + request.Operation + ". Supported operations: " + string.Join(", ", validOperations)), annotations));
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
			if (Enumerable.Contains(writeOperations, op))
			{
				WriteOperationResult writeOperationResult = await writeGuard.ExecuteWriteOperationWithGuards(mcpServer, request.ConnectionName, request.Operation);
				if (!writeOperationResult.Success)
				{
					_logger.LogWarning("{ToolName}.{Operation} blocked by write guard: {Reason}", "ColumnOperationsTool", request.Operation, writeOperationResult.Message);
					CallToolResult result2;
					result = (result2 = CallToolResultHelper.FromResponse(ColumnOperationResponse.Forbidden(request.Operation, writeOperationResult.Message), annotations));
					return result2;
				}
			}
			bool isWriteEnabled = writeGuard.IsWriteEnabled;
			CallToolResult result3;
			result = (result3 = op switch
			{
				"CREATE" => CallToolResultHelper.FromResponse(await HandleCreateOperation(request), annotations, null, minimalSuccessPayload: true), 
				"UPDATE" => CallToolResultHelper.FromResponse(await HandleUpdateOperation(request), annotations, null, minimalSuccessPayload: true), 
				"DELETE" => CallToolResultHelper.FromResponse(await HandleDeleteOperation(request), annotations, null, minimalSuccessPayload: true), 
				"GET" => CallToolResultHelper.FromResponse(await HandleGetOperation(request), annotations), 
				"LIST" => CallToolResultHelper.FromResponse(await HandleListOperation(request), annotations), 
				"RENAME" => CallToolResultHelper.FromResponse(await HandleRenameOperation(request), annotations, null, minimalSuccessPayload: true), 
				"EXPORTTMDL" => CallToolResultHelper.FromExportResponse(await HandleExportTMDLOperation(request), (request.References?.FirstOrDefault()?.Name ?? "column") + ".tmdl", "text/plain", annotations), 
				"HELP" => CallToolResultHelper.FromResponse(HandleHelpOperation(request, isWriteEnabled ? validOperations : validOperations.Except(writeOperations).ToArray()), annotations), 
				_ => CallToolResultHelper.FromResponse(ColumnOperationResponse.Forbidden(request.Operation, "Operation " + request.Operation + " is not implemented"), annotations), 
			});
			return result3;
		}
		catch (Exception ex)
		{
			_logger.LogOperationError("ColumnOperationsTool", request.Operation, ex);
			string message = op switch
			{
				"CREATE" => "Error executing Create operation: " + ex.GetErrorMessage(), 
				"UPDATE" => "Error executing Update operation: " + ex.GetErrorMessage(), 
				"DELETE" => "Error executing Delete operation: " + ex.GetErrorMessage(), 
				"GET" => "Error executing Get operation: " + ex.GetErrorMessage(), 
				"LIST" => "Error executing List operation: " + ex.GetErrorMessage(), 
				"RENAME" => "Error executing Rename operation: " + ex.GetErrorMessage(), 
				"EXPORTTMDL" => $"Error getting TMDL for column '{request.References?.FirstOrDefault()?.Name}' in table '{request.References?.FirstOrDefault()?.TableName}': {ex.GetErrorMessage()}", 
				_ => "Error executing column operation: " + ex.GetErrorMessage(), 
			};
			CallToolResult result2;
			result = (result2 = CallToolResultHelper.FromResponse(new ColumnOperationResponse
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

	private async Task<ColumnOperationResponse> HandleCreateOperation(ColumnOperationRequest request)
	{
		if (request.Definitions == null || !request.Definitions.Any())
		{
			return new ColumnOperationResponse
			{
				Success = false,
				Operation = "Create",
				Message = "Definitions is required and must contain at least one column definition"
			};
		}
		return MapBatchResponse(await ColumnOperations.CreateColumns(request.ConnectionName, request.Definitions, request.Options));
	}

	private async Task<ColumnOperationResponse> HandleUpdateOperation(ColumnOperationRequest request)
	{
		if (request.Definitions == null || !request.Definitions.Any())
		{
			return new ColumnOperationResponse
			{
				Success = false,
				Operation = "Update",
				Message = "Definitions is required and must contain at least one column definition"
			};
		}
		return MapBatchResponse(await ColumnOperations.UpdateColumns(request.ConnectionName, request.Definitions, request.Options));
	}

	private async Task<ColumnOperationResponse> HandleDeleteOperation(ColumnOperationRequest request)
	{
		if (request.References == null || !request.References.Any())
		{
			return new ColumnOperationResponse
			{
				Success = false,
				Operation = "Delete",
				Message = "References is required and must contain at least one column reference"
			};
		}
		return MapBatchResponse(await ColumnOperations.DeleteColumns(request.ConnectionName, request.References, request.ShouldCascadeDelete, request.Options));
	}

	private async Task<ColumnOperationResponse> HandleGetOperation(ColumnOperationRequest request)
	{
		if (request.References == null || !request.References.Any())
		{
			return new ColumnOperationResponse
			{
				Success = false,
				Operation = "Get",
				Message = "References is required and must contain at least one column reference"
			};
		}
		return MapBatchResponse(await ColumnOperations.GetColumns(request.ConnectionName, request.References, request.Options));
	}

	private async Task<ColumnOperationResponse> HandleListOperation(ColumnOperationRequest request)
	{
		List<string> tableNames = request.Filter?.TableNames?.Where((string t) => !string.IsNullOrWhiteSpace(t)).ToList();
		List<string> displayFolders = request.Filter?.DisplayFolders?.Where((string f) => !string.IsNullOrWhiteSpace(f)).ToList();
		int maxResults = request.Filter?.MaxResults ?? 200;
		var (list, num) = await ColumnOperations.ListColumns(request.ConnectionName, tableNames, maxResults);
		if (displayFolders != null && displayFolders.Any())
		{
			list = (from t in list
				select new TableColumnList
				{
					TableName = t.TableName,
					Columns = t.Columns.Where((ColumnList c) => displayFolders.Any((string df) => string.Equals(c.DisplayFolder, df, StringComparison.OrdinalIgnoreCase))).ToList()
				} into t
				where t.Columns.Any()
				select t).ToList();
		}
		int num2 = list.Sum((TableColumnList t) => t.Columns.Count);
		bool flag = num > maxResults;
		List<string> list2 = new List<string>();
		List<string> list3 = new List<string>();
		if (tableNames != null && tableNames.Any())
		{
			list3.Add((tableNames.Count == 1) ? ("table '" + tableNames[0] + "'") : ("tables [" + string.Join(", ", tableNames.Select((string t) => "'" + t + "'")) + "]"));
		}
		if (displayFolders != null && displayFolders.Any())
		{
			list3.Add((displayFolders.Count == 1) ? ("display folder '" + displayFolders[0] + "'") : ("display folders [" + string.Join(", ", displayFolders.Select((string f) => "'" + f + "'")) + "]"));
		}
		string message = ((!list3.Any()) ? $"Found {num2} columns across {list.Count} tables" : $"Found {num2} columns in {string.Join(" and ", list3)}");
		if (flag)
		{
			list2.Add($"Results truncated: Showing {num2} of {num} columns (limited by MaxResults={maxResults})");
		}
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, TotalCount={TotalCount}, ReturnedCount={Count}, IsTruncated={IsTruncated}", "ColumnOperationsTool", "List", request.ConnectionName, num, num2, flag);
		return new ColumnOperationResponse
		{
			Success = true,
			Message = message,
			Operation = request.Operation,
			Data = list,
			Warnings = (list2.Any() ? list2 : null)
		};
	}

	private async Task<ColumnOperationResponse> HandleRenameOperation(ColumnOperationRequest request)
	{
		if (request.RenameDefinitions == null || !request.RenameDefinitions.Any())
		{
			return new ColumnOperationResponse
			{
				Success = false,
				Operation = "Rename",
				Message = "RenameDefinitions is required and must contain at least one column rename definition"
			};
		}
		return MapBatchResponse(await ColumnOperations.RenameColumns(request.ConnectionName, request.RenameDefinitions, request.Options));
	}

	private async Task<ColumnOperationResponse> HandleExportTMDLOperation(ColumnOperationRequest request)
	{
		ExportValidationResult validation = ExportValidationHelper.ValidateReferences(request.References, "Column");
		if (!validation.IsValid)
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new ColumnOperationResponse
			{
				Success = false,
				Operation = "ExportTMDL",
				Message = validation.ErrorMessage,
				Help = value
			};
		}
		ColumnReference reference = request.References.First();
		ExportValidationResult exportValidationResult = ExportValidationHelper.ValidateTableScopedReference(reference.TableName, reference.Name, "Column");
		if (!exportValidationResult.IsValid)
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value2);
			return new ColumnOperationResponse
			{
				Success = false,
				Operation = "ExportTMDL",
				Message = exportValidationResult.ErrorMessage,
				Help = value2
			};
		}
		string data = await ColumnOperations.ExportTMDL(request.ConnectionName, reference.TableName, reference.Name, request.TmdlExportOptions ?? new ExportTmdl());
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "ColumnOperationsTool", "ExportTMDL", request.ConnectionName);
		string objectIdentifier = reference.TableName + "." + reference.Name;
		string message = ExportValidationHelper.FormatSuccessMessage("Column", objectIdentifier, validation.WarningMessage);
		List<string> warnings = ((!string.IsNullOrEmpty(validation.WarningMessage)) ? new List<string> { validation.WarningMessage } : null);
		return new ColumnOperationResponse
		{
			Success = true,
			Message = message,
			Operation = request.Operation,
			Data = data,
			Warnings = warnings
		};
	}

	private ColumnOperationResponse HandleHelpOperation(ColumnOperationRequest request, string[] operations)
	{
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Operations={OperationCount}", "ColumnOperationsTool", "Help", request.ConnectionName, operations.Length);
		ColumnOperationResponse columnOperationResponse = new ColumnOperationResponse();
		columnOperationResponse.Success = true;
		columnOperationResponse.Message = "Tool description retrieved successfully";
		columnOperationResponse.Operation = request.Operation;
		columnOperationResponse.Help = new
		{
			ToolName = "column_operations",
			Description = "Perform operations on semantic model columns.",
			SupportedOperations = operations,
			Examples = toolMetadata.Operations.Where<KeyValuePair<string, OperationMetadata>>((KeyValuePair<string, OperationMetadata> p) => operations.Contains<string>(p.Key, StringComparer.OrdinalIgnoreCase)),
			Notes = new string[6] { "Use the Operation parameter to specify which operation to perform.", "Operations accept lists of items for bulk processing.", "Single-item operations are represented as lists of one.", "Use Options.ContinueOnError to control error handling in batch operations.", "Use Options.UseTransaction to control transactional behavior.", "If the request is declined by the user, the operation should be aborted." }
		};
		return columnOperationResponse;
	}

	private ColumnOperationResponse MapBatchResponse(BatchOperationResponse batchResponse)
	{
		ColumnOperationResponse columnOperationResponse = new ColumnOperationResponse
		{
			Success = batchResponse.Success,
			Message = batchResponse.Message,
			Operation = batchResponse.Operation,
			Summary = batchResponse.Summary,
			Results = batchResponse.Results,
			Warnings = batchResponse.Warnings
		};
		if (batchResponse.Exceptions.Count > 0)
		{
			columnOperationResponse.Exceptions.AddRange(batchResponse.Exceptions);
		}
		return columnOperationResponse;
	}

	private (bool isValid, string? errorMessage) ValidateRequest(string operation, ColumnOperationRequest request)
	{
		switch (operation.ToUpperInvariant())
		{
		case "CREATE":
			if (request.Definitions == null || !request.Definitions.Any())
			{
				return (isValid: false, errorMessage: "Definitions is required for Create operation");
			}
			break;
		case "UPDATE":
			if (request.Definitions == null || !request.Definitions.Any())
			{
				return (isValid: false, errorMessage: "Definitions is required for Update operation");
			}
			break;
		case "DELETE":
			if (request.References == null || !request.References.Any())
			{
				return (isValid: false, errorMessage: "References is required for Delete operation");
			}
			break;
		case "GET":
			if (request.References == null || !request.References.Any())
			{
				return (isValid: false, errorMessage: "References is required for Get operation");
			}
			break;
		case "RENAME":
			if (request.RenameDefinitions == null || !request.RenameDefinitions.Any())
			{
				return (isValid: false, errorMessage: "RenameDefinitions is required for Rename operation");
			}
			break;
		case "EXPORTTMDL":
		{
			ColumnReference columnReference = request.References?.FirstOrDefault();
			if (columnReference == null || string.IsNullOrEmpty(columnReference.TableName) || string.IsNullOrEmpty(columnReference.Name))
			{
				return (isValid: false, errorMessage: "References with TableName and Name is required for ExportTMDL operation");
			}
			break;
		}
		}
		return (isValid: true, errorMessage: null);
	}
}
