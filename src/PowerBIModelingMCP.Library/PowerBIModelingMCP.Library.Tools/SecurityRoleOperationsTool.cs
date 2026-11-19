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
public class SecurityRoleOperationsTool
{
	public const string ToolName = "security_role_operations";

	private readonly ILogger<SecurityRoleOperationsTool> _logger;

	public static readonly ToolMetadata toolMetadata = new ToolMetadata
	{
		Operations = new Dictionary<string, OperationMetadata>(StringComparer.OrdinalIgnoreCase)
		{
			["Create"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Definitions" },
				Description = "Create one or more model roles.\nMandatory properties: Definitions (list of ModelRoleDefinition objects with Name).\nOptional: Description, ModelPermission, Annotations, ExtendedProperties, Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            { \n                \"Name\": \"SalesRole\",\n                \"ModelPermission\": \"Read\" \n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            { \"Name\": \"SalesRole\", \"ModelPermission\": \"Read\" },\n            { \"Name\": \"ManagerRole\", \"ModelPermission\": \"Read\", \"Description\": \"Role for managers\" }\n        ],\n        \"Options\": { \"ContinueOnError\": false, \"UseTransaction\": true }\n    }\n}" }
			},
			["Update"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Definitions" },
				Description = "Update one or more existing model roles. Names cannot be changed - use Rename operation instead.\nMandatory properties: Definitions (list of ModelRoleDefinition objects with Name).\nOptional: Description, ModelPermission, Annotations, ExtendedProperties, Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Update\",\n        \"Definitions\": [\n            { \n                \"Name\": \"SalesRole\",\n                \"ModelPermission\": \"Read\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Update\",\n        \"Definitions\": [\n            { \"Name\": \"SalesRole\", \"Description\": \"Updated description\" },\n            { \"Name\": \"ManagerRole\", \"ModelPermission\": \"ReadRefresh\" }\n        ],\n        \"Options\": { \"ContinueOnError\": true, \"UseTransaction\": true }\n    }\n}" }
			},
			["Delete"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Delete one or more model roles.\nMandatory properties: References (list of ModelRoleReference objects with Name).\nOptional: Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Delete\",\n        \"References\": [\n            { \"Name\": \"SalesRole\" }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Delete\",\n        \"References\": [\n            { \"Name\": \"SalesRole\" },\n            { \"Name\": \"ManagerRole\" }\n        ],\n        \"Options\": { \"ContinueOnError\": true, \"UseTransaction\": true }\n    }\n}" }
			},
			["Get"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Get details of one or more model roles.\nMandatory properties: References (list of ModelRoleReference objects with Name).\nOptional: Options (ContinueOnError).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Get\",\n        \"References\": [\n            { \"Name\": \"SalesRole\" }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Get\",\n        \"References\": [\n            { \"Name\": \"SalesRole\" },\n            { \"Name\": \"ManagerRole\" }\n        ],\n        \"Options\": { \"ContinueOnError\": true }\n    }\n}" }
			},
			["List"] = new OperationMetadata
			{
				Description = "List all model roles in the model.\nMandatory properties: None.\nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"List\"\n    }\n}" }
			},
			["Rename"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "RenameDefinitions" },
				Description = "Rename one or more model roles.\nMandatory properties: RenameDefinitions (list of ModelRoleRename objects with CurrentName and NewName).\nOptional: Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Rename\",\n        \"RenameDefinitions\": [\n            { \"CurrentName\": \"SalesRole\", \"NewName\": \"NewSalesRole\" }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Rename\",\n        \"RenameDefinitions\": [\n            { \"CurrentName\": \"Role1\", \"NewName\": \"RenamedRole1\" },\n            { \"CurrentName\": \"Role2\", \"NewName\": \"RenamedRole2\" }\n        ],\n        \"Options\": { \"ContinueOnError\": false, \"UseTransaction\": true }\n    }\n}" }
			},
			["CreatePermissions"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "PermissionDefinitions" },
				Description = "Create one or more table permissions for row-level security.\nMandatory properties: PermissionDefinitions (list of TablePermissionDefinition objects with RoleName, TableName).\nOptional: FilterExpression, MetadataPermission, Annotations, ExtendedProperties, Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"CreatePermissions\",\n        \"PermissionDefinitions\": [\n            { \n                \"RoleName\": \"SalesRole\", \n                \"TableName\": \"Sales\",\n                \"FilterExpression\": \"[Email] = USERPRINCIPALNAME()\",\n                \"MetadataPermission\": \"Read\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"CreatePermissions\",\n        \"PermissionDefinitions\": [\n            { \"RoleName\": \"SalesRole\", \"TableName\": \"Sales\", \"FilterExpression\": \"[Region] = \\\"West\\\"\" },\n            { \"RoleName\": \"SalesRole\", \"TableName\": \"Products\", \"FilterExpression\": \"[Category] = \\\"Electronics\\\"\" }\n        ],\n        \"Options\": { \"ContinueOnError\": false, \"UseTransaction\": true }\n    }\n}" }
			},
			["UpdatePermissions"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "PermissionDefinitions" },
				Description = "Update one or more table permissions for row-level security.\nMandatory properties: PermissionDefinitions (list of TablePermissionDefinition objects with RoleName, TableName).\nOptional: FilterExpression, MetadataPermission, Annotations, ExtendedProperties, Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"UpdatePermissions\",\n        \"PermissionDefinitions\": [\n            { \n                \"RoleName\": \"SalesRole\", \n                \"TableName\": \"Sales\",\n                \"FilterExpression\": \"[Email] = USERPRINCIPALNAME()\",\n                \"MetadataPermission\": \"None\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"UpdatePermissions\",\n        \"PermissionDefinitions\": [\n            { \"RoleName\": \"SalesRole\", \"TableName\": \"Sales\", \"FilterExpression\": \"[Region] = \\\"East\\\"\" },\n            { \"RoleName\": \"SalesRole\", \"TableName\": \"Products\", \"FilterExpression\": \"[Category] = \\\"All\\\"\" }\n        ],\n        \"Options\": { \"ContinueOnError\": true, \"UseTransaction\": true }\n    }\n}" }
			},
			["DeletePermissions"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "PermissionReferences" },
				Description = "Delete one or more table permissions.\nMandatory properties: PermissionReferences (list of TablePermissionReference objects with RoleName, TableName).\nOptional: Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"DeletePermissions\",\n        \"PermissionReferences\": [\n            { \"RoleName\": \"SalesRole\", \"TableName\": \"Sales\" }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"DeletePermissions\",\n        \"PermissionReferences\": [\n            { \"RoleName\": \"SalesRole\", \"TableName\": \"Sales\" },\n            { \"RoleName\": \"SalesRole\", \"TableName\": \"Products\" }\n        ],\n        \"Options\": { \"ContinueOnError\": true, \"UseTransaction\": true }\n    }\n}" }
			},
			["GetPermissions"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "PermissionReferences" },
				Description = "Get one or more table permissions.\nMandatory properties: PermissionReferences (list of TablePermissionReference objects with RoleName, TableName).\nOptional: Options (ContinueOnError).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"GetPermissions\",\n        \"PermissionReferences\": [\n            { \"RoleName\": \"SalesRole\", \"TableName\": \"Sales\" }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"GetPermissions\",\n        \"PermissionReferences\": [\n            { \"RoleName\": \"SalesRole\", \"TableName\": \"Sales\" },\n            { \"RoleName\": \"SalesRole\", \"TableName\": \"Products\" }\n        ],\n        \"Options\": { \"ContinueOnError\": true }\n    }\n}" }
			},
			["ListPermissions"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "RoleName" },
				Description = "Get all table permissions for a role.\nMandatory properties: RoleName.\nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ListPermissions\",\n        \"RoleName\": \"SalesRole\"\n    }\n}" }
			},
			["GetEffectivePermissions"] = new OperationMetadata
			{
				Description = "Get effective permissions for all roles.\nMandatory properties: None.\nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"GetEffectivePermissions\"\n    }\n}" }
			},
			["ExportTMDL"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Export role to TMDL (YAML-like syntax) format. TMDL is a human-readable, declarative format for semantic models.\nMandatory properties: References (list with at least one ModelRoleReference containing Name).\nOptional: TmdlExportOptions (TmdlSerializationOptions.IncludeChildren defaults to false).\nNote: Only the first reference is used; additional references are ignored with a warning.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ExportTMDL\",\n        \"TmdlExportOptions\": {\n            \"TmdlSerializationOptions\": {\n                \"IncludeChildren\": true\n            }\n        },\n        \"References\": [\n            { \"Name\": \"SalesRole\" }\n        ]\n    }\n}" }
			},
			["ExportTMSL"] = new OperationMetadata
			{
				RequiredParams = new string[2] { "References", "TmslExportOptions" },
				Description = "Export role to TMSL (JSON syntax) script format with specified operation type. TMSL generates executable JSON scripts for role operations. Supports Create, CreateOrReplace, Alter, Delete (Refresh not supported for roles).\nMandatory properties: References (list with at least one ModelRoleReference containing Name), TmslExportOptions (with TmslOperationType).\nOptional: IncludeRestricted.\nNote: Only the first reference is used; additional references are ignored with a warning.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ExportTMSL\",\n        \"References\": [\n            { \"Name\": \"SalesRole\" }\n        ],\n        \"TmslExportOptions\": {\n            \"TmslOperationType\": \"CreateOrReplace\",\n            \"IncludeRestricted\": false\n        }\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"ExportTMSL\",\n        \"References\": [\n            { \"Name\": \"SalesRole\" }\n        ],\n        \"TmslExportOptions\": {\n            \"TmslOperationType\": \"Delete\"\n        }\n    }\n}" }
			},
			["Help"] = new OperationMetadata
			{
				Description = "Describe the tool and its operations.\nMandatory properties: None.\nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Help\"\n    }\n}" }
			}
		}
	};

	public SecurityRoleOperationsTool(ILogger<SecurityRoleOperationsTool> logger)
	{
		_logger = logger;
	}

	[McpServerTool(Name = "security_role_operations", Title = "Security Role Operations", ReadOnly = false, Destructive = true)]
	[YamlToolDescription("security_role_operations")]
	public async Task<CallToolResult> ExecuteSecurityRoleOperation(McpServer mcpServer, SecurityRoleOperationRequest request, IWriteGuard writeGuard)
	{
		_logger.LogDebug("Executing {ToolName}.{Operation}: Connection={ConnectionName}", "SecurityRoleOperationsTool", request.Operation, request.ConnectionName ?? "(last used)");
		string[] validOperations = new string[15]
		{
			"CREATE", "UPDATE", "DELETE", "GET", "LIST", "RENAME", "CREATEPERMISSIONS", "UPDATEPERMISSIONS", "DELETEPERMISSIONS", "GETPERMISSIONS",
			"LISTPERMISSIONS", "GETEFFECTIVEPERMISSIONS", "EXPORTTMDL", "EXPORTTMSL", "HELP"
		};
		string[] writeOperations = new string[7] { "CREATE", "UPDATE", "DELETE", "RENAME", "CREATEPERMISSIONS", "UPDATEPERMISSIONS", "DELETEPERMISSIONS" };
		string op = request.Operation.ToUpperInvariant();
		ToolCallAnnotations annotations = ToolCallAnnotations.Create("security_role_operations", request.Operation, !Enumerable.Contains(writeOperations, op));
		CallToolResult result = null;
		try
		{
			if (!Enumerable.Contains(validOperations, op))
			{
				_logger.LogWarning("Invalid operation '{Operation}' requested for {ToolName}. Valid operations: {ValidOperations}", request.Operation, "SecurityRoleOperationsTool", string.Join(", ", validOperations));
				CallToolResult result2;
				result = (result2 = CallToolResultHelper.FromResponse(SecurityRoleOperationResponse.Forbidden(request.Operation, "Invalid operation: " + request.Operation + ". Supported operations: " + string.Join(", ", validOperations)), annotations));
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
					_logger.LogWarning("{ToolName}.{Operation} blocked by write guard: {Reason}", "SecurityRoleOperationsTool", request.Operation, writeOperationResult.Message);
					CallToolResult result2;
					result = (result2 = CallToolResultHelper.FromResponse(new SecurityRoleOperationResponse
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
				"CREATEPERMISSIONS" => CallToolResultHelper.FromResponse(await HandleCreatePermissionsOperation(request), annotations, null, minimalSuccessPayload: true), 
				"UPDATEPERMISSIONS" => CallToolResultHelper.FromResponse(await HandleUpdatePermissionsOperation(request), annotations, null, minimalSuccessPayload: true), 
				"DELETEPERMISSIONS" => CallToolResultHelper.FromResponse(await HandleDeletePermissionsOperation(request), annotations, null, minimalSuccessPayload: true), 
				"GETPERMISSIONS" => CallToolResultHelper.FromResponse(await HandleGetPermissionsOperation(request), annotations), 
				"LISTPERMISSIONS" => CallToolResultHelper.FromResponse(await HandleListPermissionsOperation(request), annotations), 
				"GETEFFECTIVEPERMISSIONS" => CallToolResultHelper.FromResponse(await HandleGetEffectivePermissionsOperation(request), annotations), 
				"EXPORTTMDL" => CallToolResultHelper.FromExportResponse(await HandleExportTMDLOperation(request), (request.References?.FirstOrDefault()?.Name ?? "role") + ".tmdl", "text/plain", annotations), 
				"EXPORTTMSL" => CallToolResultHelper.FromExportResponse(await HandleExportTMSLOperation(request), (request.References?.FirstOrDefault()?.Name ?? "role") + ".json", "text/plain", annotations), 
				"HELP" => CallToolResultHelper.FromResponse(await HandleHelpOperation(request, isWriteEnabled ? validOperations : validOperations.Except(writeOperations).ToArray()), annotations), 
				_ => CallToolResultHelper.FromResponse(SecurityRoleOperationResponse.Forbidden(request.Operation, "Operation " + request.Operation + " is not implemented"), annotations), 
			});
			return result3;
		}
		catch (Exception ex)
		{
			_logger.LogOperationError("SecurityRoleOperationsTool", request.Operation, ex);
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			string message = op switch
			{
				"CREATE" => "Error executing Create operation: " + ex.GetErrorMessage(), 
				"UPDATE" => "Error executing Update operation: " + ex.GetErrorMessage(), 
				"DELETE" => "Error executing Delete operation: " + ex.GetErrorMessage(), 
				"GET" => "Error executing Get operation: " + ex.GetErrorMessage(), 
				"LIST" => "Error listing roles: " + ex.GetErrorMessage(), 
				"RENAME" => "Error executing Rename operation: " + ex.GetErrorMessage(), 
				"CREATEPERMISSIONS" => "Error executing CreatePermissions operation: " + ex.GetErrorMessage(), 
				"UPDATEPERMISSIONS" => "Error executing UpdatePermissions operation: " + ex.GetErrorMessage(), 
				"DELETEPERMISSIONS" => "Error executing DeletePermissions operation: " + ex.GetErrorMessage(), 
				"GETPERMISSIONS" => "Error executing GetPermissions operation: " + ex.GetErrorMessage(), 
				"LISTPERMISSIONS" => "Error getting table permissions: " + ex.GetErrorMessage(), 
				"GETEFFECTIVEPERMISSIONS" => "Error getting effective permissions: " + ex.GetErrorMessage(), 
				"EXPORTTMDL" => "Failed to export role TMDL: " + ex.GetErrorMessage(), 
				"EXPORTTMSL" => "Failed to export role TMSL: " + ex.GetErrorMessage(), 
				_ => "Error executing security operation: " + ex.GetErrorMessage(), 
			};
			CallToolResult result2;
			result = (result2 = CallToolResultHelper.FromResponse(new SecurityRoleOperationResponse
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

	private async Task<SecurityRoleOperationResponse> HandleCreateOperation(SecurityRoleOperationRequest request)
	{
		if (request.Definitions == null || !request.Definitions.Any())
		{
			return new SecurityRoleOperationResponse
			{
				Success = false,
				Operation = "Create",
				Message = "Definitions is required and must contain at least one role definition"
			};
		}
		return MapBatchResponse(await SecurityRoleOperations.CreateModelRoles(request.ConnectionName, request.Definitions, request.Options));
	}

	private async Task<SecurityRoleOperationResponse> HandleUpdateOperation(SecurityRoleOperationRequest request)
	{
		if (request.Definitions == null || !request.Definitions.Any())
		{
			return new SecurityRoleOperationResponse
			{
				Success = false,
				Operation = "Update",
				Message = "Definitions is required and must contain at least one role definition"
			};
		}
		return MapBatchResponse(await SecurityRoleOperations.UpdateModelRoles(request.ConnectionName, request.Definitions, request.Options));
	}

	private async Task<SecurityRoleOperationResponse> HandleDeleteOperation(SecurityRoleOperationRequest request)
	{
		if (request.References == null || !request.References.Any())
		{
			return new SecurityRoleOperationResponse
			{
				Success = false,
				Operation = "Delete",
				Message = "References is required and must contain at least one role reference"
			};
		}
		return MapBatchResponse(await SecurityRoleOperations.DeleteModelRoles(request.ConnectionName, request.References, request.Options));
	}

	private async Task<SecurityRoleOperationResponse> HandleGetOperation(SecurityRoleOperationRequest request)
	{
		if (request.References == null || !request.References.Any())
		{
			return new SecurityRoleOperationResponse
			{
				Success = false,
				Operation = "Get",
				Message = "References is required and must contain at least one role reference"
			};
		}
		return MapBatchResponse(await SecurityRoleOperations.GetModelRoles(request.ConnectionName, request.References, request.Options));
	}

	private async Task<SecurityRoleOperationResponse> HandleListOperation(SecurityRoleOperationRequest request)
	{
		List<ModelRoleList> list = await SecurityRoleOperations.ListModelRoles(request.ConnectionName);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}", "SecurityRoleOperationsTool", request.Operation, request.ConnectionName, list.Count);
		return new SecurityRoleOperationResponse
		{
			Success = true,
			Message = $"Found {list.Count} roles",
			Operation = request.Operation,
			Data = list
		};
	}

	private async Task<SecurityRoleOperationResponse> HandleRenameOperation(SecurityRoleOperationRequest request)
	{
		if (request.RenameDefinitions == null || !request.RenameDefinitions.Any())
		{
			return new SecurityRoleOperationResponse
			{
				Success = false,
				Operation = "Rename",
				Message = "RenameDefinitions is required and must contain at least one role rename definition"
			};
		}
		return MapBatchResponse(await SecurityRoleOperations.RenameModelRoles(request.ConnectionName, request.RenameDefinitions, request.Options));
	}

	private async Task<SecurityRoleOperationResponse> HandleExportTMDLOperation(SecurityRoleOperationRequest request)
	{
		ExportValidationResult validation = ExportValidationHelper.ValidateReferences(request.References, "ModelRole");
		if (!validation.IsValid)
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new SecurityRoleOperationResponse
			{
				Success = false,
				Operation = "ExportTMDL",
				Message = validation.ErrorMessage,
				Help = value
			};
		}
		ModelRoleReference modelRoleReference = request.References.First();
		ExportValidationResult exportValidationResult = ExportValidationHelper.ValidateName(modelRoleReference.Name, "ModelRole");
		if (!exportValidationResult.IsValid)
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value2);
			return new SecurityRoleOperationResponse
			{
				Success = false,
				Operation = "ExportTMDL",
				Message = exportValidationResult.ErrorMessage,
				Help = value2
			};
		}
		string roleName = modelRoleReference.Name;
		TmdlExportResult tmdlExportResult = await SecurityRoleOperations.ExportTMDL(request.ConnectionName, roleName, request.TmdlExportOptions ?? new ExportTmdl());
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "SecurityRoleOperationsTool", request.Operation, request.ConnectionName);
		string text = ExportValidationHelper.FormatSuccessMessage("Role", roleName, validation.WarningMessage);
		List<string> warnings = ((!string.IsNullOrEmpty(validation.WarningMessage)) ? new List<string> { validation.WarningMessage } : null);
		return new SecurityRoleOperationResponse
		{
			Success = tmdlExportResult.Success,
			Message = (tmdlExportResult.Success ? text : (tmdlExportResult.ErrorMessage ?? "Unknown error")),
			ErrorSource = tmdlExportResult.ErrorSource,
			Operation = request.Operation,
			Data = tmdlExportResult.Content,
			Warnings = warnings
		};
	}

	private async Task<SecurityRoleOperationResponse> HandleExportTMSLOperation(SecurityRoleOperationRequest request)
	{
		string warningMessage;
		string roleName = GetRoleNameForExport(request, out warningMessage);
		if (string.IsNullOrEmpty(roleName))
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new SecurityRoleOperationResponse
			{
				Success = false,
				Operation = "ExportTMSL",
				Message = "References is required with at least one ModelRoleReference containing a Name for ExportTMSL operation",
				Help = value
			};
		}
		if (request.TmslExportOptions == null)
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value2);
			return new SecurityRoleOperationResponse
			{
				Success = false,
				Operation = "ExportTMSL",
				Message = "TmslExportOptions is required for ExportTMSL operation",
				Help = value2
			};
		}
		TmslExportResult tmslExportResult = await SecurityRoleOperations.ExportTMSL(request.ConnectionName, roleName, request.TmslExportOptions);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, OperationType={OperationType}", "SecurityRoleOperationsTool", request.Operation, request.ConnectionName, tmslExportResult.OperationType);
		string message = (tmslExportResult.Success ? $"{ExportValidationHelper.FormatTmslSuccessMessage("Role", roleName, tmslExportResult.OperationType.ToString(), warningMessage)} Generated at: {tmslExportResult.GeneratedAt}" : (tmslExportResult.ErrorMessage ?? "Unknown error"));
		List<string> warnings = ((!string.IsNullOrEmpty(warningMessage)) ? new List<string> { warningMessage } : null);
		return new SecurityRoleOperationResponse
		{
			Success = tmslExportResult.Success,
			Message = message,
			ErrorSource = tmslExportResult.ErrorSource,
			Operation = request.Operation,
			Data = tmslExportResult,
			Warnings = warnings
		};
	}

	private static string? GetRoleNameForExport(SecurityRoleOperationRequest request, out string? warningMessage)
	{
		warningMessage = null;
		if (request.References != null && request.References.Any())
		{
			ExportValidationResult exportValidationResult = ExportValidationHelper.ValidateReferences(request.References, "Role");
			warningMessage = exportValidationResult.WarningMessage;
			return request.References.First().Name;
		}
		return null;
	}

	private async Task<SecurityRoleOperationResponse> HandleCreatePermissionsOperation(SecurityRoleOperationRequest request)
	{
		if (request.PermissionDefinitions == null || !request.PermissionDefinitions.Any())
		{
			return new SecurityRoleOperationResponse
			{
				Success = false,
				Operation = "CreatePermissions",
				Message = "PermissionDefinitions is required and must contain at least one table permission definition"
			};
		}
		return MapBatchResponse(await SecurityRoleOperations.CreateTablePermissions(request.ConnectionName, request.PermissionDefinitions, request.Options));
	}

	private async Task<SecurityRoleOperationResponse> HandleUpdatePermissionsOperation(SecurityRoleOperationRequest request)
	{
		if (request.PermissionDefinitions == null || !request.PermissionDefinitions.Any())
		{
			return new SecurityRoleOperationResponse
			{
				Success = false,
				Operation = "UpdatePermissions",
				Message = "PermissionDefinitions is required and must contain at least one table permission definition"
			};
		}
		return MapBatchResponse(await SecurityRoleOperations.UpdateTablePermissions(request.ConnectionName, request.PermissionDefinitions, request.Options));
	}

	private async Task<SecurityRoleOperationResponse> HandleDeletePermissionsOperation(SecurityRoleOperationRequest request)
	{
		if (request.PermissionReferences == null || !request.PermissionReferences.Any())
		{
			return new SecurityRoleOperationResponse
			{
				Success = false,
				Operation = "DeletePermissions",
				Message = "PermissionReferences is required and must contain at least one table permission reference"
			};
		}
		return MapBatchResponse(await SecurityRoleOperations.DeleteTablePermissions(request.ConnectionName, request.PermissionReferences, request.Options));
	}

	private async Task<SecurityRoleOperationResponse> HandleGetPermissionsOperation(SecurityRoleOperationRequest request)
	{
		if (request.PermissionReferences == null || !request.PermissionReferences.Any())
		{
			return new SecurityRoleOperationResponse
			{
				Success = false,
				Operation = "GetPermissions",
				Message = "PermissionReferences is required and must contain at least one table permission reference"
			};
		}
		return MapBatchResponse(await SecurityRoleOperations.GetTablePermissionsById(request.ConnectionName, request.PermissionReferences, request.Options));
	}

	private async Task<SecurityRoleOperationResponse> HandleListPermissionsOperation(SecurityRoleOperationRequest request)
	{
		string roleName = request.PermissionFilter?.RoleName;
		if (string.IsNullOrWhiteSpace(roleName))
		{
			_logger.LogWarning("PermissionFilter.RoleName is required for ListPermissions operation");
			return new SecurityRoleOperationResponse
			{
				Success = false,
				Message = "PermissionFilter.RoleName is required for ListPermissions operation",
				Operation = request.Operation,
				Help = toolMetadata.Operations.GetValueOrDefault("ListPermissions")
			};
		}
		List<Dictionary<string, string>> list = await SecurityRoleOperations.GetTablePermissions(request.ConnectionName, roleName);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}", "SecurityRoleOperationsTool", request.Operation, request.ConnectionName, list.Count);
		return new SecurityRoleOperationResponse
		{
			Success = true,
			Message = $"Found {list.Count} table permissions for role '{roleName}'",
			Operation = request.Operation,
			Data = list
		};
	}

	private async Task<SecurityRoleOperationResponse> HandleGetEffectivePermissionsOperation(SecurityRoleOperationRequest request)
	{
		List<Dictionary<string, object>> list = await SecurityRoleOperations.GetEffectivePermissions(request.ConnectionName);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}", "SecurityRoleOperationsTool", request.Operation, request.ConnectionName, list.Count);
		return new SecurityRoleOperationResponse
		{
			Success = true,
			Message = $"Found effective permissions for {list.Count} roles",
			Operation = request.Operation,
			Data = list
		};
	}

	private Task<SecurityRoleOperationResponse> HandleHelpOperation(SecurityRoleOperationRequest request, string[] operations)
	{
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Operations={OperationCount}", "SecurityRoleOperationsTool", request.Operation, request.ConnectionName, operations.Length);
		SecurityRoleOperationResponse securityRoleOperationResponse = new SecurityRoleOperationResponse();
		securityRoleOperationResponse.Success = true;
		securityRoleOperationResponse.Message = "Tool description retrieved successfully";
		securityRoleOperationResponse.Operation = request.Operation;
		securityRoleOperationResponse.Help = new
		{
			ToolName = "security_role_operations",
			Description = "Perform operations on semantic model security roles and table permissions.",
			SupportedOperations = operations,
			Examples = toolMetadata.Operations.Where<KeyValuePair<string, OperationMetadata>>((KeyValuePair<string, OperationMetadata> p) => operations.Contains<string>(p.Key, StringComparer.OrdinalIgnoreCase)),
			Notes = new string[6] { "Use the Operation parameter to specify which operation to perform.", "Operations accept lists of items for bulk processing.", "Single-item operations are represented as lists of one.", "Use Options.ContinueOnError to control error handling in batch operations.", "Use Options.UseTransaction to control transactional behavior.", "If the request is declined by the user, the operation should be aborted." }
		};
		return Task.FromResult(securityRoleOperationResponse);
	}

	private SecurityRoleOperationResponse MapBatchResponse(BatchOperationResponse batchResponse)
	{
		SecurityRoleOperationResponse securityRoleOperationResponse = new SecurityRoleOperationResponse
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
			securityRoleOperationResponse.Exceptions.AddRange(batchResponse.Exceptions);
		}
		return securityRoleOperationResponse;
	}

	private (bool isValid, string? errorMessage) ValidateRequest(string operation, SecurityRoleOperationRequest request)
	{
		switch (operation.ToUpperInvariant())
		{
		case "CREATE":
			if (request.Definitions == null || !request.Definitions.Any())
			{
				string text3 = "Definitions is required for Create operation";
				_logger.LogWarning(text3);
				return (isValid: false, errorMessage: text3);
			}
			break;
		case "UPDATE":
			if (request.Definitions == null || !request.Definitions.Any())
			{
				string text7 = "Definitions is required for Update operation";
				_logger.LogWarning(text7);
				return (isValid: false, errorMessage: text7);
			}
			break;
		case "DELETE":
			if (request.References == null || !request.References.Any())
			{
				string text5 = "References is required for Delete operation";
				_logger.LogWarning(text5);
				return (isValid: false, errorMessage: text5);
			}
			break;
		case "GET":
			if (request.References == null || !request.References.Any())
			{
				string text4 = "References is required for Get operation";
				_logger.LogWarning(text4);
				return (isValid: false, errorMessage: text4);
			}
			break;
		case "RENAME":
			if (request.RenameDefinitions == null || !request.RenameDefinitions.Any())
			{
				string text10 = "RenameDefinitions is required for Rename operation";
				_logger.LogWarning(text10);
				return (isValid: false, errorMessage: text10);
			}
			break;
		case "CREATEPERMISSIONS":
			if (request.PermissionDefinitions == null || !request.PermissionDefinitions.Any())
			{
				string text8 = "PermissionDefinitions is required for CreatePermissions operation";
				_logger.LogWarning(text8);
				return (isValid: false, errorMessage: text8);
			}
			break;
		case "UPDATEPERMISSIONS":
			if (request.PermissionDefinitions == null || !request.PermissionDefinitions.Any())
			{
				string text2 = "PermissionDefinitions is required for UpdatePermissions operation";
				_logger.LogWarning(text2);
				return (isValid: false, errorMessage: text2);
			}
			break;
		case "DELETEPERMISSIONS":
			if (request.PermissionReferences == null || !request.PermissionReferences.Any())
			{
				string text9 = "PermissionReferences is required for DeletePermissions operation";
				_logger.LogWarning(text9);
				return (isValid: false, errorMessage: text9);
			}
			break;
		case "GETPERMISSIONS":
			if (request.PermissionReferences == null || !request.PermissionReferences.Any())
			{
				string text6 = "PermissionReferences is required for GetPermissions operation";
				_logger.LogWarning(text6);
				return (isValid: false, errorMessage: text6);
			}
			break;
		case "LISTPERMISSIONS":
			if (string.IsNullOrEmpty(request.PermissionFilter?.RoleName))
			{
				string text = "PermissionFilter.RoleName is required for ListPermissions operation";
				_logger.LogWarning(text);
				return (isValid: false, errorMessage: text);
			}
			break;
		}
		return (isValid: true, errorMessage: null);
	}
}
