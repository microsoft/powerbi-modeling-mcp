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
public class FunctionOperationsTool
{
	public const string ToolName = "function_operations";

	private readonly ILogger<FunctionOperationsTool> _logger;

	public static readonly ToolMetadata toolMetadata = new ToolMetadata
	{
		Operations = new Dictionary<string, OperationMetadata>(StringComparer.OrdinalIgnoreCase)
		{
			["Create"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Definitions" },
				Description = "Creates one or more user-defined DAX functions in the semantic model.\nMandatory properties: Definitions (list of FunctionDefinition objects with Name, Expression).\nOptional per function: Description, IsHidden, LineageTag, SourceLineageTag, Annotations, ExtendedProperties.\nOptional batch settings: Options (with UseTransaction, ContinueOnError).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            {\n                \"Name\": \"CircleArea\",\n                \"Expression\": \"(radius) => PI() * radius * radius\",\n                \"Description\": \"Calculates the area of a circle given its radius\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            {\n                \"Name\": \"DoubleValue\",\n                \"Expression\": \"(inputValue : Scalar Val) => inputValue * 2\",\n                \"Description\": \"Doubles the input value\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            {\n                \"Name\": \"Mode\",\n                \"Expression\": \"(tab : AnyRef, col : AnyRef) => MINX(TOPN(1, ADDCOLUMNS(VALUES(col), \\\"Freq\\\", CALCULATE(COUNTROWS(tab))), [Freq], DESC), col)\",\n                \"Description\": \"Finds the most frequently occurring value in the column\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            {\n                \"Name\": \"PriorYearValue\",\n                \"Expression\": \"(expression : Scalar Expr, dateColumn : AnyRef) => CALCULATE(expression, SAMEPERIODLASTYEAR(dateColumn))\",\n                \"Description\": \"Calculates the value of any scalar expression in the previous year using the specified date column\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            {\n                \"Name\": \"TodayAsDate\",\n                \"Expression\": \"() => TREATAS({ TODAY() }, 'Date'[Date])\",\n                \"Description\": \"Returns today's date as a table expression that can be used in filter contexts\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            {\n                \"Name\": \"Top3ProductsBySales\",\n                \"Expression\": \"() => TOPN(3, VALUES('Product'[ProductKey]), [Sales], DESC)\",\n                \"Description\": \"Returns the top 3 products by Sales amount\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            {\n                \"Name\": \"SplitString\",\n                \"Expression\": \"(s : String, delimiter : String) => VAR str = SUBSTITUTE(s, delimiter, \\\"|\\\") VAR len = PATHLENGTH(str) RETURN SELECTCOLUMNS(GENERATESERIES(1, len), \\\"Value\\\", PATHITEM(str, [Value], TEXT))\",\n                \"Description\": \"Splits a string by a delimiter and returns a table with the split values\"\n            }\n        ]\n    }\n}" }
			},
			["Update"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Definitions" },
				Description = "Updates one or more existing user-defined DAX functions in the semantic model. Names cannot be changed, use Rename operation instead.\nMandatory properties: Definitions (list of FunctionDefinition objects with Name).\nOptional per function: Expression, Description, IsHidden, LineageTag, SourceLineageTag, Annotations, ExtendedProperties.\nOptional batch settings: Options (with UseTransaction, ContinueOnError).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Update\",\n        \"Definitions\": [\n            {\n                \"Name\": \"CircleArea\",\n                \"Expression\": \"(radius : SCALAR NUMERIC) => PI() * radius * radius\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Update\",\n        \"Definitions\": [\n            {\n                \"Name\": \"Mode\",\n                \"Expression\": \"(tab : AnyRef, col : AnyRef) => MINX(TOPN(1, ADDCOLUMNS(VALUES(col), \\\"Freq\\\", CALCULATE(COUNTROWS(tab))), [Freq], DESC, col, ASC), col)\"\n            }\n        ]\n    }\n}" }
			},
			["Delete"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Deletes one or more user-defined DAX functions from the semantic model.\nMandatory properties: References (list of FunctionReference objects with Name).\nOptional batch settings: Options (with UseTransaction, ContinueOnError).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Delete\",\n        \"References\": [\n            { \"Name\": \"MyFunction\" }\n        ]\n    }\n}" }
			},
			["Get"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Retrieves detailed information about one or more user-defined DAX functions.\nMandatory properties: References (list of FunctionReference objects with Name).\nOptional batch settings: Options (with ContinueOnError).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Get\",\n        \"References\": [\n            { \"Name\": \"MyFunction\" }\n        ]\n    }\n}" }
			},
			["List"] = new OperationMetadata
			{
				Description = "Lists all user-defined DAX functions in the semantic model with basic information.\nMandatory properties: None.\nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"List\"\n    }\n}" }
			},
			["Rename"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "RenameDefinitions" },
				Description = "Renames one or more user-defined DAX functions in the semantic model.\nMandatory properties: RenameDefinitions (list of FunctionRename objects with CurrentName, NewName).\nOptional batch settings: Options (with UseTransaction, ContinueOnError).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Rename\",\n        \"RenameDefinitions\": [\n            { \n                \"CurrentName\": \"OldFunction\", \n                \"NewName\": \"NewFunction\"\n            }\n        ]\n    }\n}" }
			},
			["ExportTMDL"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Exports a user-defined DAX function definition to TMDL format.\nMandatory properties: References (list with single FunctionReference with Name).\nOptional: TmdlExportOptions (TmdlSerializationOptions.IncludeChildren defaults to false).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ExportTMDL\",\n        \"TmdlExportOptions\": {\n            \"TmdlSerializationOptions\": {\n                \"IncludeChildren\": true\n            }\n        },\n        \"References\": [\n            { \"Name\": \"MyFunction\" }\n        ]\n    }\n}" }
			},
			["Help"] = new OperationMetadata
			{
				Description = "Provides detailed information about the function operations tool and its capabilities.\nMandatory properties: None.\nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Help\"\n    }\n}" }
			}
		}
	};

	public FunctionOperationsTool(ILogger<FunctionOperationsTool> logger)
	{
		_logger = logger;
	}

	[McpServerTool(Name = "function_operations", Title = "Function Operations", ReadOnly = false, Destructive = true)]
	[YamlToolDescription("function_operations")]
	public async Task<CallToolResult> ExecuteFunctionOperation(McpServer mcpServer, FunctionOperationRequest request, IWriteGuard writeGuard)
	{
		_logger.LogDebug("Executing {ToolName}.{Operation}: Connection={ConnectionName}", "FunctionOperationsTool", request.Operation, request.ConnectionName ?? "(last used)");
		string[] validOperations = new string[8] { "CREATE", "UPDATE", "DELETE", "GET", "LIST", "RENAME", "EXPORTTMDL", "HELP" };
		string[] writeOperations = new string[4] { "CREATE", "UPDATE", "DELETE", "RENAME" };
		string op = request.Operation.ToUpperInvariant();
		ToolCallAnnotations annotations = ToolCallAnnotations.Create("function_operations", request.Operation, !Enumerable.Contains(writeOperations, op));
		CallToolResult result = null;
		try
		{
			if (!Enumerable.Contains(validOperations, op))
			{
				_logger.LogWarning("Invalid operation '{Operation}' requested for {ToolName}. Valid operations: {ValidOperations}", request.Operation, "FunctionOperationsTool", string.Join(", ", validOperations));
				CallToolResult result2;
				result = (result2 = CallToolResultHelper.FromResponse(FunctionOperationResponse.Forbidden(request.Operation, "Invalid operation: " + request.Operation + ". Supported operations: " + string.Join(", ", validOperations)), annotations));
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
				WriteOperationResult writeOperationResult = await writeGuard.ExecuteWriteOperationWithGuards(mcpServer, request.ConnectionName, request.Operation);
				if (!writeOperationResult.Success)
				{
					_logger.LogWarning("{ToolName}.{Operation} blocked by write guard: {Reason}", "FunctionOperationsTool", request.Operation, writeOperationResult.Message);
					CallToolResult result2;
					result = (result2 = CallToolResultHelper.FromResponse(FunctionOperationResponse.Forbidden(request.Operation, writeOperationResult.Message), annotations));
					return result2;
				}
			}
			bool isWriteEnabled = writeGuard.IsWriteEnabled;
			CallToolResult result3;
			result = (result3 = request.Operation.ToUpperInvariant() switch
			{
				"CREATE" => CallToolResultHelper.FromResponse(await HandleCreateOperation(request), annotations, null, minimalSuccessPayload: true), 
				"UPDATE" => CallToolResultHelper.FromResponse(await HandleUpdateOperation(request), annotations, null, minimalSuccessPayload: true), 
				"DELETE" => CallToolResultHelper.FromResponse(await HandleDeleteOperation(request), annotations, null, minimalSuccessPayload: true), 
				"GET" => CallToolResultHelper.FromResponse(await HandleGetOperation(request), annotations), 
				"LIST" => CallToolResultHelper.FromResponse(await HandleListOperation(request), annotations), 
				"RENAME" => CallToolResultHelper.FromResponse(await HandleRenameOperation(request), annotations, null, minimalSuccessPayload: true), 
				"EXPORTTMDL" => CallToolResultHelper.FromExportResponse(await HandleExportTMDLOperation(request), (request.References?.FirstOrDefault()?.Name ?? "function") + ".tmdl", "text/plain", annotations), 
				"HELP" => CallToolResultHelper.FromResponse(HandleHelpOperation(request, isWriteEnabled ? validOperations : validOperations.Except(writeOperations).ToArray()), annotations), 
				_ => CallToolResultHelper.FromResponse(FunctionOperationResponse.Forbidden(request.Operation, "Operation " + request.Operation + " is not implemented"), annotations), 
			});
			return result3;
		}
		catch (Exception ex)
		{
			_logger.LogOperationError("FunctionOperationsTool", request.Operation, ex);
			string message = op switch
			{
				"CREATE" => "Error creating function: " + ex.GetErrorMessage(), 
				"UPDATE" => "Error executing Update operation: " + ex.GetErrorMessage(), 
				"DELETE" => "Error executing Delete operation: " + ex.GetErrorMessage(), 
				"GET" => "Error executing Get operation: " + ex.GetErrorMessage(), 
				"LIST" => "Error listing functions: " + ex.GetErrorMessage(), 
				"RENAME" => "Error executing Rename operation: " + ex.GetErrorMessage(), 
				"EXPORTTMDL" => "Failed to export TMDL for function '" + request.References?.FirstOrDefault()?.Name + "': " + ex.GetErrorMessage(), 
				_ => "Error executing function operation: " + ex.GetErrorMessage(), 
			};
			CallToolResult result2;
			result = (result2 = CallToolResultHelper.FromResponse(new FunctionOperationResponse
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

	private async Task<FunctionOperationResponse> HandleCreateOperation(FunctionOperationRequest request)
	{
		if (request.Definitions == null || !request.Definitions.Any())
		{
			return new FunctionOperationResponse
			{
				Success = false,
				Operation = "Create",
				Message = "Definitions is required and must contain at least one function definition"
			};
		}
		return MapBatchResponse(await FunctionOperations.CreateFunctions(request.ConnectionName, request.Definitions, request.Options));
	}

	private async Task<FunctionOperationResponse> HandleUpdateOperation(FunctionOperationRequest request)
	{
		if (request.Definitions == null || !request.Definitions.Any())
		{
			return new FunctionOperationResponse
			{
				Success = false,
				Operation = "Update",
				Message = "Definitions is required and must contain at least one function definition"
			};
		}
		return MapBatchResponse(await FunctionOperations.UpdateFunctions(request.ConnectionName, request.Definitions, request.Options));
	}

	private async Task<FunctionOperationResponse> HandleDeleteOperation(FunctionOperationRequest request)
	{
		if (request.References == null || !request.References.Any())
		{
			return new FunctionOperationResponse
			{
				Success = false,
				Operation = "Delete",
				Message = "References is required and must contain at least one function reference"
			};
		}
		return MapBatchResponse(await FunctionOperations.DeleteFunctions(request.ConnectionName, request.References, request.Options));
	}

	private async Task<FunctionOperationResponse> HandleGetOperation(FunctionOperationRequest request)
	{
		if (request.References == null || !request.References.Any())
		{
			return new FunctionOperationResponse
			{
				Success = false,
				Operation = "Get",
				Message = "References is required and must contain at least one function reference"
			};
		}
		return MapBatchResponse(await FunctionOperations.GetFunctions(request.ConnectionName, request.References, request.Options));
	}

	private async Task<FunctionOperationResponse> HandleListOperation(FunctionOperationRequest request)
	{
		List<FunctionList> list = await FunctionOperations.ListFunctions(request.ConnectionName);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}", "FunctionOperationsTool", request.Operation, request.ConnectionName, list.Count);
		return new FunctionOperationResponse
		{
			Success = true,
			Message = $"Found {list.Count} functions in the model",
			Operation = request.Operation,
			Data = list
		};
	}

	private async Task<FunctionOperationResponse> HandleRenameOperation(FunctionOperationRequest request)
	{
		if (request.RenameDefinitions == null || !request.RenameDefinitions.Any())
		{
			return new FunctionOperationResponse
			{
				Success = false,
				Operation = "Rename",
				Message = "RenameDefinitions is required and must contain at least one rename definition"
			};
		}
		return MapBatchResponse(await FunctionOperations.RenameFunctions(request.ConnectionName, request.RenameDefinitions, request.Options));
	}

	private async Task<FunctionOperationResponse> HandleExportTMDLOperation(FunctionOperationRequest request)
	{
		ExportValidationResult validation = ExportValidationHelper.ValidateReferences(request.References, "Function");
		if (!validation.IsValid)
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new FunctionOperationResponse
			{
				Success = false,
				Operation = "ExportTMDL",
				Message = validation.ErrorMessage,
				Help = value
			};
		}
		FunctionReference functionReference = request.References.First();
		ExportValidationResult exportValidationResult = ExportValidationHelper.ValidateName(functionReference.Name, "Function");
		if (!exportValidationResult.IsValid)
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value2);
			return new FunctionOperationResponse
			{
				Success = false,
				Operation = "ExportTMDL",
				Message = exportValidationResult.ErrorMessage,
				Help = value2
			};
		}
		string functionName = functionReference.Name;
		string data = await FunctionOperations.ExportTMDL(request.ConnectionName, functionName, request.TmdlExportOptions ?? new ExportTmdl());
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "FunctionOperationsTool", request.Operation, request.ConnectionName);
		string message = ExportValidationHelper.FormatSuccessMessage("Function", functionName, validation.WarningMessage);
		List<string> warnings = ((!string.IsNullOrEmpty(validation.WarningMessage)) ? new List<string> { validation.WarningMessage } : null);
		return new FunctionOperationResponse
		{
			Success = true,
			Message = message,
			Operation = request.Operation,
			Data = data,
			Warnings = warnings
		};
	}

	private FunctionOperationResponse HandleHelpOperation(FunctionOperationRequest request, string[] operations)
	{
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Operations={OperationCount}", "FunctionOperationsTool", request.Operation, request.ConnectionName, operations.Length);
		FunctionOperationResponse functionOperationResponse = new FunctionOperationResponse();
		functionOperationResponse.Success = true;
		functionOperationResponse.Message = "Tool description retrieved successfully";
		functionOperationResponse.Operation = request.Operation;
		functionOperationResponse.Help = new
		{
			ToolName = "function_operations",
			Description = "Perform operations on semantic model functions.",
			SupportedOperations = operations,
			Examples = toolMetadata.Operations.Where<KeyValuePair<string, OperationMetadata>>((KeyValuePair<string, OperationMetadata> p) => operations.Contains<string>(p.Key, StringComparer.OrdinalIgnoreCase)),
			Notes = new string[3] { "Use Definitions for Create and Update operations.", "Use References for Delete, Get, and ExportTMDL operations.", "Functions are user-defined DAX functions in the semantic model." }
		};
		return functionOperationResponse;
	}

	private FunctionOperationResponse MapBatchResponse(BatchOperationResponse batchResponse)
	{
		return new FunctionOperationResponse
		{
			Success = batchResponse.Success,
			Message = batchResponse.Message,
			Operation = batchResponse.Operation,
			Summary = batchResponse.Summary,
			Results = batchResponse.Results,
			Warnings = batchResponse.Warnings
		};
	}

	private (bool isValid, string? errorMessage) ValidateRequest(string operation, FunctionOperationRequest request)
	{
		switch (operation.ToUpperInvariant())
		{
		case "UPDATE":
		case "CREATE":
			if (request.Definitions == null || !request.Definitions.Any())
			{
				string item2 = "Definitions is required for Create/Update operations";
				return (isValid: false, errorMessage: item2);
			}
			break;
		case "DELETE":
		case "GET":
		case "EXPORTTMDL":
			if (request.References == null || !request.References.Any())
			{
				string item3 = "References is required for Delete/Get/ExportTMDL operations";
				return (isValid: false, errorMessage: item3);
			}
			break;
		case "RENAME":
			if (request.RenameDefinitions == null || !request.RenameDefinitions.Any())
			{
				string item = "RenameDefinitions is required for Rename operation";
				return (isValid: false, errorMessage: item);
			}
			break;
		}
		return (isValid: true, errorMessage: null);
	}
}
