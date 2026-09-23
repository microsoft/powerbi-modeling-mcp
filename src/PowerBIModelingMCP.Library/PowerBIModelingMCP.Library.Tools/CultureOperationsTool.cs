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
public class CultureOperationsTool
{
	public const string ToolName = "culture_operations";

	private readonly ILogger<CultureOperationsTool> _logger;

	public static readonly ToolMetadata toolMetadata = new ToolMetadata
	{
		Operations = new Dictionary<string, OperationMetadata>(StringComparer.OrdinalIgnoreCase)
		{
			["List"] = new OperationMetadata
			{
				Description = "List all cultures in the model.\nMandatory properties: None.\nOptional: IncludeNeutralCultures, IncludeUserCustomCultures.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"List\"\n    }\n}" }
			},
			["Get"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Get details of one or more cultures.\nMandatory properties: References (list of CultureReference objects with Name).\nOptional: Options (ContinueOnError).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Get\",\n        \"References\": [\n            { \"Name\": \"fr-FR\" }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Get\",\n        \"References\": [\n            { \"Name\": \"fr-FR\" },\n            { \"Name\": \"de-DE\" }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": true\n        }\n    }\n}" }
			},
			["Create"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Definitions" },
				Description = "Create one or more cultures.\nMandatory properties: Definitions (list of CultureDefinition objects with Name).\nOptional: Annotations, ExtendedProperties, Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            { \"Name\": \"fr-FR\" }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            { \"Name\": \"fr-FR\" },\n            { \"Name\": \"de-DE\" }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": false,\n            \"UseTransaction\": true\n        }\n    }\n}" }
			},
			["Update"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Definitions" },
				Description = "Update one or more existing cultures. Names cannot be changed - use Rename operation instead.\nMandatory properties: Definitions (list of CultureDefinition objects with Name).\nOptional: Annotations, ExtendedProperties, Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Update\",\n        \"Definitions\": [\n            { \n                \"Name\": \"fr-FR\",\n                \"Annotations\": [{\"Key\": \"Description\", \"Value\": \"French culture\"}]\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Update\",\n        \"Definitions\": [\n            { \n                \"Name\": \"fr-FR\",\n                \"Annotations\": [{\"Key\": \"Description\", \"Value\": \"French culture\"}]\n            },\n            {\n                \"Name\": \"de-DE\",\n                \"Annotations\": [{\"Key\": \"Description\", \"Value\": \"German culture\"}]\n            }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": true,\n            \"UseTransaction\": true\n        }\n    }\n}" }
			},
			["Delete"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Delete one or more cultures.\nMandatory properties: References (list of CultureReference objects with Name).\nOptional: Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Delete\",\n        \"References\": [\n            { \"Name\": \"ObsoleteCulture\" }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Delete\",\n        \"References\": [\n            { \"Name\": \"ObsoleteCulture1\" },\n            { \"Name\": \"ObsoleteCulture2\" }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": true,\n            \"UseTransaction\": true\n        }\n    }\n}" }
			},
			["Rename"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "RenameDefinitions" },
				Description = "Rename one or more cultures.\nMandatory properties: RenameDefinitions (list of CultureRename objects with CurrentName, NewName).\nOptional: Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Rename\",\n        \"RenameDefinitions\": [\n            { \"CurrentName\": \"fr-FR\", \"NewName\": \"es-ES\" }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Rename\",\n        \"RenameDefinitions\": [\n            { \"CurrentName\": \"fr-FR\", \"NewName\": \"es-ES\" },\n            { \"CurrentName\": \"de-DE\", \"NewName\": \"it-IT\" }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": false,\n            \"UseTransaction\": true\n        }\n    }\n}" }
			},
			["GetValidNames"] = new OperationMetadata
			{
				Description = "Get valid culture names.\nMandatory properties: None.\nOptional: IncludeNeutralCultures, IncludeUserCustomCultures.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"GetValidNames\",\n        \"IncludeNeutralCultures\": true,\n        \"IncludeUserCustomCultures\": false\n    }\n}" }
			},
			["GetValidDetails"] = new OperationMetadata
			{
				Description = "Get valid culture details.\nMandatory properties: None.\nOptional: IncludeNeutralCultures, IncludeUserCustomCultures.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"GetValidDetails\"\n    }\n}" }
			},
			["GetDetailsByName"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Get details by culture name.\nMandatory properties: References (list with one CultureReference with Name).\nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"GetDetailsByName\",\n        \"References\": [\n            { \"Name\": \"fr-FR\" }\n        ]\n    }\n}" }
			},
			["GetDetailsByLCID"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "LCID" },
				Description = "Get details by LCID.\nMandatory properties: LCID.\nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"GetDetailsByLCID\",\n        \"LCID\": 1033\n    }\n}" }
			},
			["ExportTMDL"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Export culture to TMDL format.\nMandatory properties: References (list with one CultureReference with Name).\nOptional: TmdlExportOptions (TmdlSerializationOptions.IncludeChildren defaults to false).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ExportTMDL\",\n        \"TmdlExportOptions\": {\n            \"TmdlSerializationOptions\": {\n                \"IncludeChildren\": true\n            }\n        },\n        \"References\": [\n            { \"Name\": \"fr-FR\" }\n        ]\n    }\n}" }
			},
			["Help"] = new OperationMetadata
			{
				Description = "Describe the tool and its operations.\nMandatory properties: None.\nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Help\"\n    }\n}" }
			}
		}
	};

	public CultureOperationsTool(ILogger<CultureOperationsTool> logger)
	{
		_logger = logger;
	}

	[McpServerTool(Name = "culture_operations", Title = "Culture Operations", ReadOnly = false, Destructive = true)]
	[YamlToolDescription("culture_operations")]
	public async Task<CallToolResult> ExecuteCultureOperation(McpServer mcpServer, CultureOperationRequest request, IWriteGuard writeGuard)
	{
		_logger.LogDebug("Executing {ToolName}.{Operation}: Connection={ConnectionName}", "CultureOperationsTool", request.Operation, request.ConnectionName ?? "(last used)");
		string[] validOperations = new string[12]
		{
			"CREATE", "UPDATE", "DELETE", "GET", "LIST", "RENAME", "GETVALIDNAMES", "GETVALIDDETAILS", "GETDETAILSBYNAME", "GETDETAILSBYLCID",
			"EXPORTTMDL", "HELP"
		};
		string[] writeOperations = new string[4] { "CREATE", "UPDATE", "DELETE", "RENAME" };
		string op = request.Operation.ToUpperInvariant();
		ToolCallAnnotations annotations = ToolCallAnnotations.Create("culture_operations", request.Operation, !Enumerable.Contains(writeOperations, op));
		CallToolResult result = null;
		try
		{
			if (!Enumerable.Contains(validOperations, op))
			{
				_logger.LogWarning("Invalid operation '{Operation}' requested for {ToolName}. Valid operations: {ValidOperations}", request.Operation, "CultureOperationsTool", string.Join(", ", validOperations));
				CallToolResult result2;
				result = (result2 = CallToolResultHelper.FromResponse(CultureOperationResponse.Forbidden(request.Operation, "Invalid operation: " + request.Operation + ". Supported operations: " + string.Join(", ", validOperations)), annotations));
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
					_logger.LogWarning("{ToolName}.{Operation} blocked by write guard: {Reason}", "CultureOperationsTool", request.Operation, writeOperationResult.Message);
					CallToolResult result2;
					result = (result2 = CallToolResultHelper.FromResponse(new CultureOperationResponse
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
				"GETVALIDNAMES" => CallToolResultHelper.FromResponse(HandleGetValidNamesOperation(request), annotations), 
				"GETVALIDDETAILS" => CallToolResultHelper.FromResponse(HandleGetValidDetailsOperation(request), annotations), 
				"GETDETAILSBYNAME" => CallToolResultHelper.FromResponse(HandleGetDetailsByNameOperation(request), annotations), 
				"GETDETAILSBYLCID" => CallToolResultHelper.FromResponse(HandleGetDetailsByLCIDOperation(request), annotations), 
				"EXPORTTMDL" => CallToolResultHelper.FromExportResponse(await HandleExportTMDLOperation(request), (request.References?.FirstOrDefault()?.Name ?? "culture") + ".tmdl", "text/plain", annotations), 
				"HELP" => CallToolResultHelper.FromResponse(HandleHelpOperation(request, isWriteEnabled ? validOperations : validOperations.Except(writeOperations).ToArray()), annotations), 
				_ => CallToolResultHelper.FromResponse(CultureOperationResponse.Forbidden(request.Operation, "Operation " + request.Operation + " is not implemented"), annotations), 
			});
			return result3;
		}
		catch (Exception ex)
		{
			_logger.LogOperationError("CultureOperationsTool", request.Operation, ex);
			string message = op switch
			{
				"CREATE" => "Error executing Create operation: " + ex.GetErrorMessage(), 
				"UPDATE" => "Error executing Update operation: " + ex.GetErrorMessage(), 
				"DELETE" => "Error executing Delete operation: " + ex.GetErrorMessage(), 
				"GET" => "Error executing Get operation: " + ex.GetErrorMessage(), 
				"LIST" => "Failed to list cultures: " + ex.GetErrorMessage(), 
				"RENAME" => "Error executing Rename operation: " + ex.GetErrorMessage(), 
				"GETVALIDNAMES" => "Failed to get valid culture names: " + ex.GetErrorMessage(), 
				"GETVALIDDETAILS" => "Failed to get valid culture details: " + ex.GetErrorMessage(), 
				"GETDETAILSBYNAME" => "Failed to get culture details: " + ex.GetErrorMessage(), 
				"GETDETAILSBYLCID" => $"Failed to get culture details for LCID '{request.LCID}': {ex.GetErrorMessage()}", 
				"EXPORTTMDL" => "Failed to export TMDL: " + ex.GetErrorMessage(), 
				_ => "Error executing culture operation: " + ex.GetErrorMessage(), 
			};
			CallToolResult result2;
			result = (result2 = CallToolResultHelper.FromResponse(new CultureOperationResponse
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

	private async Task<CultureOperationResponse> HandleCreateOperation(CultureOperationRequest request)
	{
		if (request.Definitions == null || !request.Definitions.Any())
		{
			return new CultureOperationResponse
			{
				Success = false,
				Operation = "Create",
				Message = "Definitions is required and must contain at least one culture definition"
			};
		}
		return MapBatchResponse(await CultureOperations.CreateCultures(request.ConnectionName, request.Definitions, request.Options));
	}

	private async Task<CultureOperationResponse> HandleUpdateOperation(CultureOperationRequest request)
	{
		if (request.Definitions == null || !request.Definitions.Any())
		{
			return new CultureOperationResponse
			{
				Success = false,
				Operation = "Update",
				Message = "Definitions is required and must contain at least one culture definition"
			};
		}
		return MapBatchResponse(await CultureOperations.UpdateCultures(request.ConnectionName, request.Definitions, request.Options));
	}

	private async Task<CultureOperationResponse> HandleDeleteOperation(CultureOperationRequest request)
	{
		if (request.References == null || !request.References.Any())
		{
			return new CultureOperationResponse
			{
				Success = false,
				Operation = "Delete",
				Message = "References is required and must contain at least one culture reference"
			};
		}
		return MapBatchResponse(await CultureOperations.DeleteCultures(request.ConnectionName, request.References, request.Options));
	}

	private async Task<CultureOperationResponse> HandleGetOperation(CultureOperationRequest request)
	{
		if (request.References == null || !request.References.Any())
		{
			return new CultureOperationResponse
			{
				Success = false,
				Operation = "Get",
				Message = "References is required and must contain at least one culture reference"
			};
		}
		return MapBatchResponse(await CultureOperations.GetCultures(request.ConnectionName, request.References, request.Options));
	}

	private async Task<CultureOperationResponse> HandleListOperation(CultureOperationRequest request)
	{
		List<CultureList> list = await CultureOperations.ListCultures(request.ConnectionName);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}", "CultureOperationsTool", "List", request.ConnectionName, list.Count);
		return new CultureOperationResponse
		{
			Success = true,
			Message = $"Found {list.Count} culture(s)",
			Operation = request.Operation,
			Data = list
		};
	}

	private async Task<CultureOperationResponse> HandleRenameOperation(CultureOperationRequest request)
	{
		if (request.RenameDefinitions == null || !request.RenameDefinitions.Any())
		{
			return new CultureOperationResponse
			{
				Success = false,
				Operation = "Rename",
				Message = "RenameDefinitions is required and must contain at least one culture rename definition"
			};
		}
		return MapBatchResponse(await CultureOperations.RenameCultures(request.ConnectionName, request.RenameDefinitions, request.Options));
	}

	private CultureOperationResponse HandleGetValidNamesOperation(CultureOperationRequest request)
	{
		List<string> validCultureNames = CultureOperations.GetValidCultureNames(request.IncludeNeutralCultures, request.IncludeUserCustomCultures);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}", "CultureOperationsTool", "GetValidNames", request.ConnectionName, validCultureNames.Count);
		return new CultureOperationResponse
		{
			Success = true,
			Message = $"Found {validCultureNames.Count} valid culture name(s)",
			Operation = request.Operation,
			Data = validCultureNames
		};
	}

	private CultureOperationResponse HandleGetValidDetailsOperation(CultureOperationRequest request)
	{
		List<CultureDetails> validCultureDetails = CultureOperations.GetValidCultureDetails(request.IncludeNeutralCultures, request.IncludeUserCustomCultures);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}", "CultureOperationsTool", "GetValidDetails", request.ConnectionName, validCultureDetails.Count);
		return new CultureOperationResponse
		{
			Success = true,
			Message = $"Found {validCultureDetails.Count} valid culture(s) with details",
			Operation = request.Operation,
			Data = validCultureDetails
		};
	}

	private CultureOperationResponse HandleGetDetailsByNameOperation(CultureOperationRequest request)
	{
		string text = request.References?.FirstOrDefault()?.Name;
		if (string.IsNullOrEmpty(text))
		{
			return new CultureOperationResponse
			{
				Success = false,
				Operation = "GetDetailsByName",
				Message = "References is required with at least one culture name"
			};
		}
		CultureDetails cultureDetailsByName = CultureOperations.GetCultureDetailsByName(text);
		if (cultureDetailsByName == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Culture '" + text + "' not found or invalid", ErrorSource.User);
		}
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "CultureOperationsTool", "GetDetailsByName", request.ConnectionName);
		return new CultureOperationResponse
		{
			Success = true,
			Message = "Culture details retrieved for '" + text + "'",
			Operation = request.Operation,
			Data = cultureDetailsByName
		};
	}

	private CultureOperationResponse HandleGetDetailsByLCIDOperation(CultureOperationRequest request)
	{
		CultureDetails cultureDetailsByLCID = CultureOperations.GetCultureDetailsByLCID(request.LCID.Value);
		if (cultureDetailsByLCID == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Culture with LCID '{request.LCID}' not found or invalid", ErrorSource.User);
		}
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, LCID={LCID}", "CultureOperationsTool", "GetDetailsByLCID", request.ConnectionName, request.LCID);
		return new CultureOperationResponse
		{
			Success = true,
			Message = $"Culture details retrieved for LCID '{request.LCID}'",
			Operation = request.Operation,
			Data = cultureDetailsByLCID
		};
	}

	private async Task<CultureOperationResponse> HandleExportTMDLOperation(CultureOperationRequest request)
	{
		ExportValidationResult validation = ExportValidationHelper.ValidateReferences(request.References, "Culture");
		if (!validation.IsValid)
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new CultureOperationResponse
			{
				Success = false,
				Operation = "ExportTMDL",
				Message = validation.ErrorMessage,
				Help = value
			};
		}
		CultureReference cultureReference = request.References.First();
		ExportValidationResult exportValidationResult = ExportValidationHelper.ValidateName(cultureReference.Name, "Culture");
		if (!exportValidationResult.IsValid)
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value2);
			return new CultureOperationResponse
			{
				Success = false,
				Operation = "ExportTMDL",
				Message = exportValidationResult.ErrorMessage,
				Help = value2
			};
		}
		string cultureName = cultureReference.Name;
		string data = await CultureOperations.ExportTMDL(request.ConnectionName, cultureName, request.TmdlExportOptions ?? new ExportTmdl());
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "CultureOperationsTool", "ExportTMDL", request.ConnectionName);
		string message = ExportValidationHelper.FormatSuccessMessage("Culture", cultureName, validation.WarningMessage);
		List<string> warnings = ((!string.IsNullOrEmpty(validation.WarningMessage)) ? new List<string> { validation.WarningMessage } : null);
		return new CultureOperationResponse
		{
			Success = true,
			Message = message,
			Operation = request.Operation,
			Data = data,
			Warnings = warnings
		};
	}

	private CultureOperationResponse HandleHelpOperation(CultureOperationRequest request, string[] operations)
	{
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Operations={OperationCount}", "CultureOperationsTool", "Help", request.ConnectionName, operations.Length);
		CultureOperationResponse cultureOperationResponse = new CultureOperationResponse();
		cultureOperationResponse.Success = true;
		cultureOperationResponse.Message = "Tool description retrieved successfully";
		cultureOperationResponse.Operation = request.Operation;
		cultureOperationResponse.Help = new
		{
			ToolName = "culture_operations",
			Description = "Perform operations on semantic model cultures.",
			SupportedOperations = operations,
			Examples = toolMetadata.Operations.Where<KeyValuePair<string, OperationMetadata>>((KeyValuePair<string, OperationMetadata> p) => operations.Contains<string>(p.Key, StringComparer.OrdinalIgnoreCase)),
			Notes = new string[8] { "Use the Operation parameter to specify which operation to perform.", "Operations accept lists of items for bulk processing.", "Single-item operations are represented as lists of one.", "Use Options.ContinueOnError to control error handling in batch operations.", "Use Options.UseTransaction to control transactional behavior.", "GetValidNames returns culture names only, GetValidDetails returns full culture information including LCIDs.", "ExportTMDL exports a culture to TMDL format.", "If the request is declined by the user, the operation should be aborted." }
		};
		return cultureOperationResponse;
	}

	private CultureOperationResponse MapBatchResponse(BatchOperationResponse batchResponse)
	{
		CultureOperationResponse cultureOperationResponse = new CultureOperationResponse
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
			cultureOperationResponse.Exceptions.AddRange(batchResponse.Exceptions);
		}
		return cultureOperationResponse;
	}

	private (bool isValid, string? errorMessage) ValidateRequest(string operation, CultureOperationRequest request)
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
		case "GETDETAILSBYNAME":
			if (request.References == null || !request.References.Any() || string.IsNullOrEmpty(request.References.First().Name))
			{
				return (isValid: false, errorMessage: "References with culture name is required for GetDetailsByName operation");
			}
			break;
		case "GETDETAILSBYLCID":
			if (!request.LCID.HasValue)
			{
				return (isValid: false, errorMessage: "LCID is required for GetDetailsByLCID operation");
			}
			break;
		case "EXPORTTMDL":
			if (request.References == null || !request.References.Any() || string.IsNullOrEmpty(request.References.First().Name))
			{
				return (isValid: false, errorMessage: "References with culture name is required for ExportTMDL operation");
			}
			break;
		}
		return (isValid: true, errorMessage: null);
	}
}
