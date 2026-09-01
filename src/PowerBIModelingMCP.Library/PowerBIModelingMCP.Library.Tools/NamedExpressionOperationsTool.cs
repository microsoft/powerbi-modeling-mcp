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
public class NamedExpressionOperationsTool
{
	public const string ToolName = "named_expression_operations";

	private readonly ILogger<NamedExpressionOperationsTool> _logger;

	public static readonly ToolMetadata toolMetadata = new ToolMetadata
	{
		Operations = new Dictionary<string, OperationMetadata>(StringComparer.OrdinalIgnoreCase)
		{
			["Create"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Definitions" },
				Description = "Create one or more named expressions in the semantic model. Named expressions are M expressions that can be shared across multiple tables/partitions and enable scenarios like parameterized data sources, shared logic, and temporary overrides via OOL bindings during refresh operations.\nMandatory properties: Definitions (list of NamedExpressionDefinition objects with Name, Expression, Kind).\nOptional: Description, LineageTag, SourceLineageTag, QueryGroupName, Annotations, ExtendedProperties, Options (ContinueOnError, UseTransaction).",
				CommonMistakes = new string[3] { "Using Create for Power Query parameters instead of CreateParameter operation", "Missing Kind property (required for Create, auto-set by CreateParameter)", "Incorrect parameter expression syntax when creating parameters manually" },
				Tips = new string[3] { "Use CreateParameter for Power Query parameters - it handles metadata and syntax automatically", "Use Create for generic M expressions like shared query logic or data transformations", "Kind is always set to 'M' since named expressions are Power Query expressions" },
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            {\n                \"Name\": \"MyExpression\",\n                \"Expression\": \"let Source = Excel.CurrentWorkbook(){[Name=\\\"SalesData\\\"]}[Content], FilteredRows = Table.SelectRows(Source, each [Region] = \\\"West\\\") in FilteredRows\",\n                \"Kind\": \"M\",\n                \"Description\": \"My description\",\n                \"QueryGroupName\": \"QueryGroup1\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            {\n                \"Name\": \"Expression1\",\n                \"Expression\": \"let Source = #table({\\\"Col1\\\"}, {{1}}) in Source\",\n                \"Kind\": \"M\"\n            },\n            {\n                \"Name\": \"Expression2\",\n                \"Expression\": \"let Source = #table({\\\"Col2\\\"}, {{2}}) in Source\",\n                \"Kind\": \"M\"\n            }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": false,\n            \"UseTransaction\": true\n        }\n    }\n}" }
			},
			["Update"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Definitions" },
				Description = "Update one or more existing named expressions. Names cannot be changed; use the Rename operation instead.\nMandatory properties: Definitions (list of NamedExpressionDefinition objects with Name matching target expression).\nOptional: Expression, Kind, Description, LineageTag, SourceLineageTag, QueryGroupName, Annotations, ExtendedProperties, Options (ContinueOnError, UseTransaction).",
				CommonMistakes = new string[3] { "Using Update for Power Query parameters instead of UpdateParameter operation", "Attempting to change the Name property (use Rename operation instead)", "Breaking parameter metadata when updating parameter expressions manually" },
				Tips = new string[3] { "Use UpdateParameter for Power Query parameters - it preserves metadata and handles syntax", "Use Update for generic M expressions like shared query logic or data transformations", "Omit properties to keep current values, set to empty string to clear (except Expression which cannot be empty)" },
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Update\",\n        \"Definitions\": [\n            {\n                \"Name\": \"MyExpression\",\n                \"Expression\": \"let Source = Excel.CurrentWorkbook(){[Name=\\\"SalesData\\\"]}[Content], FilteredRows = Table.SelectRows(Source, each [Region] = \\\"West\\\") in FilteredRows\",\n                \"Kind\": \"M\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Update\",\n        \"Definitions\": [\n            {\n                \"Name\": \"Expression1\",\n                \"Description\": \"Updated description 1\"\n            },\n            {\n                \"Name\": \"Expression2\",\n                \"Description\": \"Updated description 2\"\n            }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": true,\n            \"UseTransaction\": true\n        }\n    }\n}" }
			},
			["Delete"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Delete one or more named expressions from the semantic model.\nMandatory properties: References (list of NamedExpressionReference objects with Name).\nOptional: Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Delete\",\n        \"References\": [\n            { \"Name\": \"MyExpression\" }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Delete\",\n        \"References\": [\n            { \"Name\": \"Expression1\" },\n            { \"Name\": \"Expression2\" }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": true,\n            \"UseTransaction\": true\n        }\n    }\n}" }
			},
			["Get"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Retrieve detailed information about one or more specific named expressions.\nMandatory properties: References (list of NamedExpressionReference objects with Name).\nOptional: Options (ContinueOnError).",
				Tips = new string[2] { "Use List operation first to see all available named expressions", "Parameters will have metadata like IsParameterQuery=true in their Expression" },
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Get\",\n        \"References\": [\n            { \"Name\": \"MyExpression\" }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Get\",\n        \"References\": [\n            { \"Name\": \"Expression1\" },\n            { \"Name\": \"Expression2\" }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": true\n        }\n    }\n}" }
			},
			["List"] = new OperationMetadata
			{
				Description = "List all named expressions in the semantic model with basic information.\nNo mandatory properties required.",
				Tips = new string[3] { "Use this to discover available named expressions before Get, Update, or Delete operations", "All named expressions have Kind='M' since they are Power Query expressions", "Parameters are special M expressions with metadata - use CreateParameter/UpdateParameter for better handling" },
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"List\"\n    }\n}" }
			},
			["Rename"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "RenameDefinitions" },
				Description = "Rename one or more named expressions by changing their identifiers.\nMandatory properties: RenameDefinitions (list of NamedExpressionRename objects with CurrentName and NewName).\nOptional: Options (ContinueOnError, UseTransaction).",
				Tips = new string[3] { "Use this instead of Update operation when you need to change the name", "Works for both generic named expressions and Power Query parameters", "All references to the named expression will be automatically updated" },
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Rename\",\n        \"RenameDefinitions\": [\n            { \n                \"CurrentName\": \"OldExpression\", \n                \"NewName\": \"NewExpression\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Rename\",\n        \"RenameDefinitions\": [\n            { \n                \"CurrentName\": \"OldExpression1\", \n                \"NewName\": \"NewExpression1\"\n            },\n            {\n                \"CurrentName\": \"OldExpression2\",\n                \"NewName\": \"NewExpression2\"\n            }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": false,\n            \"UseTransaction\": true\n        }\n    }\n}" }
			},
			["CreateParameter"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Definitions" },
				Description = "Create one or more Power Query parameters with proper metadata. Expression will be automatically converted to parameter format if needed.\nMandatory properties: Definitions (list of NamedExpressionDefinition objects with Name, Expression). Kind will be automatically set to 'M'.\nOptional: Description, LineageTag, SourceLineageTag, QueryGroupName, Annotations, ExtendedProperties, Options (ContinueOnError, UseTransaction).",
				CommonMistakes = new string[1] { "Manually formatting parameter expression syntax (let the tool handle it)" },
				Tips = new string[5] { "Preferred over Create for Power Query parameters - handles metadata automatically", "Power Query parameters are also referred to as semantic model parameters or Power BI parameters", "Provide simple values in Expression - tool converts to proper parameter format", "Auto-adds metadata: IsParameterQuery=true, Type=\"Text\", IsParameterQueryRequired=true", "Example: Expression=\"MyDatabase\" becomes '\"MyDatabase\" meta [IsParameterQuery=true, Type=\"Text\", IsParameterQueryRequired=true]'" },
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"CreateParameter\",\n        \"Definitions\": [\n            {\n                \"Name\": \"DataSourcePath\",\n                \"Expression\": \"https://mycompany.com/data/myfile.csv\",\n                \"Kind\": \"M\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"CreateParameter\",\n        \"Definitions\": [\n            {\n                \"Name\": \"ServerName\",\n                \"Expression\": \"myserver.database.windows.net\",\n                \"Kind\": \"M\"\n            },\n            {\n                \"Name\": \"DatabaseName\",\n                \"Expression\": \"MyDatabase\",\n                \"Kind\": \"M\"\n            }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": false,\n            \"UseTransaction\": true\n        }\n    }\n}" }
			},
			["UpdateParameter"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Definitions" },
				Description = "Update one or more existing Power Query parameters. Expression will be automatically converted to parameter format if needed. Names cannot be changed; use the Rename operation instead.\nMandatory properties: Definitions (list of NamedExpressionDefinition objects with Name matching target parameter). Kind will be automatically set to 'M'.\nOptional: Expression, Description, LineageTag, SourceLineageTag, QueryGroupName, Annotations, ExtendedProperties, Options (ContinueOnError, UseTransaction).",
				CommonMistakes = new string[3] { "Using Update operation instead of UpdateParameter for Power Query parameters", "Manually formatting parameter expression syntax (let the tool handle it)", "Breaking existing parameter metadata by using generic Update operation" },
				Tips = new string[3] { "Preferred over Update for Power Query parameters - preserves metadata automatically", "Provide simple values in Expression - tool converts to proper parameter format", "Maintains existing parameter metadata while updating the value" },
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"UpdateParameter\",\n        \"Definitions\": [\n            {\n                \"Name\": \"DataSourcePath\",\n                \"Expression\": \"https://mycompany.com/data/newfile.csv\",\n                \"Kind\": \"M\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"UpdateParameter\",\n        \"Definitions\": [\n            {\n                \"Name\": \"ServerName\",\n                \"Expression\": \"newserver.database.windows.net\",\n                \"Kind\": \"M\"\n            },\n            {\n                \"Name\": \"DatabaseName\",\n                \"Expression\": \"NewDatabase\",\n                \"Kind\": \"M\"\n            }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": true,\n            \"UseTransaction\": true\n        }\n    }\n}" }
			},
			["ExportTMDL"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Export a named expression to TMDL format.\nMandatory properties: References (list with one NamedExpressionReference object containing Name).\nOptional: TmdlExportOptions (TmdlSerializationOptions.IncludeChildren defaults to false).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ExportTMDL\",\n        \"TmdlExportOptions\": {\n            \"TmdlSerializationOptions\": {\n                \"IncludeChildren\": true\n            }\n        },\n        \"References\": [\n            { \"Name\": \"MyExpression\" }\n        ]\n    }\n}" }
			},
			["Help"] = new OperationMetadata
			{
				Description = "Retrieve detailed information about the named expression operations tool and its capabilities.\nNo mandatory properties required.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Help\"\n    }\n}" }
			}
		}
	};

	public NamedExpressionOperationsTool(ILogger<NamedExpressionOperationsTool> logger)
	{
		_logger = logger;
	}

	[McpServerTool(Name = "named_expression_operations", Title = "Named Expression Operations", ReadOnly = false, Destructive = true)]
	[YamlToolDescription("named_expression_operations")]
	public async Task<CallToolResult> ExecuteNamedExpressionOperation(McpServer mcpServer, NamedExpressionOperationRequest request, IWriteGuard writeGuard)
	{
		_logger.LogDebug("Executing {ToolName}.{Operation}: Connection={ConnectionName}", "NamedExpressionOperationsTool", request.Operation, request.ConnectionName ?? "(last used)");
		string[] validOperations = new string[10] { "CREATE", "UPDATE", "DELETE", "GET", "LIST", "RENAME", "CREATEPARAMETER", "UPDATEPARAMETER", "EXPORTTMDL", "HELP" };
		string[] writeOperations = new string[6] { "CREATE", "UPDATE", "DELETE", "RENAME", "CREATEPARAMETER", "UPDATEPARAMETER" };
		string op = request.Operation.ToUpperInvariant();
		ToolCallAnnotations annotations = ToolCallAnnotations.Create("named_expression_operations", request.Operation, !Enumerable.Contains(writeOperations, op));
		CallToolResult result = null;
		try
		{
			if (!Enumerable.Contains(validOperations, op))
			{
				_logger.LogWarning("Invalid operation '{Operation}' requested for {ToolName}. Valid operations: {ValidOperations}", request.Operation, "NamedExpressionOperationsTool", string.Join(", ", validOperations));
				CallToolResult result2;
				result = (result2 = CallToolResultHelper.FromResponse(NamedExpressionOperationResponse.Forbidden(request.Operation, "Invalid operation: " + request.Operation + ". Supported operations: " + string.Join(", ", validOperations)), annotations));
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
					_logger.LogWarning("{ToolName}.{Operation} blocked by write guard: {Reason}", "NamedExpressionOperationsTool", request.Operation, writeOperationResult.Message);
					CallToolResult result2;
					result = (result2 = CallToolResultHelper.FromResponse(new NamedExpressionOperationResponse
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
			result = (result3 = op switch
			{
				"CREATE" => CallToolResultHelper.FromResponse(await HandleCreateOperation(request), annotations, null, minimalSuccessPayload: true), 
				"UPDATE" => CallToolResultHelper.FromResponse(await HandleUpdateOperation(request), annotations, null, minimalSuccessPayload: true), 
				"DELETE" => CallToolResultHelper.FromResponse(await HandleDeleteOperation(request), annotations, null, minimalSuccessPayload: true), 
				"GET" => CallToolResultHelper.FromResponse(await HandleGetOperation(request), annotations), 
				"LIST" => CallToolResultHelper.FromResponse(await HandleListOperation(request), annotations), 
				"RENAME" => CallToolResultHelper.FromResponse(await HandleRenameOperation(request), annotations, null, minimalSuccessPayload: true), 
				"CREATEPARAMETER" => CallToolResultHelper.FromResponse(await HandleCreateParameterOperation(request), annotations, null, minimalSuccessPayload: true), 
				"UPDATEPARAMETER" => CallToolResultHelper.FromResponse(await HandleUpdateParameterOperation(request), annotations, null, minimalSuccessPayload: true), 
				"EXPORTTMDL" => CallToolResultHelper.FromExportResponse(await HandleExportTMDLOperation(request), (request.References?.FirstOrDefault()?.Name ?? "expression") + ".tmdl", "text/plain", annotations), 
				"HELP" => CallToolResultHelper.FromResponse(HandleHelpOperation(request, isWriteEnabled ? validOperations : validOperations.Except(writeOperations).ToArray()), annotations), 
				_ => CallToolResultHelper.FromResponse(NamedExpressionOperationResponse.Forbidden(request.Operation, "Operation " + request.Operation + " is not implemented"), annotations), 
			});
			return result3;
		}
		catch (Exception ex)
		{
			_logger.LogOperationError("NamedExpressionOperationsTool", request.Operation, ex);
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			string message = op switch
			{
				"CREATE" => "Error creating named expression: " + ex.GetErrorMessage(), 
				"UPDATE" => "Error updating named expression: " + ex.GetErrorMessage(), 
				"DELETE" => "Error executing named expression operation: " + ex.GetErrorMessage(), 
				"GET" => "Error executing named expression operation: " + ex.GetErrorMessage(), 
				"LIST" => "Error listing named expressions: " + ex.GetErrorMessage(), 
				"RENAME" => "Error renaming named expression: " + ex.GetErrorMessage(), 
				"CREATEPARAMETER" => "Error creating parameter: " + ex.GetErrorMessage(), 
				"UPDATEPARAMETER" => "Error updating parameter: " + ex.GetErrorMessage(), 
				"EXPORTTMDL" => "Failed to export TMDL for named expression '" + (request.References?.FirstOrDefault()?.Name ?? "(unknown)") + "': " + ex.GetErrorMessage(), 
				_ => "Error executing named expression operation: " + ex.GetErrorMessage(), 
			};
			CallToolResult result2;
			result = (result2 = CallToolResultHelper.FromResponse(new NamedExpressionOperationResponse
			{
				Success = false,
				Message = message,
				Operation = request.Operation,
				Help = value
			}, annotations, ex));
			return result2;
		}
		finally
		{
			_logger.LogToolCallCompleted(annotations.Title, !annotations.ReadOnlyHint, result?.IsError ?? true, ConnectionOperations.ResolveSemanticModelId());
		}
	}

	private async Task<NamedExpressionOperationResponse> HandleCreateOperation(NamedExpressionOperationRequest request)
	{
		if (request.Definitions == null || !request.Definitions.Any())
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new NamedExpressionOperationResponse
			{
				Success = false,
				Operation = "Create",
				Message = "Definitions is required and must contain at least one named expression definition",
				Help = value
			};
		}
		BatchOperationResponse batchOperationResponse = await NamedExpressionOperations.CreateNamedExpressions(request.ConnectionName, request.Definitions, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}, Success={Success}", "NamedExpressionOperationsTool", "Create", request.ConnectionName, request.Definitions.Count, batchOperationResponse.Success);
		return MapBatchResponse(batchOperationResponse);
	}

	private async Task<NamedExpressionOperationResponse> HandleUpdateOperation(NamedExpressionOperationRequest request)
	{
		if (request.Definitions == null || !request.Definitions.Any())
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new NamedExpressionOperationResponse
			{
				Success = false,
				Operation = "Update",
				Message = "Definitions is required and must contain at least one named expression definition",
				Help = value
			};
		}
		BatchOperationResponse batchOperationResponse = await NamedExpressionOperations.UpdateNamedExpressions(request.ConnectionName, request.Definitions, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}, Success={Success}", "NamedExpressionOperationsTool", "Update", request.ConnectionName, request.Definitions.Count, batchOperationResponse.Success);
		return MapBatchResponse(batchOperationResponse);
	}

	private async Task<NamedExpressionOperationResponse> HandleDeleteOperation(NamedExpressionOperationRequest request)
	{
		if (request.References == null || !request.References.Any())
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new NamedExpressionOperationResponse
			{
				Success = false,
				Operation = "Delete",
				Message = "References is required and must contain at least one named expression reference",
				Help = value
			};
		}
		BatchOperationResponse batchOperationResponse = await NamedExpressionOperations.DeleteNamedExpressions(request.ConnectionName, request.References, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}, Success={Success}", "NamedExpressionOperationsTool", "Delete", request.ConnectionName, request.References.Count, batchOperationResponse.Success);
		return MapBatchResponse(batchOperationResponse);
	}

	private async Task<NamedExpressionOperationResponse> HandleGetOperation(NamedExpressionOperationRequest request)
	{
		if (request.References == null || !request.References.Any())
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new NamedExpressionOperationResponse
			{
				Success = false,
				Operation = "Get",
				Message = "References is required and must contain at least one named expression reference",
				Help = value
			};
		}
		BatchOperationResponse batchOperationResponse = await NamedExpressionOperations.GetNamedExpressions(request.ConnectionName, request.References, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}, Success={Success}", "NamedExpressionOperationsTool", "Get", request.ConnectionName, request.References.Count, batchOperationResponse.Success);
		return MapBatchResponse(batchOperationResponse);
	}

	private async Task<NamedExpressionOperationResponse> HandleListOperation(NamedExpressionOperationRequest request)
	{
		List<NamedExpressionList> list = await NamedExpressionOperations.ListNamedExpressions(request.ConnectionName);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}", "NamedExpressionOperationsTool", request.Operation, request.ConnectionName, list.Count);
		return new NamedExpressionOperationResponse
		{
			Success = true,
			Message = $"Found {list.Count} named expressions in the model",
			Operation = request.Operation,
			Data = list
		};
	}

	private async Task<NamedExpressionOperationResponse> HandleRenameOperation(NamedExpressionOperationRequest request)
	{
		if (request.RenameDefinitions == null || !request.RenameDefinitions.Any())
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new NamedExpressionOperationResponse
			{
				Success = false,
				Operation = "Rename",
				Message = "RenameDefinitions is required and must contain at least one named expression rename definition",
				Help = value
			};
		}
		BatchOperationResponse batchOperationResponse = await NamedExpressionOperations.RenameNamedExpressions(request.ConnectionName, request.RenameDefinitions, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}, Success={Success}", "NamedExpressionOperationsTool", "Rename", request.ConnectionName, request.RenameDefinitions.Count, batchOperationResponse.Success);
		return MapBatchResponse(batchOperationResponse);
	}

	private async Task<NamedExpressionOperationResponse> HandleCreateParameterOperation(NamedExpressionOperationRequest request)
	{
		if (request.Definitions == null || !request.Definitions.Any())
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new NamedExpressionOperationResponse
			{
				Success = false,
				Operation = "CreateParameter",
				Message = "Definitions is required and must contain at least one parameter definition",
				Help = value
			};
		}
		BatchOperationResponse batchOperationResponse = await NamedExpressionOperations.CreateParameters(request.ConnectionName, request.Definitions, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}, Success={Success}", "NamedExpressionOperationsTool", "CreateParameter", request.ConnectionName, request.Definitions.Count, batchOperationResponse.Success);
		return MapBatchResponse(batchOperationResponse);
	}

	private async Task<NamedExpressionOperationResponse> HandleUpdateParameterOperation(NamedExpressionOperationRequest request)
	{
		if (request.Definitions == null || !request.Definitions.Any())
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new NamedExpressionOperationResponse
			{
				Success = false,
				Operation = "UpdateParameter",
				Message = "Definitions is required and must contain at least one parameter definition",
				Help = value
			};
		}
		BatchOperationResponse batchOperationResponse = await NamedExpressionOperations.UpdateParameters(request.ConnectionName, request.Definitions, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}, Success={Success}", "NamedExpressionOperationsTool", "UpdateParameter", request.ConnectionName, request.Definitions.Count, batchOperationResponse.Success);
		return MapBatchResponse(batchOperationResponse);
	}

	private async Task<NamedExpressionOperationResponse> HandleExportTMDLOperation(NamedExpressionOperationRequest request)
	{
		ExportValidationResult validation = ExportValidationHelper.ValidateReferences(request.References, "NamedExpression");
		if (!validation.IsValid)
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new NamedExpressionOperationResponse
			{
				Success = false,
				Operation = "ExportTMDL",
				Message = validation.ErrorMessage,
				Help = value
			};
		}
		string namedExpressionName = request.References.First().Name;
		string data = await NamedExpressionOperations.ExportTMDL(request.ConnectionName, namedExpressionName, request.TmdlExportOptions ?? new ExportTmdl());
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "NamedExpressionOperationsTool", request.Operation, request.ConnectionName);
		string message = ExportValidationHelper.FormatSuccessMessage("Named Expression", namedExpressionName, validation.WarningMessage);
		List<string> warnings = ((!string.IsNullOrEmpty(validation.WarningMessage)) ? new List<string> { validation.WarningMessage } : null);
		return new NamedExpressionOperationResponse
		{
			Success = true,
			Message = message,
			Operation = request.Operation,
			Data = data,
			Warnings = warnings
		};
	}

	private NamedExpressionOperationResponse HandleHelpOperation(NamedExpressionOperationRequest request, string[] operations)
	{
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Operations={OperationCount}", "NamedExpressionOperationsTool", request.Operation, request.ConnectionName, operations.Length);
		NamedExpressionOperationResponse namedExpressionOperationResponse = new NamedExpressionOperationResponse();
		namedExpressionOperationResponse.Success = true;
		namedExpressionOperationResponse.Message = "Tool description retrieved successfully";
		namedExpressionOperationResponse.Operation = request.Operation;
		namedExpressionOperationResponse.Help = new
		{
			ToolName = "named_expression_operations",
			Description = "Perform operations on semantic model named expressions and Power Query parameters.",
			SupportedOperations = operations,
			Examples = toolMetadata.Operations.Where<KeyValuePair<string, OperationMetadata>>((KeyValuePair<string, OperationMetadata> p) => operations.Contains<string>(p.Key, StringComparer.OrdinalIgnoreCase)),
			Notes = new string[7] { "Use the Operation parameter to specify which operation to perform.", "Operations accept lists of items for bulk processing.", "Single-item operations are represented as lists of one.", "Use Options.ContinueOnError to control error handling in batch operations.", "Use Options.UseTransaction to control transactional behavior.", "Parameter operations create Power Query parameters with required metadata.", "ExportTMDL exports a named expression to TMDL format." }
		};
		return namedExpressionOperationResponse;
	}

	private NamedExpressionOperationResponse MapBatchResponse(BatchOperationResponse batchResponse)
	{
		NamedExpressionOperationResponse namedExpressionOperationResponse = new NamedExpressionOperationResponse
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
			namedExpressionOperationResponse.Exceptions.AddRange(batchResponse.Exceptions);
		}
		return namedExpressionOperationResponse;
	}

	private (bool isValid, string? errorMessage) ValidateRequest(string operation, NamedExpressionOperationRequest request)
	{
		switch (operation.ToUpperInvariant())
		{
		case "UPDATE":
		case "CREATE":
		case "CREATEPARAMETER":
		case "UPDATEPARAMETER":
			if (request.Definitions == null || !request.Definitions.Any())
			{
				string item2 = "Definitions is required for " + operation + " operation";
				return (isValid: false, errorMessage: item2);
			}
			break;
		case "DELETE":
		case "GET":
		case "EXPORTTMDL":
			if (request.References == null || !request.References.Any())
			{
				string item3 = "References is required for " + operation + " operation";
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
