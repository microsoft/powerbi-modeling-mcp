using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
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
public class CalculationGroupOperationsTool
{
	public const string ToolName = "calculation_group_operations";

	private readonly IWriteGuard _writeGuard;

	private readonly ILogger<CalculationGroupOperationsTool> _logger;

	public static readonly ToolMetadata toolMetadata = new ToolMetadata
	{
		Operations = new Dictionary<string, OperationMetadata>(StringComparer.OrdinalIgnoreCase)
		{
			["CreateGroup"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "GroupDefinitions" },
				Description = "Create one or more calculation groups with optional initial calculation items.\nMandatory properties: GroupDefinitions (list of CalculationGroupDefinition objects with Name).\nOptional per group: Description, IsHidden, Precedence, MultipleOrEmptySelectionExpression, NoSelectionExpression, CalculationItems, Annotations.\nNote: If MultipleOrEmptySelectionExpression or NoSelectionExpression are provided, their Expression property is mandatory.\nBatch options: Set Options.ContinueOnError to continue processing remaining items after failures (default: false). Set Options.UseTransaction for transactional behavior (default: true).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"CreateGroup\",\n        \"GroupDefinitions\": [\n            { \n                \"Name\": \"Time Intelligence\",\n                \"Precedence\": 1,\n                \"NoSelectionExpression\": {\n                    \"Expression\": \"SELECTEDMEASURE()\"\n                }\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"CreateGroup\",\n        \"GroupDefinitions\": [\n            { \n                \"Name\": \"Time Intelligence\",\n                \"Precedence\": 1,\n                \"CalculationItems\": [\n                    {\n                        \"Name\": \"YTD\",\n                        \"Expression\": \"CALCULATE(SELECTEDMEASURE(), DATESYTD('Date'[Date]))\",\n                        \"Ordinal\": 0\n                    }\n                ]\n            }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": false,\n            \"UseTransaction\": true\n        }\n    }\n}" }
			},
			["UpdateGroup"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "GroupDefinitions" },
				Description = "Update one or more existing calculation groups. Names cannot be changed and must use the RenameGroup operation instead.\nMandatory properties: GroupDefinitions (list of CalculationGroupDefinition objects with Name).\nOptional: Description, IsHidden, Precedence, MultipleOrEmptySelectionExpression, NoSelectionExpression, Annotations.\nBatch options: Set Options.ContinueOnError to continue processing remaining items after failures (default: false). Set Options.UseTransaction for transactional behavior (default: true).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"UpdateGroup\",\n        \"GroupDefinitions\": [\n            { \n                \"Name\": \"Time Intelligence\",\n                \"Precedence\": 2,\n                \"NoSelectionExpression\": {\n                    \"Expression\": \"SELECTEDMEASURE()\"\n                }\n            }\n        ]\n    }\n}" }
			},
			["DeleteGroup"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "GroupReferences" },
				Description = "Delete one or more calculation groups and all their calculation items.\nMandatory properties: GroupReferences (list of CalculationGroupReference objects with Name).\nBatch options: Set Options.ContinueOnError to continue processing remaining items after failures (default: false). Set Options.UseTransaction for transactional behavior (default: true).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"DeleteGroup\",\n        \"GroupReferences\": [\n            { \"Name\": \"ObsoleteGroup\" }\n        ]\n    }\n}" }
			},
			["GetGroup"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "GroupReferences" },
				Description = "Get details of one or more calculation groups including all calculation items and metadata.\nMandatory properties: GroupReferences (list of CalculationGroupReference objects with Name).\nBatch options: Set Options.ContinueOnError to continue processing remaining items after failures (default: false).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"GetGroup\",\n        \"GroupReferences\": [\n            { \"Name\": \"Time Intelligence\" }\n        ]\n    }\n}" }
			},
			["ListGroups"] = new OperationMetadata
			{
				Description = "List all calculation groups in the model with basic information.\nMandatory properties: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ListGroups\"\n    }\n}" }
			},
			["RenameGroup"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "RenameGroupDefinitions" },
				Description = "Rename one or more calculation groups to new names.\nMandatory properties: RenameGroupDefinitions (list of CalculationGroupRename objects with CurrentName, NewName).\nBatch options: Set Options.ContinueOnError to continue processing remaining items after failures (default: false). Set Options.UseTransaction for transactional behavior (default: true).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"RenameGroup\",\n        \"RenameGroupDefinitions\": [\n            { \n                \"CurrentName\": \"OldGroup\", \n                \"NewName\": \"NewGroup\"\n            }\n        ]\n    }\n}" }
			},
			["CreateItems"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "ItemDefinitions" },
				Description = "Create one or more calculation items in existing calculation groups.\nMandatory properties: ItemDefinitions (list of CalculationItemDefinition objects with Name, Expression, CalculationGroupName).\nOptional: Description, Ordinal, FormatStringExpression, Annotations.\nBatch options: Set Options.ContinueOnError to continue processing remaining items after failures (default: false). Set Options.UseTransaction for transactional behavior (default: true).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"CreateItems\",\n        \"ItemDefinitions\": [\n            { \n                \"Name\": \"YTD\", \n                \"Expression\": \"CALCULATE(SELECTEDMEASURE(), DATESYTD('Date'[Date]))\",\n                \"Ordinal\": 1,\n                \"CalculationGroupName\": \"Time Intelligence\" \n            }\n        ]\n    }\n}" }
			},
			["UpdateItems"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "ItemDefinitions" },
				Description = "Update one or more calculation items. Names cannot be changed and must use the RenameItems operation instead.\nMandatory properties: ItemDefinitions (list of CalculationItemDefinition objects with Name, CalculationGroupName).\nOptional: Description, Expression, Ordinal, FormatStringExpression, Annotations.\nNote: Expression cannot be empty string if provided.\nBatch options: Set Options.ContinueOnError to continue processing remaining items after failures (default: false). Set Options.UseTransaction for transactional behavior (default: true).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"UpdateItems\",\n        \"ItemDefinitions\": [\n            { \n                \"Name\": \"YTD\", \n                \"Expression\": \"CALCULATE(SELECTEDMEASURE(), DATESYTD('Date'[Date]))\",\n                \"Ordinal\": 1,\n                \"CalculationGroupName\": \"Time Intelligence\" \n            }\n        ]\n    }\n}" }
			},
			["DeleteItems"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "ItemReferences" },
				Description = "Delete one or more calculation items from calculation groups.\nMandatory properties: ItemReferences (list of CalculationItemReference objects with Name, CalculationGroupName).\nBatch options: Set Options.ContinueOnError to continue processing remaining items after failures (default: false). Set Options.UseTransaction for transactional behavior (default: true).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"DeleteItems\",\n        \"ItemReferences\": [\n            {\n                \"CalculationGroupName\": \"Time Intelligence\",\n                \"Name\": \"ObsoleteItem\"\n            }\n        ]\n    }\n}" }
			},
			["GetItems"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "ItemReferences" },
				Description = "Get details of one or more calculation items.\nMandatory properties: ItemReferences (list of CalculationItemReference objects with CalculationGroupName, Name).\nBatch options: Set Options.ContinueOnError to continue processing remaining items after failures (default: false).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"GetItems\",\n        \"ItemReferences\": [\n            {\n                \"CalculationGroupName\": \"Time Intelligence\",\n                \"Name\": \"YTD\"\n            }\n        ]\n    }\n}" }
			},
			["ListItems"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "ItemFilter" },
				Description = "List all calculation items in a calculation group with basic information.\nMandatory properties: ItemFilter.CalculationGroupName.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ListItems\",\n        \"ItemFilter\": {\n            \"CalculationGroupName\": \"Time Intelligence\"\n        }\n    }\n}" }
			},
			["RenameItems"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "RenameItemsDefinitions" },
				Description = "Rename one or more calculation items to new names.\nMandatory properties: RenameItemsDefinitions (list of CalculationItemRename objects with CalculationGroupName, CurrentName, NewName).\nBatch options: Set Options.ContinueOnError to continue processing remaining items after failures (default: false). Set Options.UseTransaction for transactional behavior (default: true).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"RenameItems\",\n        \"RenameItemsDefinitions\": [\n            {\n                \"CalculationGroupName\": \"Time Intelligence\",\n                \"CurrentName\": \"OldItem\",\n                \"NewName\": \"NewItem\"\n            }\n        ]\n    }\n}" }
			},
			["ReorderItems"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "ReorderDefinitions" },
				Description = "Reorder calculation items in calculation groups by setting their ordinal positions.\nMandatory properties: ReorderDefinitions (list of CalculationItemReorder objects with CalculationGroupName, ItemNamesInOrder).\nNote: ItemNamesInOrder should contain the item names in the desired order.\nBatch options: Set Options.ContinueOnError to continue processing remaining items after failures (default: false). Set Options.UseTransaction for transactional behavior (default: true).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ReorderItems\",\n        \"ReorderDefinitions\": [\n            {\n                \"CalculationGroupName\": \"Time Intelligence\",\n                \"ItemNamesInOrder\": [\"YTD\", \"QTD\", \"MTD\", \"PY\", \"YoY\", \"YoY%\"]\n            }\n        ]\n    }\n}" }
			},
			["ExportTMDL"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "GroupReferences" },
				Description = "Export a calculation group to TMDL format.\nMandatory properties: GroupReferences (list with at least one CalculationGroupReference containing Name).\nOptional: TmdlExportOptions (TmdlSerializationOptions.IncludeChildren defaults to false).\nNote: Only the first reference is used; additional references are ignored with a warning.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ExportTMDL\",\n        \"TmdlExportOptions\": {\n            \"TmdlSerializationOptions\": {\n                \"IncludeChildren\": true\n            }\n        },\n        \"GroupReferences\": [\n            { \"Name\": \"Time Intelligence\" }\n        ]\n    }\n}" }
			},
			["Help"] = new OperationMetadata
			{
				Description = "Describe the tool and its operations with usage examples.\nMandatory properties: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Help\"\n    }\n}" }
			}
		}
	};

	public CalculationGroupOperationsTool(ILogger<CalculationGroupOperationsTool> logger, IWriteGuard writeGuard)
	{
		_logger = logger;
		_writeGuard = writeGuard;
	}

	[McpServerTool(Name = "calculation_group_operations", Title = "Calculation Group Operations", ReadOnly = false, Destructive = true)]
	[YamlToolDescription("calculation_group_operations")]
	public async Task<CallToolResult> ExecuteCalculationGroupOperation(McpServer mcpServer, CalculationGroupOperationRequest request)
	{
		_logger.LogDebug("Executing {ToolName}.{Operation}: Connection={ConnectionName}", "CalculationGroupOperationsTool", request.Operation, request.ConnectionName ?? "(last used)");
		string[] validOperations = new string[15]
		{
			"CREATEGROUP", "UPDATEGROUP", "DELETEGROUP", "GETGROUP", "LISTGROUPS", "RENAMEGROUP", "CREATEITEMS", "UPDATEITEMS", "DELETEITEMS", "GETITEMS",
			"LISTITEMS", "RENAMEITEMS", "REORDERITEMS", "EXPORTTMDL", "HELP"
		};
		string[] writeOperations = new string[9] { "CREATEGROUP", "UPDATEGROUP", "DELETEGROUP", "RENAMEGROUP", "CREATEITEMS", "UPDATEITEMS", "DELETEITEMS", "RENAMEITEMS", "REORDERITEMS" };
		string value = request.Operation.ToUpperInvariant();
		ToolCallAnnotations annotations = ToolCallAnnotations.Create("calculation_group_operations", request.Operation, !Enumerable.Contains(writeOperations, value));
		CallToolResult result = null;
		try
		{
			if (!Enumerable.Contains(validOperations, value))
			{
				_logger.LogWarning("Invalid operation '{Operation}' requested for {ToolName}. Valid operations: {ValidOperations}", request.Operation, "CalculationGroupOperationsTool", string.Join(", ", validOperations));
				CallToolResult result2;
				result = (result2 = CallToolResultHelper.FromResponse(CalculationGroupOperationResponse.Forbidden(request.Operation, "Invalid operation: " + request.Operation + ". Supported operations: " + string.Join(", ", validOperations)), annotations));
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
					_logger.LogWarning("{ToolName}.{Operation} blocked by write guard: {Reason}", "CalculationGroupOperationsTool", request.Operation, writeOperationResult.Message);
					CallToolResult result2;
					result = (result2 = CallToolResultHelper.FromResponse(CalculationGroupOperationResponse.Forbidden(request.Operation, writeOperationResult.Message), annotations));
					return result2;
				}
			}
			bool isWriteEnabled = _writeGuard.IsWriteEnabled;
			CallToolResult result3;
			result = (result3 = request.Operation.ToUpperInvariant() switch
			{
				"CREATEGROUP" => CallToolResultHelper.FromResponse(await HandleCreateGroupOperation(request), annotations, null, minimalSuccessPayload: true), 
				"UPDATEGROUP" => CallToolResultHelper.FromResponse(await HandleUpdateGroupOperation(request), annotations, null, minimalSuccessPayload: true), 
				"DELETEGROUP" => CallToolResultHelper.FromResponse(await HandleDeleteGroupOperation(request), annotations, null, minimalSuccessPayload: true), 
				"GETGROUP" => CallToolResultHelper.FromResponse(await HandleGetGroupOperation(request), annotations), 
				"LISTGROUPS" => CallToolResultHelper.FromResponse(await HandleListGroupsOperation(request), annotations), 
				"RENAMEGROUP" => CallToolResultHelper.FromResponse(await HandleRenameGroupOperation(request), annotations, null, minimalSuccessPayload: true), 
				"CREATEITEMS" => CallToolResultHelper.FromResponse(await HandleCreateItemsOperation(request), annotations, null, minimalSuccessPayload: true), 
				"UPDATEITEMS" => CallToolResultHelper.FromResponse(await HandleUpdateItemsOperation(request), annotations, null, minimalSuccessPayload: true), 
				"DELETEITEMS" => CallToolResultHelper.FromResponse(await HandleDeleteItemsOperation(request), annotations, null, minimalSuccessPayload: true), 
				"GETITEMS" => CallToolResultHelper.FromResponse(await HandleGetItemsOperation(request), annotations), 
				"LISTITEMS" => CallToolResultHelper.FromResponse(await HandleListItemsOperation(request), annotations), 
				"RENAMEITEMS" => CallToolResultHelper.FromResponse(await HandleRenameItemsOperation(request), annotations, null, minimalSuccessPayload: true), 
				"REORDERITEMS" => CallToolResultHelper.FromResponse(await HandleReorderItemsOperation(request), annotations, null, minimalSuccessPayload: true), 
				"EXPORTTMDL" => CallToolResultHelper.FromExportResponse(await HandleExportTMDLOperation(request), (request.GroupReferences?.FirstOrDefault()?.Name ?? "calculationgroup") + ".tmdl", "text/plain", annotations), 
				"HELP" => CallToolResultHelper.FromResponse(HandleHelpOperation(request, isWriteEnabled ? validOperations : validOperations.Except(writeOperations).ToArray()), annotations), 
				_ => CallToolResultHelper.FromResponse(CalculationGroupOperationResponse.Forbidden(request.Operation, "Operation " + request.Operation + " not implemented"), annotations), 
			});
			return result3;
		}
		catch (Exception ex)
		{
			_logger.LogOperationError("CalculationGroupOperationsTool", request.Operation, ex);
			string message = request.Operation.ToUpperInvariant() switch
			{
				"CREATEGROUP" => "Failed to create calculation groups: " + ex.GetErrorMessage(), 
				"UPDATEGROUP" => "Failed to update calculation groups: " + ex.GetErrorMessage(), 
				"DELETEGROUP" => "Failed to delete calculation groups: " + ex.GetErrorMessage(), 
				"GETGROUP" => "Failed to get calculation groups: " + ex.GetErrorMessage(), 
				"LISTGROUPS" => "Failed to list calculation groups: " + ex.GetErrorMessage(), 
				"RENAMEGROUP" => "Failed to rename calculation groups: " + ex.GetErrorMessage(), 
				"CREATEITEMS" => "Failed to create calculation items: " + ex.GetErrorMessage(), 
				"UPDATEITEMS" => "Failed to update calculation items: " + ex.GetErrorMessage(), 
				"DELETEITEMS" => "Failed to delete calculation items: " + ex.GetErrorMessage(), 
				"GETITEMS" => "Failed to get calculation items: " + ex.GetErrorMessage(), 
				"LISTITEMS" => "Error listing calculation items: " + ex.GetErrorMessage(), 
				"RENAMEITEMS" => "Failed to rename calculation items: " + ex.GetErrorMessage(), 
				"REORDERITEMS" => "Error reordering calculation items: " + ex.GetErrorMessage(), 
				"EXPORTTMDL" => "Error getting TMDL for calculation group '" + (request.GroupReferences?.FirstOrDefault()?.Name ?? "(unknown)") + "': " + ex.GetErrorMessage(), 
				_ => "Error executing calculation group operation: " + ex.GetErrorMessage(), 
			};
			CallToolResult result2;
			result = (result2 = CallToolResultHelper.FromResponse(new CalculationGroupOperationResponse
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

	private static CalculationGroupOperationResponse MapBatchResponse(BatchOperationResponse batchResult, string operation)
	{
		CalculationGroupOperationResponse obj = new CalculationGroupOperationResponse
		{
			Success = batchResult.Success,
			Message = batchResult.Message,
			Operation = operation,
			Summary = batchResult.Summary,
			Results = batchResult.Results,
			Warnings = batchResult.Warnings
		};
		obj.Exceptions.AddRange(batchResult.Exceptions);
		return obj;
	}

	private async Task<CalculationGroupOperationResponse> HandleCreateGroupOperation(CalculationGroupOperationRequest request)
	{
		BatchOperationResponse batchOperationResponse = await CalculationGroupOperations.CreateCalculationGroups(request.ConnectionName, request.GroupDefinitions, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, TotalItems={TotalItems}, SuccessCount={SuccessCount}", "CalculationGroupOperationsTool", "CreateGroup", request.ConnectionName, batchOperationResponse.Summary?.TotalItems, batchOperationResponse.Summary?.SuccessCount);
		return MapBatchResponse(batchOperationResponse, request.Operation);
	}

	private async Task<CalculationGroupOperationResponse> HandleUpdateGroupOperation(CalculationGroupOperationRequest request)
	{
		BatchOperationResponse batchOperationResponse = await CalculationGroupOperations.UpdateCalculationGroups(request.ConnectionName, request.GroupDefinitions, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, TotalItems={TotalItems}, SuccessCount={SuccessCount}", "CalculationGroupOperationsTool", "UpdateGroup", request.ConnectionName, batchOperationResponse.Summary?.TotalItems, batchOperationResponse.Summary?.SuccessCount);
		return MapBatchResponse(batchOperationResponse, request.Operation);
	}

	private async Task<CalculationGroupOperationResponse> HandleDeleteGroupOperation(CalculationGroupOperationRequest request)
	{
		BatchOperationResponse batchOperationResponse = await CalculationGroupOperations.DeleteCalculationGroups(request.ConnectionName, request.GroupReferences, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, TotalItems={TotalItems}, SuccessCount={SuccessCount}", "CalculationGroupOperationsTool", "DeleteGroup", request.ConnectionName, batchOperationResponse.Summary?.TotalItems, batchOperationResponse.Summary?.SuccessCount);
		return MapBatchResponse(batchOperationResponse, request.Operation);
	}

	private async Task<CalculationGroupOperationResponse> HandleGetGroupOperation(CalculationGroupOperationRequest request)
	{
		BatchOperationResponse batchOperationResponse = await CalculationGroupOperations.GetCalculationGroups(request.ConnectionName, request.GroupReferences, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, TotalItems={TotalItems}, SuccessCount={SuccessCount}", "CalculationGroupOperationsTool", "GetGroup", request.ConnectionName, batchOperationResponse.Summary?.TotalItems, batchOperationResponse.Summary?.SuccessCount);
		return MapBatchResponse(batchOperationResponse, request.Operation);
	}

	private async Task<CalculationGroupOperationResponse> HandleListGroupsOperation(CalculationGroupOperationRequest request)
	{
		List<CalculationGroupList> list = await CalculationGroupOperations.ListCalculationGroups(request.ConnectionName);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}", "CalculationGroupOperationsTool", "ListGroups", request.ConnectionName, list.Count);
		return new CalculationGroupOperationResponse
		{
			Success = true,
			Message = $"Found {list.Count} calculation groups",
			Operation = request.Operation,
			Data = list
		};
	}

	private async Task<CalculationGroupOperationResponse> HandleRenameGroupOperation(CalculationGroupOperationRequest request)
	{
		BatchOperationResponse batchOperationResponse = await CalculationGroupOperations.RenameCalculationGroups(request.ConnectionName, request.RenameGroupDefinitions, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, TotalItems={TotalItems}, SuccessCount={SuccessCount}", "CalculationGroupOperationsTool", "RenameGroup", request.ConnectionName, batchOperationResponse.Summary?.TotalItems, batchOperationResponse.Summary?.SuccessCount);
		return MapBatchResponse(batchOperationResponse, request.Operation);
	}

	private async Task<CalculationGroupOperationResponse> HandleCreateItemsOperation(CalculationGroupOperationRequest request)
	{
		BatchOperationResponse batchOperationResponse = await CalculationGroupOperations.CreateCalculationItems(request.ConnectionName, request.ItemDefinitions, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, TotalItems={TotalItems}, SuccessCount={SuccessCount}", "CalculationGroupOperationsTool", "CreateItems", request.ConnectionName, batchOperationResponse.Summary?.TotalItems, batchOperationResponse.Summary?.SuccessCount);
		return MapBatchResponse(batchOperationResponse, request.Operation);
	}

	private async Task<CalculationGroupOperationResponse> HandleUpdateItemsOperation(CalculationGroupOperationRequest request)
	{
		BatchOperationResponse batchOperationResponse = await CalculationGroupOperations.UpdateCalculationItems(request.ConnectionName, request.ItemDefinitions, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, TotalItems={TotalItems}, SuccessCount={SuccessCount}", "CalculationGroupOperationsTool", "UpdateItems", request.ConnectionName, batchOperationResponse.Summary?.TotalItems, batchOperationResponse.Summary?.SuccessCount);
		return MapBatchResponse(batchOperationResponse, request.Operation);
	}

	private async Task<CalculationGroupOperationResponse> HandleDeleteItemsOperation(CalculationGroupOperationRequest request)
	{
		BatchOperationResponse batchOperationResponse = await CalculationGroupOperations.DeleteCalculationItems(request.ConnectionName, request.ItemReferences, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, TotalItems={TotalItems}, SuccessCount={SuccessCount}", "CalculationGroupOperationsTool", "DeleteItems", request.ConnectionName, batchOperationResponse.Summary?.TotalItems, batchOperationResponse.Summary?.SuccessCount);
		return MapBatchResponse(batchOperationResponse, request.Operation);
	}

	private async Task<CalculationGroupOperationResponse> HandleGetItemsOperation(CalculationGroupOperationRequest request)
	{
		BatchOperationResponse batchOperationResponse = await CalculationGroupOperations.GetCalculationItems(request.ConnectionName, request.ItemReferences, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, TotalItems={TotalItems}, SuccessCount={SuccessCount}", "CalculationGroupOperationsTool", "GetItems", request.ConnectionName, batchOperationResponse.Summary?.TotalItems, batchOperationResponse.Summary?.SuccessCount);
		return MapBatchResponse(batchOperationResponse, request.Operation);
	}

	private async Task<CalculationGroupOperationResponse> HandleListItemsOperation(CalculationGroupOperationRequest request)
	{
		string calculationGroupName = request.ItemFilter?.CalculationGroupName;
		if (string.IsNullOrWhiteSpace(calculationGroupName))
		{
			_logger.LogWarning("ItemFilter.CalculationGroupName is required for ListItems operation");
			return new CalculationGroupOperationResponse
			{
				Success = false,
				Message = "ItemFilter.CalculationGroupName is required for ListItems operation",
				Operation = request.Operation,
				Help = toolMetadata.Operations.GetValueOrDefault("ListItems")
			};
		}
		List<CalculationItemList> list = await CalculationGroupOperations.ListCalculationItems(request.ConnectionName, calculationGroupName);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}", "CalculationGroupOperationsTool", "ListItems", request.ConnectionName, list.Count);
		return new CalculationGroupOperationResponse
		{
			Success = true,
			Message = $"Found {list.Count} calculation items in calculation group '{calculationGroupName}'",
			Operation = request.Operation,
			Data = list
		};
	}

	private async Task<CalculationGroupOperationResponse> HandleRenameItemsOperation(CalculationGroupOperationRequest request)
	{
		BatchOperationResponse batchOperationResponse = await CalculationGroupOperations.RenameCalculationItems(request.ConnectionName, request.RenameItemsDefinitions, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, TotalItems={TotalItems}, SuccessCount={SuccessCount}", "CalculationGroupOperationsTool", "RenameItems", request.ConnectionName, batchOperationResponse.Summary?.TotalItems, batchOperationResponse.Summary?.SuccessCount);
		return MapBatchResponse(batchOperationResponse, request.Operation);
	}

	private async Task<CalculationGroupOperationResponse> HandleReorderItemsOperation(CalculationGroupOperationRequest request)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<ItemResult> results = new List<ItemResult>();
		List<string> warnings = new List<string>();
		foreach (var (reorderDef, index) in request.ReorderDefinitions.Select((CalculationItemReorder d, int i) => (d: d, i: i)))
		{
			try
			{
				await CalculationGroupOperations.ReorderCalculationItems(request.ConnectionName, reorderDef.CalculationGroupName, reorderDef.ItemNamesInOrder);
				results.Add(new ItemResult
				{
					Index = index,
					Success = true,
					Message = "Reordered items in calculation group '" + reorderDef.CalculationGroupName + "'",
					ItemIdentifier = (reorderDef.CalculationGroupName ?? "")
				});
			}
			catch (Exception ex)
			{
				results.Add(new ItemResult
				{
					Index = index,
					Success = false,
					Message = ex.GetErrorMessage(),
					ItemIdentifier = (reorderDef.CalculationGroupName ?? "")
				});
				if (!request.Options.ContinueOnError)
				{
					throw;
				}
			}
		}
		stopwatch.Stop();
		int num = results.Count((ItemResult r) => r.Success);
		int num2 = results.Count((ItemResult r) => !r.Success);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, TotalItems={TotalItems}, SuccessCount={SuccessCount}", "CalculationGroupOperationsTool", "ReorderItems", request.ConnectionName, results.Count, num);
		return new CalculationGroupOperationResponse
		{
			Success = (num2 == 0),
			Message = ((num2 == 0) ? $"Successfully reordered items in {num} calculation group(s)" : $"Completed with {num2} failure(s) out of {results.Count} reorder operation(s)"),
			Operation = request.Operation,
			Summary = new BatchSummary
			{
				TotalItems = results.Count,
				SuccessCount = num,
				FailureCount = num2,
				ExecutionTime = stopwatch.Elapsed
			},
			Results = results,
			Warnings = ((warnings.Count > 0) ? warnings : null)
		};
	}

	private async Task<CalculationGroupOperationResponse> HandleExportTMDLOperation(CalculationGroupOperationRequest request)
	{
		ExportValidationResult validation = ExportValidationHelper.ValidateReferences(request.GroupReferences, "CalculationGroup");
		if (!validation.IsValid)
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new CalculationGroupOperationResponse
			{
				Success = false,
				Operation = "ExportTMDL",
				Message = validation.ErrorMessage,
				Help = value
			};
		}
		CalculationGroupReference calculationGroupReference = request.GroupReferences.First();
		ExportValidationResult exportValidationResult = ExportValidationHelper.ValidateName(calculationGroupReference.Name, "CalculationGroup");
		if (!exportValidationResult.IsValid)
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value2);
			return new CalculationGroupOperationResponse
			{
				Success = false,
				Operation = "ExportTMDL",
				Message = exportValidationResult.ErrorMessage,
				Help = value2
			};
		}
		string calculationGroupName = calculationGroupReference.Name;
		string data = await CalculationGroupOperations.ExportTMDL(request.ConnectionName, calculationGroupName, request.TmdlExportOptions ?? new ExportTmdl());
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "CalculationGroupOperationsTool", "ExportTMDL", request.ConnectionName);
		string message = ExportValidationHelper.FormatSuccessMessage("Calculation Group", calculationGroupName, validation.WarningMessage);
		List<string> warnings = ((!string.IsNullOrEmpty(validation.WarningMessage)) ? new List<string> { validation.WarningMessage } : null);
		return new CalculationGroupOperationResponse
		{
			Success = true,
			Message = message,
			Operation = request.Operation,
			Data = data,
			Warnings = warnings
		};
	}

	private static string? GetCalculationGroupNameForExport(CalculationGroupOperationRequest request, out string? warningMessage)
	{
		warningMessage = null;
		if (request.GroupReferences != null && request.GroupReferences.Any())
		{
			ExportValidationResult exportValidationResult = ExportValidationHelper.ValidateReferences(request.GroupReferences, "CalculationGroup");
			warningMessage = exportValidationResult.WarningMessage;
			return request.GroupReferences.First().Name;
		}
		return null;
	}

	private CalculationGroupOperationResponse HandleHelpOperation(CalculationGroupOperationRequest request, string[] operations)
	{
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Operations={OperationCount}", "CalculationGroupOperationsTool", "Help", request.ConnectionName, operations.Length);
		CalculationGroupOperationResponse calculationGroupOperationResponse = new CalculationGroupOperationResponse();
		calculationGroupOperationResponse.Success = true;
		calculationGroupOperationResponse.Message = "Tool description retrieved successfully";
		calculationGroupOperationResponse.Operation = request.Operation;
		calculationGroupOperationResponse.Help = new
		{
			ToolName = "calculation_group_operations",
			Description = "Perform batch operations on semantic model calculation groups and calculation items.",
			SupportedOperations = operations,
			Examples = toolMetadata.Operations.Where<KeyValuePair<string, OperationMetadata>>((KeyValuePair<string, OperationMetadata> p) => operations.Contains<string>(p.Key, StringComparer.OrdinalIgnoreCase)),
			Notes = new string[5] { "Use the Operation parameter to specify which operation to perform.", "Most operations support batch processing using plural definition lists (e.g., GroupDefinitions, ItemDefinitions).", "Use Options.ContinueOnError=true to continue processing after failures.", "Use Options.UseTransaction=true for transactional behavior (all-or-nothing).", "Results include Summary (TotalItems, SuccessCount, FailureCount, ExecutionTime) and individual Results per item." }
		};
		return calculationGroupOperationResponse;
	}

	private (bool isValid, string? errorMessage) ValidateRequest(string operation, CalculationGroupOperationRequest request)
	{
		if (!toolMetadata.Operations.TryGetValue(operation, out OperationMetadata value))
		{
			return (isValid: true, errorMessage: null);
		}
		JsonObject requestDict = JsonSerializer.SerializeToNode(request) as JsonObject;
		List<string> list = value.RequiredParams.Where((string p) => requestDict != null && requestDict[p] == null).ToList();
		List<string> list2 = value.ForbiddenParams.Where((string p) => requestDict != null && requestDict[p] != null).ToList();
		if (list.Any())
		{
			string item = "Missing required parameters for " + operation + " operation: " + string.Join(", ", list);
			return (isValid: false, errorMessage: item);
		}
		if (list2.Any())
		{
			string item2 = "Forbidden parameters provided for " + operation + " operation: " + string.Join(", ", list2);
			return (isValid: false, errorMessage: item2);
		}
		return (isValid: true, errorMessage: null);
	}
}
