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
public class QueryGroupOperationsTool
{
	public const string ToolName = "query_group_operations";

	private readonly ILogger<QueryGroupOperationsTool> _logger;

	public static readonly ToolMetadata toolMetadata = new ToolMetadata
	{
		Operations = new Dictionary<string, OperationMetadata>(StringComparer.OrdinalIgnoreCase)
		{
			["Create"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Definitions" },
				Description = "Create one or more query groups in the semantic model. Query groups organize related queries and expressions.\nMandatory properties: Definitions (list of QueryGroupDefinition objects with Folder).\nOptional: Description, Annotations, Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            { \n                \"Folder\": \"Sales\",\n                \"Description\": \"Sales-related queries\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            { \n                \"Folder\": \"Sales\",\n                \"Description\": \"Sales-related queries\"\n            },\n            {\n                \"Folder\": \"Marketing\",\n                \"Description\": \"Marketing-related queries\"\n            }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": false,\n            \"UseTransaction\": true\n        }\n    }\n}" }
			},
			["Update"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Definitions" },
				Description = "Update one or more existing query groups' properties. Names cannot be changed.\nMandatory properties: Definitions (list of QueryGroupDefinition objects with Name).\nOptional: Description, Folder, Annotations, Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Update\",\n        \"Definitions\": [\n            { \n                \"Name\": \"SalesQueries\",\n                \"Folder\": \"Sales\",\n                \"Description\": \"Updated queries for sales data\" \n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Update\",\n        \"Definitions\": [\n            { \n                \"Name\": \"SalesQueries\",\n                \"Description\": \"Updated sales queries\" \n            },\n            {\n                \"Name\": \"MarketingQueries\",\n                \"Description\": \"Updated marketing queries\"\n            }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": true,\n            \"UseTransaction\": true\n        }\n    }\n}" }
			},
			["Delete"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Delete one or more query groups from the semantic model. Checks for dependencies (partitions and named expressions) before deletion.\nMandatory properties: References (list of QueryGroupReference objects with Name).\nOptional: Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Delete\",\n        \"References\": [\n            {\n                \"Name\": \"ObsoleteQueryGroup\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Delete\",\n        \"References\": [\n            {\n                \"Name\": \"ObsoleteQueryGroup1\"\n            },\n            {\n                \"Name\": \"ObsoleteQueryGroup2\"\n            }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": true,\n            \"UseTransaction\": true\n        }\n    }\n}" }
			},
			["Get"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Retrieve detailed metadata for one or more query groups including properties and annotations.\nMandatory properties: References (list of QueryGroupReference objects with Name).\nOptional: Options (ContinueOnError).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Get\",\n        \"References\": [\n            {\n                \"Name\": \"SalesQueries\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Get\",\n        \"References\": [\n            {\n                \"Name\": \"SalesQueries\"\n            },\n            {\n                \"Name\": \"MarketingQueries\"\n            }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": true\n        }\n    }\n}" }
			},
			["List"] = new OperationMetadata
			{
				Description = "List all query groups in the semantic model with basic information (name, description, folder).\nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"List\"\n    }\n}" }
			},
			["ExportTMDL"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Export a query group to TMDL format.\nMandatory properties: References (list with at least one QueryGroupReference containing Name).\nOptional: TmdlExportOptions (TmdlSerializationOptions.IncludeChildren defaults to false).\nNote: Only the first reference is used; additional references are ignored with a warning.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ExportTMDL\",\n        \"TmdlExportOptions\": {\n            \"TmdlSerializationOptions\": {\n                \"IncludeChildren\": true\n            }\n        },\n        \"References\": [\n            { \"Name\": \"SalesQueries\" }\n        ]\n    }\n}" }
			},
			["Help"] = new OperationMetadata
			{
				Description = "Display comprehensive help information for the query group operations tool including supported operations and examples.\nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Help\"\n    }\n}" }
			}
		}
	};

	public QueryGroupOperationsTool(ILogger<QueryGroupOperationsTool> logger)
	{
		_logger = logger;
	}

	[McpServerTool(Name = "query_group_operations", Title = "Query Group Operations", ReadOnly = false, Destructive = true)]
	[YamlToolDescription("query_group_operations")]
	public async Task<CallToolResult> ExecuteQueryGroupOperation(McpServer mcpServer, QueryGroupOperationRequest request, IWriteGuard writeGuard)
	{
		_logger.LogDebug("Executing {ToolName}.{Operation}: QueryGroup={QueryGroupName}, Connection={ConnectionName}", "QueryGroupOperationsTool", request.Operation, request.References?.FirstOrDefault()?.Name ?? request.Definitions?.FirstOrDefault()?.Name ?? "(multiple or unspecified)", request.ConnectionName ?? "(last used)");
		string[] validOperations = new string[7] { "CREATE", "UPDATE", "DELETE", "GET", "LIST", "EXPORTTMDL", "HELP" };
		string[] writeOperations = new string[3] { "CREATE", "UPDATE", "DELETE" };
		string op = request.Operation.ToUpperInvariant();
		ToolCallAnnotations annotations = ToolCallAnnotations.Create("query_group_operations", request.Operation, !Enumerable.Contains(writeOperations, op));
		CallToolResult result = null;
		try
		{
			if (!Enumerable.Contains(validOperations, op))
			{
				_logger.LogWarning("Invalid operation '{Operation}' requested for {ToolName}. Valid operations: {ValidOperations}", request.Operation, "QueryGroupOperationsTool", string.Join(", ", validOperations));
				CallToolResult result2;
				result = (result2 = CallToolResultHelper.FromResponse(QueryGroupOperationResponse.Forbidden(request.Operation, "Invalid operation: " + request.Operation + ". Supported operations: " + string.Join(", ", validOperations)), annotations));
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
					_logger.LogWarning("{ToolName}.{Operation} blocked by write guard: {Reason}", "QueryGroupOperationsTool", request.Operation, writeOperationResult.Message);
					CallToolResult result2;
					result = (result2 = CallToolResultHelper.FromResponse(new QueryGroupOperationResponse
					{
						Success = false,
						Warnings = writeOperationResult.Warnings,
						Message = writeOperationResult.Message,
						Operation = request.Operation
					}, annotations));
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
				"EXPORTTMDL" => CallToolResultHelper.FromExportResponse(await HandleExportTMDLOperation(request), (request.References?.FirstOrDefault()?.Name ?? "querygroup") + ".tmdl", "text/plain", annotations), 
				"HELP" => CallToolResultHelper.FromResponse(await HandleHelpOperation(request, isWriteEnabled ? validOperations : validOperations.Except(writeOperations).ToArray()), annotations), 
				_ => CallToolResultHelper.FromResponse(QueryGroupOperationResponse.Forbidden(request.Operation.ToUpperInvariant(), "Operation '" + request.Operation + "' is not implemented"), annotations), 
			});
			return result3;
		}
		catch (Exception ex)
		{
			_logger.LogOperationError("QueryGroupOperationsTool", request.Operation, ex);
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			string message = op switch
			{
				"CREATE" => "Error executing Create operation: " + ex.GetErrorMessage(), 
				"UPDATE" => "Error executing Update operation: " + ex.GetErrorMessage(), 
				"DELETE" => "Error executing Delete operation: " + ex.GetErrorMessage(), 
				"GET" => "Error executing Get operation: " + ex.GetErrorMessage(), 
				"LIST" => "Error executing List operation: " + ex.GetErrorMessage(), 
				"EXPORTTMDL" => "Error exporting query group TMDL: " + ex.GetErrorMessage(), 
				_ => "Error executing query group operation: " + ex.GetErrorMessage(), 
			};
			CallToolResult result2;
			result = (result2 = CallToolResultHelper.FromResponse(new QueryGroupOperationResponse
			{
				Success = false,
				Message = message,
				Operation = request.Operation.ToUpperInvariant(),
				Help = value
			}, annotations, ex));
			return result2;
		}
		finally
		{
			_logger.LogToolCallCompleted(annotations.Title, !annotations.ReadOnlyHint, result?.IsError ?? true, ConnectionOperations.ResolveSemanticModelId());
		}
	}

	private async Task<QueryGroupOperationResponse> HandleCreateOperation(QueryGroupOperationRequest request)
	{
		if (request.Definitions == null || !request.Definitions.Any())
		{
			return new QueryGroupOperationResponse
			{
				Success = false,
				Operation = "Create",
				Message = "Definitions is required and must contain at least one query group definition"
			};
		}
		return MapBatchResponse(await QueryGroupOperations.CreateQueryGroups(request.ConnectionName, request.Definitions, request.Options));
	}

	private async Task<QueryGroupOperationResponse> HandleUpdateOperation(QueryGroupOperationRequest request)
	{
		if (request.Definitions == null || !request.Definitions.Any())
		{
			return new QueryGroupOperationResponse
			{
				Success = false,
				Operation = "Update",
				Message = "Definitions is required and must contain at least one query group definition"
			};
		}
		return MapBatchResponse(await QueryGroupOperations.UpdateQueryGroups(request.ConnectionName, request.Definitions, request.Options));
	}

	private async Task<QueryGroupOperationResponse> HandleDeleteOperation(QueryGroupOperationRequest request)
	{
		if (request.References == null || !request.References.Any())
		{
			return new QueryGroupOperationResponse
			{
				Success = false,
				Operation = "Delete",
				Message = "References is required and must contain at least one query group reference"
			};
		}
		return MapBatchResponse(await QueryGroupOperations.DeleteQueryGroups(request.ConnectionName, request.References, request.Options));
	}

	private async Task<QueryGroupOperationResponse> HandleGetOperation(QueryGroupOperationRequest request)
	{
		if (request.References == null || !request.References.Any())
		{
			return new QueryGroupOperationResponse
			{
				Success = false,
				Operation = "Get",
				Message = "References is required and must contain at least one query group reference"
			};
		}
		return MapBatchResponse(await QueryGroupOperations.GetQueryGroups(request.ConnectionName, request.References, request.Options));
	}

	private async Task<QueryGroupOperationResponse> HandleListOperation(QueryGroupOperationRequest request)
	{
		List<QueryGroupList> list = await QueryGroupOperations.ListQueryGroups(request.ConnectionName);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}", "QueryGroupOperationsTool", "LIST", request.ConnectionName, list.Count);
		return new QueryGroupOperationResponse
		{
			Success = true,
			Message = $"Found {list.Count} query groups",
			Operation = "LIST",
			Data = list
		};
	}

	private async Task<QueryGroupOperationResponse> HandleExportTMDLOperation(QueryGroupOperationRequest request)
	{
		ExportValidationResult validation = ExportValidationHelper.ValidateReferences(request.References, "QueryGroup");
		if (!validation.IsValid)
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new QueryGroupOperationResponse
			{
				Success = false,
				Operation = "ExportTMDL",
				Message = validation.ErrorMessage,
				Help = value
			};
		}
		QueryGroupReference queryGroupReference = request.References.First();
		ExportValidationResult exportValidationResult = ExportValidationHelper.ValidateName(queryGroupReference.Name, "QueryGroup");
		if (!exportValidationResult.IsValid)
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value2);
			return new QueryGroupOperationResponse
			{
				Success = false,
				Operation = "ExportTMDL",
				Message = exportValidationResult.ErrorMessage,
				Help = value2
			};
		}
		string queryGroupName = queryGroupReference.Name;
		string data = await QueryGroupOperations.ExportTMDL(request.ConnectionName, queryGroupName, request.TmdlExportOptions ?? new ExportTmdl());
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "QueryGroupOperationsTool", "ExportTMDL", request.ConnectionName);
		string message = ExportValidationHelper.FormatSuccessMessage("Query Group", queryGroupName, validation.WarningMessage);
		List<string> warnings = ((!string.IsNullOrEmpty(validation.WarningMessage)) ? new List<string> { validation.WarningMessage } : null);
		return new QueryGroupOperationResponse
		{
			Success = true,
			Message = message,
			Operation = "ExportTMDL",
			Data = data,
			Warnings = warnings
		};
	}

	private Task<QueryGroupOperationResponse> HandleHelpOperation(QueryGroupOperationRequest request, string[] operations)
	{
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Operations={OperationCount}", "QueryGroupOperationsTool", request.Operation, request.ConnectionName, operations.Length);
		QueryGroupOperationResponse queryGroupOperationResponse = new QueryGroupOperationResponse();
		queryGroupOperationResponse.Success = true;
		queryGroupOperationResponse.Message = "Tool description retrieved successfully";
		queryGroupOperationResponse.Operation = request.Operation;
		queryGroupOperationResponse.Help = new
		{
			ToolName = "query_group_operations",
			Description = "Perform operations on semantic model query groups.",
			SupportedOperations = operations,
			Examples = toolMetadata.Operations.Where<KeyValuePair<string, OperationMetadata>>((KeyValuePair<string, OperationMetadata> p) => operations.Contains<string>(p.Key, StringComparer.OrdinalIgnoreCase)),
			Notes = new string[9] { "Query groups organize partitions and named expressions. They are NOT related to DAX queries.", "Use the Operation parameter to specify which operation to perform.", "Operations accept lists of items for bulk processing.", "Single-item operations are represented as lists of one.", "Use Options.ContinueOnError to control error handling in batch operations.", "Use Options.UseTransaction to control transactional behavior.", "For Create operations, the Folder property is required in Definitions.", "For Update operations, the Name property is required in Definitions.", "For Delete, Get, and ExportTMDL operations, use References with Name property." }
		};
		return Task.FromResult(queryGroupOperationResponse);
	}

	private QueryGroupOperationResponse MapBatchResponse(BatchOperationResponse batchResponse)
	{
		return new QueryGroupOperationResponse
		{
			Success = batchResponse.Success,
			Message = batchResponse.Message,
			Operation = batchResponse.Operation,
			Summary = batchResponse.Summary,
			Results = batchResponse.Results,
			Warnings = batchResponse.Warnings
		};
	}

	private (bool isValid, string? errorMessage) ValidateRequest(string operation, QueryGroupOperationRequest request)
	{
		switch (operation.ToUpperInvariant())
		{
		case "CREATE":
			if (request.Definitions == null || !request.Definitions.Any())
			{
				string item2 = "Definitions is required for Create operation";
				return (isValid: false, errorMessage: item2);
			}
			break;
		case "UPDATE":
			if (request.Definitions == null || !request.Definitions.Any())
			{
				string item4 = "Definitions is required for Update operation";
				return (isValid: false, errorMessage: item4);
			}
			break;
		case "DELETE":
			if (request.References == null || !request.References.Any())
			{
				string item3 = "References is required for Delete operation";
				return (isValid: false, errorMessage: item3);
			}
			break;
		case "GET":
			if (request.References == null || !request.References.Any())
			{
				string item5 = "References is required for Get operation";
				return (isValid: false, errorMessage: item5);
			}
			break;
		case "EXPORTTMDL":
			if (request.References == null || !request.References.Any() || string.IsNullOrEmpty(request.References.First().Name))
			{
				string item = "References with Name is required for ExportTMDL operation";
				return (isValid: false, errorMessage: item);
			}
			break;
		}
		return (isValid: true, errorMessage: null);
	}
}
