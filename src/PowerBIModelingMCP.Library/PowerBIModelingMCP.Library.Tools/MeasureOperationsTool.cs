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
public class MeasureOperationsTool
{
	public const string ToolName = "measure_operations";

	private readonly ILogger<MeasureOperationsTool> _logger;

	private readonly IWriteGuard _writeGuard;

	public static readonly ToolMetadata toolMetadata = new ToolMetadata
	{
		Operations = new Dictionary<string, OperationMetadata>(StringComparer.OrdinalIgnoreCase)
		{
			["Create"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Definitions" },
				Description = "Create one or more measures.\nMandatory properties: Definitions (list of MeasureDefinition objects with Name, Expression, TableName).\nOptional: Description, FormatString, IsHidden, IsSimpleMeasure, DisplayFolder, DataType, DataCategory, LineageTag, SourceLineageTag, KPI, DetailRowsExpression, FormatStringExpression, Annotations, ExtendedProperties, Options (ContinueOnError, UseTransaction).",
				CommonMistakes = new string[1] { "Forgetting to supply the host table of the measure to be created in Definitions[].TableName" },
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            { \n                \"Name\": \"TotalSales\", \n                \"TableName\": \"Sales\", \n                \"Expression\": \"SUM([SalesAmount])\" \n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            { \n                \"Name\": \"TotalSales\", \n                \"TableName\": \"Sales\", \n                \"Expression\": \"SUM([SalesAmount])\" \n            },\n            {\n                \"Name\": \"AverageSales\",\n                \"TableName\": \"Sales\",\n                \"Expression\": \"AVERAGE([SalesAmount])\",\n                \"FormatString\": \"$#,##0.00\"\n            }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": false,\n            \"UseTransaction\": true\n        }\n    }\n}" }
			},
			["Update"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Definitions" },
				Description = "Update properties, except for name or table, of one or more existing measures.\nMandatory properties: Definitions (list of MeasureDefinition objects with Name, TableName).\nOptional: Expression, Description, FormatString, IsHidden, IsSimpleMeasure, DisplayFolder, DataType, DataCategory, LineageTag, SourceLineageTag, KPI, DetailRowsExpression, FormatStringExpression, Annotations, ExtendedProperties, Options (ContinueOnError, UseTransaction).",
				CommonMistakes = new string[1] { "Cannot change tableName via Update - use Move operation instead" },
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Update\",\n        \"Definitions\": [\n            { \n                \"Name\": \"TotalSales\", \n                \"TableName\": \"Sales\", \n                \"Description\": \"Sum of all sales amounts\",\n                \"FormatString\": \"$#,##0.00\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Update\",\n        \"Definitions\": [\n            { \n                \"Name\": \"TotalSales\", \n                \"TableName\": \"Sales\", \n                \"IsHidden\": false\n            },\n            {\n                \"Name\": \"AverageSales\",\n                \"TableName\": \"Sales\",\n                \"IsHidden\": false,\n                \"DisplayFolder\": \"Sales Metrics\"\n            }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": true,\n            \"UseTransaction\": true\n        }\n    }\n}" }
			},
			["Delete"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Delete one or more measures.\nMandatory properties: References (list of MeasureReference objects with Name, TableName).\nOptional: ShouldCascadeDelete (applies to all items, default: true), Options (ContinueOnError, UseTransaction).",
				Tips = new string[1] { "Use ShouldCascadeDelete to delete dependencies of the measures" },
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Delete\",\n        \"References\": [\n            {\n                \"Name\": \"ObsoleteMeasure\",\n                \"TableName\": \"Sales\"\n            }\n        ],\n        \"ShouldCascadeDelete\": true\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Delete\",\n        \"References\": [\n            {\n                \"Name\": \"ObsoleteMeasure1\",\n                \"TableName\": \"Sales\"\n            },\n            {\n                \"Name\": \"ObsoleteMeasure2\",\n                \"TableName\": \"Sales\"\n            }\n        ],\n        \"ShouldCascadeDelete\": false,\n        \"Options\": {\n            \"ContinueOnError\": true,\n            \"UseTransaction\": true\n        }\n    }\n}" }
			},
			["Get"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Get details of one or more measures.\nMandatory properties: References (list of MeasureReference objects with Name, TableName).\nOptional: Options (ContinueOnError).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Get\",\n        \"References\": [\n            {\n                \"Name\": \"TotalSales\",\n                \"TableName\": \"Sales\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Get\",\n        \"References\": [\n            {\n                \"Name\": \"TotalSales\",\n                \"TableName\": \"Sales\"\n            },\n            {\n                \"Name\": \"AverageSales\",\n                \"TableName\": \"Sales\"\n            }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": true\n        }\n    }\n}" }
			},
			["List"] = new OperationMetadata
			{
				Description = "List all measures in specified tables, or all measures across tables if no filter specified.\nMandatory properties: None.\nOptional: Filter (with TableNames and/or DisplayFolders arrays to filter results), MaxResults.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"List\",\n        \"Filter\": { \"TableNames\": [\"Sales\"] }\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"List\",\n        \"Filter\": { \"DisplayFolders\": [\"Key Metrics\", \"Sales Metrics\"] }\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"List\",\n        \"Filter\": { \"TableNames\": [\"Sales\", \"Products\"], \"DisplayFolders\": [\"Key Metrics\"] }\n    }\n}" }
			},
			["Rename"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "RenameDefinitions" },
				Description = "Rename one or more measures.\nMandatory properties: RenameDefinitions (list of MeasureRename objects with CurrentName, NewName, TableName).\nOptional: Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Rename\",\n        \"RenameDefinitions\": [\n            { \n                \"CurrentName\": \"OldMeasure\", \n                \"NewName\": \"NewMeasure\",\n                \"TableName\": \"Sales\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Rename\",\n        \"RenameDefinitions\": [\n            { \n                \"CurrentName\": \"OldMeasure1\", \n                \"NewName\": \"NewMeasure1\",\n                \"TableName\": \"Sales\"\n            },\n            {\n                \"CurrentName\": \"OldMeasure2\",\n                \"NewName\": \"NewMeasure2\",\n                \"TableName\": \"Sales\"\n            }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": false,\n            \"UseTransaction\": true\n        }\n    }\n}" }
			},
			["Move"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "MoveDefinitions" },
				ForbiddenParams = new string[1] { "Definitions" },
				Description = "Move one or more measures to a different table.\nMandatory properties: MoveDefinitions (list of MeasureMove objects with Name, DestinationTableName, CurrentTableName).\nOptional: Options (ContinueOnError, UseTransaction).",
				CommonMistakes = new string[2] { "Don't use Definitions[].TableName - use MoveDefinitions[].DestinationTableName", "Don't use delete and recreate - Move operation handles the transfer" },
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Move\",\n        \"MoveDefinitions\": [\n            {\n                \"Name\": \"MyMeasure\",\n                \"DestinationTableName\": \"NewTable\",\n                \"CurrentTableName\": \"OldTable\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Move\",\n        \"MoveDefinitions\": [\n            {\n                \"Name\": \"Measure1\",\n                \"DestinationTableName\": \"TargetTable\",\n                \"CurrentTableName\": \"SourceTable\"\n            },\n            {\n                \"Name\": \"Measure2\",\n                \"DestinationTableName\": \"TargetTable\",\n                \"CurrentTableName\": \"SourceTable\"\n            }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": false,\n            \"UseTransaction\": true\n        }\n    }\n}" },
				Tips = new string[1] { "Use this instead of Delete and Create for better performance" }
			},
			["ExportTMDL"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Export measure to TMDL format.\nMandatory properties: References (single item with Name).\nOptional: TmdlExportOptions (TmdlSerializationOptions.IncludeChildren defaults to false).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ExportTMDL\",\n        \"TmdlExportOptions\": {\n            \"TmdlSerializationOptions\": {\n                \"IncludeChildren\": true\n            }\n        },\n        \"References\": [\n            {\n                \"Name\": \"TotalSales\"\n            }\n        ]\n    }\n}" }
			},
			["Help"] = new OperationMetadata
			{
				Description = "Describe the tool and its operations.\nMandatory properties: None.\nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Help\"\n    }\n}" }
			}
		}
	};

	public MeasureOperationsTool(ILogger<MeasureOperationsTool> logger, IWriteGuard writeGuard)
	{
		_logger = logger;
		_writeGuard = writeGuard;
	}

	[McpServerTool(Name = "measure_operations", Title = "Measure Operations", ReadOnly = false, Destructive = true)]
	[YamlToolDescription("measure_operations")]
	public async Task<CallToolResult> ExecuteMeasureOperation(McpServer mcpServer, MeasureOperationRequest request)
	{
		_logger.LogDebug("Executing {ToolName}.{Operation}: Connection={ConnectionName}", "MeasureOperationsTool", request.Operation, request.ConnectionName ?? "(last used)");
		string[] validOperations = new string[9] { "CREATE", "UPDATE", "DELETE", "GET", "LIST", "RENAME", "MOVE", "EXPORTTMDL", "HELP" };
		string[] writeOperations = new string[5] { "CREATE", "UPDATE", "DELETE", "RENAME", "MOVE" };
		string op = request.Operation.ToUpperInvariant();
		ToolCallAnnotations annotations = ToolCallAnnotations.Create("measure_operations", request.Operation, !Enumerable.Contains(writeOperations, op));
		CallToolResult result = null;
		try
		{
			if (!Enumerable.Contains(validOperations, op))
			{
				_logger.LogWarning("Invalid operation '{Operation}' requested for {ToolName}. Valid operations: {ValidOperations}", request.Operation, "MeasureOperationsTool", string.Join(", ", validOperations));
				CallToolResult result2;
				result = (result2 = CallToolResultHelper.FromResponse(MeasureOperationResponse.Forbidden(request.Operation, "Invalid operation: " + request.Operation + ". Supported operations: " + string.Join(", ", validOperations)), annotations));
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
				WriteOperationResult writeOperationResult = await _writeGuard.ExecuteWriteOperationWithGuards(mcpServer, request.ConnectionName, request.Operation);
				if (!writeOperationResult.Success)
				{
					_logger.LogWarning("{ToolName}.{Operation} blocked by write guard: {Reason}", "MeasureOperationsTool", request.Operation, writeOperationResult.Message);
					CallToolResult result2;
					result = (result2 = CallToolResultHelper.FromResponse(new MeasureOperationResponse
					{
						Success = false,
						Warnings = writeOperationResult.Warnings,
						Message = writeOperationResult.Message,
						Operation = request.Operation
					}, annotations));
					return result2;
				}
			}
			bool isWriteEnabled = _writeGuard.IsWriteEnabled;
			CallToolResult result3;
			result = (result3 = op switch
			{
				"CREATE" => CallToolResultHelper.FromResponse(await HandleCreateOperation(request), annotations, null, minimalSuccessPayload: true), 
				"UPDATE" => CallToolResultHelper.FromResponse(await HandleUpdateOperation(request), annotations, null, minimalSuccessPayload: true), 
				"DELETE" => CallToolResultHelper.FromResponse(await HandleDeleteOperation(request), annotations, null, minimalSuccessPayload: true), 
				"GET" => CallToolResultHelper.FromResponse(await HandleGetOperation(request), annotations), 
				"LIST" => CallToolResultHelper.FromResponse(await HandleListOperation(request), annotations), 
				"RENAME" => CallToolResultHelper.FromResponse(await HandleRenameOperation(request), annotations, null, minimalSuccessPayload: true), 
				"MOVE" => CallToolResultHelper.FromResponse(await HandleMoveOperation(request), annotations, null, minimalSuccessPayload: true), 
				"EXPORTTMDL" => CallToolResultHelper.FromExportResponse(await HandleExportTMDLOperation(request), (request.References?.FirstOrDefault()?.Name ?? "measure") + ".tmdl", "text/plain", annotations), 
				"HELP" => CallToolResultHelper.FromResponse(HandleHelpOperation(request, isWriteEnabled ? validOperations : validOperations.Except(writeOperations).ToArray()), annotations), 
				_ => CallToolResultHelper.FromResponse(MeasureOperationResponse.Forbidden(request.Operation, "Operation " + request.Operation + " is not implemented"), annotations), 
			});
			return result3;
		}
		catch (Exception ex)
		{
			_logger.LogOperationError("MeasureOperationsTool", request.Operation, ex);
			string message = op switch
			{
				"CREATE" => "Error executing Create operation: " + ex.Message, 
				"UPDATE" => "Error executing Update operation: " + ex.GetErrorMessage(), 
				"DELETE" => "Error executing Delete operation: " + ex.GetErrorMessage(), 
				"GET" => "Error executing Get operation: " + ex.GetErrorMessage(), 
				"LIST" => "Error executing List operation: " + ex.GetErrorMessage(), 
				"RENAME" => "Error executing Rename operation: " + ex.GetErrorMessage(), 
				"MOVE" => "Error executing Move operation: " + ex.GetErrorMessage(), 
				"EXPORTTMDL" => "Failed to export TMDL for measure '" + (request.References?.FirstOrDefault()?.Name ?? "(unknown)") + "': " + ex.GetErrorMessage(), 
				_ => "Error executing measure operation: " + ex.GetErrorMessage(), 
			};
			CallToolResult result2;
			result = (result2 = CallToolResultHelper.FromResponse(new MeasureOperationResponse
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

	private async Task<MeasureOperationResponse> HandleCreateOperation(MeasureOperationRequest request)
	{
		if (request.Definitions == null || !request.Definitions.Any())
		{
			return new MeasureOperationResponse
			{
				Success = false,
				Operation = "Create",
				Message = "Definitions is required and must contain at least one measure definition"
			};
		}
		return MapBatchResponse(await MeasureOperations.CreateMeasures(request.ConnectionName, request.Definitions, request.Options));
	}

	private async Task<MeasureOperationResponse> HandleUpdateOperation(MeasureOperationRequest request)
	{
		if (request.Definitions == null || !request.Definitions.Any())
		{
			return new MeasureOperationResponse
			{
				Success = false,
				Operation = "Update",
				Message = "Definitions is required and must contain at least one measure definition"
			};
		}
		return MapBatchResponse(await MeasureOperations.UpdateMeasures(request.ConnectionName, request.Definitions, request.Options));
	}

	private async Task<MeasureOperationResponse> HandleDeleteOperation(MeasureOperationRequest request)
	{
		if (request.References == null || !request.References.Any())
		{
			return new MeasureOperationResponse
			{
				Success = false,
				Operation = "Delete",
				Message = "References is required and must contain at least one measure reference"
			};
		}
		return MapBatchResponse(await MeasureOperations.DeleteMeasures(request.ConnectionName, request.References, request.ShouldCascadeDelete, request.Options));
	}

	private async Task<MeasureOperationResponse> HandleGetOperation(MeasureOperationRequest request)
	{
		if (request.References == null || !request.References.Any())
		{
			return new MeasureOperationResponse
			{
				Success = false,
				Operation = "Get",
				Message = "References is required and must contain at least one measure reference"
			};
		}
		return MapBatchResponse(await MeasureOperations.GetMeasures(request.ConnectionName, request.References, request.Options));
	}

	private async Task<MeasureOperationResponse> HandleListOperation(MeasureOperationRequest request)
	{
		List<string> tableNames = request.Filter?.TableNames?.Where((string t) => !string.IsNullOrWhiteSpace(t)).ToList();
		List<string> displayFolders = request.Filter?.DisplayFolders?.Where((string f) => !string.IsNullOrWhiteSpace(f)).ToList();
		int maxResults = request.Filter?.MaxResults ?? 200;
		var (list, num) = await MeasureOperations.ListMeasures(request.ConnectionName, tableNames, maxResults);
		if (displayFolders != null && displayFolders.Any())
		{
			list = (from t in list
				select new TableMeasureList
				{
					TableName = t.TableName,
					Measures = t.Measures.Where((MeasureList m) => displayFolders.Any((string df) => string.Equals(m.DisplayFolder, df, StringComparison.OrdinalIgnoreCase))).ToList()
				} into t
				where t.Measures.Any()
				select t).ToList();
		}
		int num2 = list.Sum((TableMeasureList t) => t.Measures.Count);
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
		string message = ((!list3.Any()) ? $"Found {num2} measures across {list.Count} tables" : $"Found {num2} measures in {string.Join(" and ", list3)}");
		if (flag)
		{
			list2.Add($"Results truncated: Showing {num2} of {num} measures (limited by MaxResults={maxResults})");
		}
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, TotalCount={TotalCount}, ReturnedCount={Count}, IsTruncated={IsTruncated}", "MeasureOperationsTool", request.Operation, request.ConnectionName, num, num2, flag);
		return new MeasureOperationResponse
		{
			Success = true,
			Message = message,
			Operation = request.Operation,
			Data = list,
			Warnings = (list2.Any() ? list2 : null)
		};
	}

	private async Task<MeasureOperationResponse> HandleRenameOperation(MeasureOperationRequest request)
	{
		if (request.RenameDefinitions == null || !request.RenameDefinitions.Any())
		{
			return new MeasureOperationResponse
			{
				Success = false,
				Operation = "Rename",
				Message = "RenameDefinitions is required and must contain at least one measure rename definition"
			};
		}
		return MapBatchResponse(await MeasureOperations.RenameMeasures(request.ConnectionName, request.RenameDefinitions, request.Options));
	}

	private async Task<MeasureOperationResponse> HandleMoveOperation(MeasureOperationRequest request)
	{
		if (request.MoveDefinitions == null || !request.MoveDefinitions.Any())
		{
			return new MeasureOperationResponse
			{
				Success = false,
				Operation = "Move",
				Message = "MoveDefinitions is required and must contain at least one measure move definition"
			};
		}
		return MapBatchResponse(await MeasureOperations.MoveMeasures(request.ConnectionName, request.MoveDefinitions, request.Options));
	}

	private async Task<MeasureOperationResponse> HandleExportTMDLOperation(MeasureOperationRequest request)
	{
		ExportValidationResult validation = ExportValidationHelper.ValidateReferences(request.References, "Measure");
		if (!validation.IsValid)
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new MeasureOperationResponse
			{
				Success = false,
				Operation = "ExportTMDL",
				Message = validation.ErrorMessage,
				Help = value
			};
		}
		MeasureReference measureReference = request.References.First();
		ExportValidationResult exportValidationResult = ExportValidationHelper.ValidateName(measureReference.Name, "Measure");
		if (!exportValidationResult.IsValid)
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value2);
			return new MeasureOperationResponse
			{
				Success = false,
				Operation = "ExportTMDL",
				Message = exportValidationResult.ErrorMessage,
				Help = value2
			};
		}
		string measureName = measureReference.Name;
		string data = await MeasureOperations.ExportTMDL(request.ConnectionName, measureName, request.TmdlExportOptions ?? new ExportTmdl());
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "MeasureOperationsTool", request.Operation, request.ConnectionName);
		string message = ExportValidationHelper.FormatSuccessMessage("Measure", measureName, validation.WarningMessage);
		List<string> warnings = ((!string.IsNullOrEmpty(validation.WarningMessage)) ? new List<string> { validation.WarningMessage } : null);
		return new MeasureOperationResponse
		{
			Success = true,
			Message = message,
			Operation = request.Operation,
			Data = data,
			Warnings = warnings
		};
	}

	private MeasureOperationResponse HandleHelpOperation(MeasureOperationRequest request, string[] operations)
	{
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Operations={OperationCount}", "MeasureOperationsTool", request.Operation, request.ConnectionName, operations.Length);
		MeasureOperationResponse measureOperationResponse = new MeasureOperationResponse();
		measureOperationResponse.Success = true;
		measureOperationResponse.Message = "Tool description retrieved successfully";
		measureOperationResponse.Operation = request.Operation;
		measureOperationResponse.Help = new
		{
			ToolName = "measure_operations",
			Description = "Perform operations on semantic model measures.",
			SupportedOperations = operations,
			Examples = toolMetadata.Operations.Where<KeyValuePair<string, OperationMetadata>>((KeyValuePair<string, OperationMetadata> p) => operations.Contains<string>(p.Key, StringComparer.OrdinalIgnoreCase)),
			Notes = new string[6] { "Use the Operation parameter to specify which operation to perform.", "Operations accept lists of items for bulk processing.", "Single-item operations are represented as lists of one.", "Use Options.ContinueOnError to control error handling in batch operations.", "Use Options.UseTransaction to control transactional behavior.", "If the request is declined by the user, the operation should be aborted." }
		};
		return measureOperationResponse;
	}

	private MeasureOperationResponse MapBatchResponse(BatchOperationResponse batchResponse)
	{
		MeasureOperationResponse measureOperationResponse = new MeasureOperationResponse
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
			measureOperationResponse.Exceptions.AddRange(batchResponse.Exceptions);
		}
		return measureOperationResponse;
	}

	private (bool isValid, string? errorMessage) ValidateRequest(string operation, MeasureOperationRequest request)
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
		case "MOVE":
			if (request.MoveDefinitions == null || !request.MoveDefinitions.Any())
			{
				return (isValid: false, errorMessage: "MoveDefinitions is required for Move operation");
			}
			break;
		case "EXPORTTMDL":
			if (request.References == null || !request.References.Any() || string.IsNullOrEmpty(request.References.First().Name))
			{
				return (isValid: false, errorMessage: "References with Name is required for ExportTMDL operation");
			}
			break;
		}
		return (isValid: true, errorMessage: null);
	}
}
