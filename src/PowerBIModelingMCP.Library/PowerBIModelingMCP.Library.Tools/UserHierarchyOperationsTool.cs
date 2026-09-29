using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AnalysisServices.Tabular;
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
public class UserHierarchyOperationsTool
{
	public const string ToolName = "user_hierarchy_operations";

	private readonly ILogger<UserHierarchyOperationsTool> _logger;

	public static readonly ToolMetadata toolMetadata = new ToolMetadata
	{
		Operations = new Dictionary<string, OperationMetadata>(StringComparer.OrdinalIgnoreCase)
		{
			["List"] = new OperationMetadata
			{
				RequiredParams = Array.Empty<string>(),
				Description = "List all user hierarchies in a table with their levels and metadata.\nOptional: Filter.TableNames (if omitted, lists hierarchies from all tables).",
				ExampleRequests = new List<string> { "{\n    \"request\": { \n        \"Operation\": \"List\"\n    }\n}", "{\n    \"request\": { \n        \"Operation\": \"List\",\n        \"Filter\": { \"TableNames\": [\"Sales\"] }\n    }\n}" }
			},
			["Get"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Get detailed information of one or more user hierarchies including all levels, properties, annotations, and extended properties.\nMandatory properties: References (list of UserHierarchyReference objects with TableName, HierarchyName).\nOptional: Options (ContinueOnError).",
				ExampleRequests = new List<string> { "{\n    \"request\": { \n        \"Operation\": \"Get\",\n        \"References\": [\n            { \"TableName\": \"Sales\", \"HierarchyName\": \"Geography\" }\n        ]\n    }\n}", "{\n    \"request\": { \n        \"Operation\": \"Get\",\n        \"References\": [\n            { \"TableName\": \"Sales\", \"HierarchyName\": \"Geography\" },\n            { \"TableName\": \"Product\", \"HierarchyName\": \"Category\" }\n        ],\n        \"Options\": { \"ContinueOnError\": true }\n    }\n}" }
			},
			["Create"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Definitions" },
				Description = "Create one or more user hierarchies in tables with specified levels and properties.\nMandatory properties: Definitions (list of HierarchyDefinition objects with TableName, Name, Levels).\nEach level in Levels requires: Name, ColumnName.\nOptional: Description, IsHidden, DisplayFolder, HideMembers, LineageTag, SourceLineageTag, Annotations, ExtendedProperties, Options (ContinueOnError, UseTransaction).\nOptional level properties: Description, Ordinal, LineageTag, SourceLineageTag, Annotations, ExtendedProperties.\nNote: Either all levels must have Ordinal specified or none (mixed ordinals not allowed).",
				CommonMistakes = new string[1] { "Forgetting to supply the host table of the hierarchy to be created in Definitions[].TableName" },
				ExampleRequests = new List<string> { "{\n    \"request\": { \n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            { \n                \"TableName\": \"Sales\", \n                \"Name\": \"Geography\", \n                \"Levels\": [\n                    { \"Name\": \"Country\", \"ColumnName\": \"Country\", \"Ordinal\": 0 },\n                    { \"Name\": \"State\", \"ColumnName\": \"State\", \"Ordinal\": 1 }\n                ] \n            }\n        ]\n    }\n}", "{\n    \"request\": { \n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            { \n                \"TableName\": \"Sales\", \n                \"Name\": \"Geography\", \n                \"Levels\": [\n                    { \"Name\": \"Country\", \"ColumnName\": \"Country\" },\n                    { \"Name\": \"State\", \"ColumnName\": \"State\" }\n                ] \n            },\n            { \n                \"TableName\": \"Product\", \n                \"Name\": \"Category\", \n                \"Levels\": [\n                    { \"Name\": \"Category\", \"ColumnName\": \"Category\" },\n                    { \"Name\": \"Subcategory\", \"ColumnName\": \"Subcategory\" }\n                ] \n            }\n        ],\n        \"Options\": { \"ContinueOnError\": false, \"UseTransaction\": true }\n    }\n}" }
			},
			["Update"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Definitions" },
				Description = "Update properties of one or more existing user hierarchies. Names cannot be changed through this operation - use Rename operation instead.\nMandatory properties: Definitions (list of HierarchyDefinition objects with TableName, Name).\nOptional: Description, IsHidden, DisplayFolder, HideMembers, LineageTag, SourceLineageTag, Annotations, ExtendedProperties, Options (ContinueOnError, UseTransaction).\nOptional Levels: When Levels is provided, applies patch mode - new levels are added, existing levels are updated by name, unspecified levels are left unchanged.\nNote: To change hierarchy name, use the Rename operation. To remove levels, use the RemoveLevels operation.",
				ExampleRequests = new List<string> { "{\n    \"request\": { \n        \"Operation\": \"Update\",\n        \"Definitions\": [\n            { \n                \"TableName\": \"Sales\", \n                \"Name\": \"Geography\",\n                \"HideMembers\": \"HideBlankMembers\"\n            }\n        ]\n    }\n}", "{\n    \"request\": { \n        \"Operation\": \"Update\",\n        \"Definitions\": [\n            { \"TableName\": \"Sales\", \"Name\": \"Geography\", \"IsHidden\": false },\n            { \"TableName\": \"Product\", \"Name\": \"Category\", \"DisplayFolder\": \"Product Hierarchies\" }\n        ],\n        \"Options\": { \"ContinueOnError\": true, \"UseTransaction\": true }\n    }\n}", "{\n    \"request\": { \n        \"Operation\": \"Update\",\n        \"Definitions\": [\n            { \n                \"TableName\": \"Sales\", \n                \"Name\": \"Geography\",\n                \"Description\": \"Geographic hierarchy\",\n                \"Levels\": [\n                    { \"Name\": \"Country\", \"Description\": \"Country level\" },\n                    { \"Name\": \"City\", \"ColumnName\": \"City\", \"Ordinal\": 2 }\n                ]\n            }\n        ]\n    }\n}" }
			},
			["Delete"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Delete one or more user hierarchies from tables. Can optionally cascade delete dependent objects.\nMandatory properties: References (list of UserHierarchyReference objects with TableName, HierarchyName).\nOptional: ShouldCascadeDelete (applies to all items in the batch), Options (ContinueOnError, UseTransaction).",
				Tips = new string[1] { "Use ShouldCascadeDelete to delete dependencies of the hierarchy" },
				ExampleRequests = new List<string> { "{\n    \"request\": { \n        \"Operation\": \"Delete\",\n        \"References\": [\n            { \"TableName\": \"Sales\", \"HierarchyName\": \"Geography\" }\n        ],\n        \"ShouldCascadeDelete\": true\n    }\n}", "{\n    \"request\": { \n        \"Operation\": \"Delete\",\n        \"References\": [\n            { \"TableName\": \"Sales\", \"HierarchyName\": \"Geography\" },\n            { \"TableName\": \"Product\", \"HierarchyName\": \"Category\" }\n        ],\n        \"ShouldCascadeDelete\": false,\n        \"Options\": { \"ContinueOnError\": true, \"UseTransaction\": true }\n    }\n}" }
			},
			["Rename"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "RenameDefinitions" },
				Description = "Rename one or more user hierarchies to new names.\nMandatory properties: RenameDefinitions (list of UserHierarchyRename objects with TableName, CurrentName, NewName).\nOptional: Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": { \n        \"Operation\": \"Rename\",\n        \"RenameDefinitions\": [\n            { \"TableName\": \"Sales\", \"CurrentName\": \"Geography\", \"NewName\": \"Location\" }\n        ]\n    }\n}", "{\n    \"request\": { \n        \"Operation\": \"Rename\",\n        \"RenameDefinitions\": [\n            { \"TableName\": \"Sales\", \"CurrentName\": \"Geography\", \"NewName\": \"Location\" },\n            { \"TableName\": \"Product\", \"CurrentName\": \"Category\", \"NewName\": \"ProductCategory\" }\n        ],\n        \"Options\": { \"ContinueOnError\": false, \"UseTransaction\": true }\n    }\n}" }
			},
			["GetColumns"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Get all columns referenced by levels in a user hierarchy.\nMandatory properties: References (single UserHierarchyReference with TableName, HierarchyName).\nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": { \n        \"Operation\": \"GetColumns\",\n        \"References\": [\n            { \"TableName\": \"Sales\", \"HierarchyName\": \"Geography\" }\n        ]\n    }\n}" }
			},
			["AddLevels"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "LevelDefinitions" },
				Description = "Add one or more levels to existing user hierarchies.\nMandatory properties: LevelDefinitions (list of HierarchyLevelDefinition objects with TableName, HierarchyName, Name, ColumnName).\nOptional: Description, Ordinal, LineageTag, SourceLineageTag, Annotations, ExtendedProperties, Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": { \n        \"Operation\": \"AddLevels\",\n        \"LevelDefinitions\": [\n            { \n                \"TableName\": \"Sales\", \n                \"HierarchyName\": \"Geography\",\n                \"Name\": \"City\",\n                \"ColumnName\": \"City\",\n                \"Ordinal\": 2\n            }\n        ]\n    }\n}", "{\n    \"request\": { \n        \"Operation\": \"AddLevels\",\n        \"LevelDefinitions\": [\n            { \"TableName\": \"Sales\", \"HierarchyName\": \"Geography\", \"Name\": \"City\", \"ColumnName\": \"City\" },\n            { \"TableName\": \"Product\", \"HierarchyName\": \"Category\", \"Name\": \"Product\", \"ColumnName\": \"ProductName\" }\n        ],\n        \"Options\": { \"ContinueOnError\": true, \"UseTransaction\": true }\n    }\n}" }
			},
			["RemoveLevels"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "LevelReferences" },
				Description = "Remove one or more levels from user hierarchies. Cannot remove the last level (delete hierarchy instead).\nMandatory properties: LevelReferences (list of HierarchyLevelReference objects with TableName, HierarchyName, LevelName).\nOptional: ShouldCascadeDelete (request-level, applies to all items, default: false), Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": { \n        \"Operation\": \"RemoveLevels\",\n        \"LevelReferences\": [\n            { \"TableName\": \"Sales\", \"HierarchyName\": \"Geography\", \"LevelName\": \"City\" }\n        ],\n        \"ShouldCascadeDelete\": true\n    }\n}", "{\n    \"request\": { \n        \"Operation\": \"RemoveLevels\",\n        \"LevelReferences\": [\n            { \"TableName\": \"Sales\", \"HierarchyName\": \"Geography\", \"LevelName\": \"City\" },\n            { \"TableName\": \"Product\", \"HierarchyName\": \"Category\", \"LevelName\": \"Product\" }\n        ],\n        \"ShouldCascadeDelete\": false,\n        \"Options\": { \"ContinueOnError\": true, \"UseTransaction\": true }\n    }\n}" }
			},
			["UpdateLevels"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "LevelDefinitions" },
				Description = "Update properties of one or more existing levels in user hierarchies. Level names cannot be changed through this operation - use RenameLevels operation instead.\nMandatory properties: LevelDefinitions (list of HierarchyLevelDefinition objects with TableName, HierarchyName, Name - where Name identifies the level to update).\nOptional: Description, Ordinal, ColumnName, LineageTag, SourceLineageTag, Annotations, ExtendedProperties, Options (ContinueOnError, UseTransaction).\nNote: To change level name, use the RenameLevels operation.",
				ExampleRequests = new List<string> { "{\n    \"request\": { \n        \"Operation\": \"UpdateLevels\",\n        \"LevelDefinitions\": [\n            { \n                \"TableName\": \"Sales\", \n                \"HierarchyName\": \"Geography\",\n                \"Name\": \"Country\",\n                \"Description\": \"Updated country level\"\n            }\n        ]\n    }\n}", "{\n    \"request\": { \n        \"Operation\": \"UpdateLevels\",\n        \"LevelDefinitions\": [\n            { \"TableName\": \"Sales\", \"HierarchyName\": \"Geography\", \"Name\": \"Country\", \"Ordinal\": 0 },\n            { \"TableName\": \"Sales\", \"HierarchyName\": \"Geography\", \"Name\": \"State\", \"Ordinal\": 1 }\n        ],\n        \"Options\": { \"ContinueOnError\": true, \"UseTransaction\": true }\n    }\n}" }
			},
			["RenameLevels"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "LevelRenameDefinitions" },
				Description = "Rename one or more levels in user hierarchies to new names.\nMandatory properties: LevelRenameDefinitions (list of HierarchyLevelRenameDefinition objects with TableName, HierarchyName, CurrentLevelName, NewLevelName).\nOptional: Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": { \n        \"Operation\": \"RenameLevels\",\n        \"LevelRenameDefinitions\": [\n            { \"TableName\": \"Sales\", \"HierarchyName\": \"Geography\", \"CurrentLevelName\": \"Country\", \"NewLevelName\": \"Nation\" }\n        ]\n    }\n}", "{\n    \"request\": { \n        \"Operation\": \"RenameLevels\",\n        \"LevelRenameDefinitions\": [\n            { \"TableName\": \"Sales\", \"HierarchyName\": \"Geography\", \"CurrentLevelName\": \"Country\", \"NewLevelName\": \"Nation\" },\n            { \"TableName\": \"Sales\", \"HierarchyName\": \"Geography\", \"CurrentLevelName\": \"State\", \"NewLevelName\": \"Region\" }\n        ],\n        \"Options\": { \"ContinueOnError\": false, \"UseTransaction\": true }\n    }\n}" }
			},
			["ReorderLevels"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "ReorderLevelsDefinitions" },
				Description = "Reorder levels in one or more user hierarchies by specifying the complete ordered list of level names for each.\nMandatory properties: ReorderLevelsDefinitions (list of UserHierarchyReorderLevels objects with TableName, HierarchyName, LevelNamesInOrder).\nOptional: Options (ContinueOnError, UseTransaction).",
				ExampleRequests = new List<string> { "{\n    \"request\": { \n        \"Operation\": \"ReorderLevels\",\n        \"ReorderLevelsDefinitions\": [\n            { \"TableName\": \"Sales\", \"HierarchyName\": \"Geography\", \"LevelNamesInOrder\": [\"Country\", \"State\", \"City\"] }\n        ]\n    }\n}", "{\n    \"request\": { \n        \"Operation\": \"ReorderLevels\",\n        \"ReorderLevelsDefinitions\": [\n            { \"TableName\": \"Sales\", \"HierarchyName\": \"Geography\", \"LevelNamesInOrder\": [\"Country\", \"State\", \"City\"] },\n            { \"TableName\": \"Product\", \"HierarchyName\": \"Category\", \"LevelNamesInOrder\": [\"Category\", \"Subcategory\", \"Product\"] }\n        ],\n        \"Options\": { \"ContinueOnError\": false, \"UseTransaction\": true }\n    }\n}" }
			},
			["ExportTMDL"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Export a user hierarchy definition to TMDL format.\nMandatory properties: References (list with exactly one UserHierarchyReference object containing TableName, HierarchyName).\nOptional: TmdlExportOptions (TmdlSerializationOptions.IncludeChildren defaults to false).",
				ExampleRequests = new List<string> { "{\n    \"request\": { \n        \"Operation\": \"ExportTMDL\",\n        \"TmdlExportOptions\": {\n            \"TmdlSerializationOptions\": {\n                \"IncludeChildren\": true\n            }\n        },\n        \"References\": [{ \"TableName\": \"Sales\", \"HierarchyName\": \"Geography\" }]\n    }\n}" }
			},
			["Help"] = new OperationMetadata
			{
				Description = "Describe the user hierarchy operations tool and list all available operations with their requirements.\nMandatory properties: None.\nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Help\"\n    }\n}" }
			}
		}
	};

	public UserHierarchyOperationsTool(ILogger<UserHierarchyOperationsTool> logger)
	{
		_logger = logger;
	}

	[McpServerTool(Name = "user_hierarchy_operations", Title = "User Hierarchy Operations", ReadOnly = false, Destructive = true)]
	[YamlToolDescription("user_hierarchy_operations")]
	public async Task<CallToolResult> ExecuteUserHierarchyOperation(McpServer mcpServer, UserHierarchyOperationRequest request, IWriteGuard writeGuard)
	{
		_logger.LogDebug("Executing {ToolName}.{Operation}: Connection={ConnectionName}", "UserHierarchyOperationsTool", request.Operation, request.ConnectionName ?? "(last used)");
		string[] validOperations = new string[14]
		{
			"LIST", "GET", "CREATE", "UPDATE", "DELETE", "RENAME", "GETCOLUMNS", "ADDLEVELS", "REMOVELEVELS", "UPDATELEVELS",
			"RENAMELEVELS", "REORDERLEVELS", "EXPORTTMDL", "HELP"
		};
		string[] writeOperations = new string[9] { "CREATE", "UPDATE", "DELETE", "RENAME", "ADDLEVELS", "REMOVELEVELS", "UPDATELEVELS", "RENAMELEVELS", "REORDERLEVELS" };
		string op = request.Operation.ToUpperInvariant();
		ToolCallAnnotations annotations = ToolCallAnnotations.Create("user_hierarchy_operations", request.Operation, !Enumerable.Contains(writeOperations, op));
		CallToolResult result = null;
		try
		{
			if (!Enumerable.Contains(validOperations, op))
			{
				_logger.LogWarning("Invalid operation '{Operation}' requested for {ToolName}. Valid operations: {ValidOperations}", request.Operation, "UserHierarchyOperationsTool", string.Join(", ", validOperations));
				CallToolResult result2;
				result = (result2 = CallToolResultHelper.FromResponse(UserHierarchyOperationResponse.Forbidden(request.Operation, "Invalid operation: " + request.Operation + ". Supported operations: " + string.Join(", ", validOperations)), annotations));
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
					_logger.LogWarning("{ToolName}.{Operation} blocked by write guard: {Reason}", "UserHierarchyOperationsTool", request.Operation, writeOperationResult.Message);
					CallToolResult result2;
					result = (result2 = CallToolResultHelper.FromResponse(new UserHierarchyOperationResponse
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
				"LIST" => CallToolResultHelper.FromResponse(await HandleListOperation(request), annotations), 
				"GET" => CallToolResultHelper.FromResponse(await HandleGetOperation(request), annotations), 
				"CREATE" => CallToolResultHelper.FromResponse(await HandleCreateOperation(request), annotations, null, minimalSuccessPayload: true), 
				"UPDATE" => CallToolResultHelper.FromResponse(await HandleUpdateOperation(request), annotations, null, minimalSuccessPayload: true), 
				"DELETE" => CallToolResultHelper.FromResponse(await HandleDeleteOperation(request), annotations, null, minimalSuccessPayload: true), 
				"RENAME" => CallToolResultHelper.FromResponse(await HandleRenameOperation(request), annotations, null, minimalSuccessPayload: true), 
				"GETCOLUMNS" => CallToolResultHelper.FromResponse(await HandleGetColumnsOperation(request), annotations), 
				"ADDLEVELS" => CallToolResultHelper.FromResponse(await HandleAddLevelsOperation(request), annotations, null, minimalSuccessPayload: true), 
				"REMOVELEVELS" => CallToolResultHelper.FromResponse(await HandleRemoveLevelsOperation(request), annotations, null, minimalSuccessPayload: true), 
				"UPDATELEVELS" => CallToolResultHelper.FromResponse(await HandleUpdateLevelsOperation(request), annotations, null, minimalSuccessPayload: true), 
				"RENAMELEVELS" => CallToolResultHelper.FromResponse(await HandleRenameLevelsOperation(request), annotations, null, minimalSuccessPayload: true), 
				"REORDERLEVELS" => CallToolResultHelper.FromResponse(await HandleReorderLevelsOperation(request), annotations, null, minimalSuccessPayload: true), 
				"EXPORTTMDL" => CallToolResultHelper.FromExportResponse(await HandleExportTMDLOperation(request), (request.References?.FirstOrDefault()?.HierarchyName ?? "hierarchy") + ".tmdl", "text/plain", annotations), 
				"HELP" => CallToolResultHelper.FromResponse(HandleHelpOperation(request, isWriteEnabled ? validOperations : validOperations.Except(writeOperations).ToArray()), annotations), 
				_ => CallToolResultHelper.FromResponse(UserHierarchyOperationResponse.Forbidden(request.Operation, "Operation " + request.Operation + " is not implemented"), annotations), 
			});
			return result3;
		}
		catch (Exception ex)
		{
			_logger.LogOperationError("UserHierarchyOperationsTool", request.Operation, ex);
			string message = op switch
			{
				"LIST" => "Failed to list hierarchies: " + ex.GetErrorMessage(), 
				"GET" => "Failed to get hierarchies: " + ex.GetErrorMessage(), 
				"CREATE" => "Failed to create hierarchies: " + ex.GetErrorMessage(), 
				"UPDATE" => "Failed to update hierarchies: " + ex.GetErrorMessage(), 
				"DELETE" => "Failed to delete hierarchies: " + ex.GetErrorMessage(), 
				"RENAME" => "Failed to rename hierarchies: " + ex.GetErrorMessage(), 
				"GETCOLUMNS" => "Failed to get hierarchy columns: " + ex.GetErrorMessage(), 
				"ADDLEVELS" => "Failed to add levels: " + ex.GetErrorMessage(), 
				"REMOVELEVELS" => "Failed to remove levels: " + ex.GetErrorMessage(), 
				"UPDATELEVELS" => "Failed to update levels: " + ex.GetErrorMessage(), 
				"RENAMELEVELS" => "Failed to rename levels: " + ex.GetErrorMessage(), 
				"REORDERLEVELS" => "Failed to reorder levels: " + ex.GetErrorMessage(), 
				"EXPORTTMDL" => "Error getting hierarchy TMDL: " + ex.GetErrorMessage(), 
				_ => "Error executing user hierarchy operation: " + ex.GetErrorMessage(), 
			};
			CallToolResult result2;
			result = (result2 = CallToolResultHelper.FromResponse(new UserHierarchyOperationResponse
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

	private async Task<UserHierarchyOperationResponse> HandleListOperation(UserHierarchyOperationRequest request)
	{
		List<string> tableNames = request.Filter?.TableNames?.Where((string t) => !string.IsNullOrWhiteSpace(t)).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToList();
		if (tableNames == null || tableNames.Count == 0)
		{
			await using IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(request.ConnectionName);
			List<string> list = new List<string>();
			list.AddRange(connectionInfo.Database.Model.Tables.Select((Table t) => t.Name));
			tableNames = list;
		}
		List<object> allHierarchies = new List<object>();
		foreach (string tableName in tableNames)
		{
			foreach (HierarchyList item in await UserHierarchyOperations.ListHierarchies(request.ConnectionName, tableName))
			{
				allHierarchies.Add(new
				{
					tableName = tableName,
					hierarchy = item
				});
			}
		}
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, TableCount={TableCount}, Count={Count}", "UserHierarchyOperationsTool", "List", request.ConnectionName, tableNames.Count, allHierarchies.Count);
		string message = ((tableNames.Count == 1) ? $"Found {allHierarchies.Count} hierarchies in table '{tableNames[0]}'" : $"Found {allHierarchies.Count} hierarchies across {tableNames.Count} tables");
		return new UserHierarchyOperationResponse
		{
			Success = true,
			Message = message,
			Operation = "List",
			Data = allHierarchies
		};
	}

	private async Task<UserHierarchyOperationResponse> HandleGetOperation(UserHierarchyOperationRequest request)
	{
		if (request.References == null || !request.References.Any())
		{
			return new UserHierarchyOperationResponse
			{
				Success = false,
				Operation = "Get",
				Message = "References is required and must contain at least one hierarchy identifier"
			};
		}
		BatchOperationResponse batchOperationResponse = await UserHierarchyOperations.GetHierarchies(request.ConnectionName, request.References, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}, Success={Success}", "UserHierarchyOperationsTool", "Get", request.ConnectionName, request.References.Count, batchOperationResponse.Success);
		return MapBatchResponse(batchOperationResponse);
	}

	private async Task<UserHierarchyOperationResponse> HandleCreateOperation(UserHierarchyOperationRequest request)
	{
		if (request.Definitions == null || !request.Definitions.Any())
		{
			return new UserHierarchyOperationResponse
			{
				Success = false,
				Operation = "Create",
				Message = "Definitions is required and must contain at least one hierarchy definition"
			};
		}
		BatchOperationResponse batchOperationResponse = await UserHierarchyOperations.CreateHierarchies(request.ConnectionName, request.Definitions, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}, Success={Success}", "UserHierarchyOperationsTool", "Create", request.ConnectionName, request.Definitions.Count, batchOperationResponse.Success);
		return MapBatchResponse(batchOperationResponse);
	}

	private async Task<UserHierarchyOperationResponse> HandleUpdateOperation(UserHierarchyOperationRequest request)
	{
		if (request.Definitions == null || !request.Definitions.Any())
		{
			return new UserHierarchyOperationResponse
			{
				Success = false,
				Operation = "Update",
				Message = "Definitions is required and must contain at least one hierarchy definition"
			};
		}
		BatchOperationResponse batchOperationResponse = await UserHierarchyOperations.UpdateHierarchies(request.ConnectionName, request.Definitions, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}, Success={Success}", "UserHierarchyOperationsTool", "Update", request.ConnectionName, request.Definitions.Count, batchOperationResponse.Success);
		return MapBatchResponse(batchOperationResponse);
	}

	private async Task<UserHierarchyOperationResponse> HandleDeleteOperation(UserHierarchyOperationRequest request)
	{
		if (request.References == null || !request.References.Any())
		{
			return new UserHierarchyOperationResponse
			{
				Success = false,
				Operation = "Delete",
				Message = "References is required and must contain at least one hierarchy identifier"
			};
		}
		BatchOperationResponse batchOperationResponse = await UserHierarchyOperations.DeleteHierarchies(request.ConnectionName, request.References, request.ShouldCascadeDelete, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}, Success={Success}", "UserHierarchyOperationsTool", "Delete", request.ConnectionName, request.References.Count, batchOperationResponse.Success);
		return MapBatchResponse(batchOperationResponse);
	}

	private async Task<UserHierarchyOperationResponse> HandleRenameOperation(UserHierarchyOperationRequest request)
	{
		if (request.RenameDefinitions == null || !request.RenameDefinitions.Any())
		{
			return new UserHierarchyOperationResponse
			{
				Success = false,
				Operation = "Rename",
				Message = "RenameDefinitions is required and must contain at least one hierarchy rename definition"
			};
		}
		BatchOperationResponse batchOperationResponse = await UserHierarchyOperations.RenameHierarchies(request.ConnectionName, request.RenameDefinitions, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}, Success={Success}", "UserHierarchyOperationsTool", "Rename", request.ConnectionName, request.RenameDefinitions.Count, batchOperationResponse.Success);
		return MapBatchResponse(batchOperationResponse);
	}

	private async Task<UserHierarchyOperationResponse> HandleGetColumnsOperation(UserHierarchyOperationRequest request)
	{
		if (request.References == null || !request.References.Any())
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("References is required for GetColumns operation and must contain exactly one hierarchy identifier", ErrorSource.User);
		}
		UserHierarchyReference userHierarchyReference = request.References.First();
		string tableName = userHierarchyReference.TableName;
		string hierarchyName = userHierarchyReference.HierarchyName;
		List<string> list = await UserHierarchyOperations.GetHierarchyColumns(request.ConnectionName, tableName, hierarchyName);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}", "UserHierarchyOperationsTool", "GetColumns", request.ConnectionName, list.Count);
		return new UserHierarchyOperationResponse
		{
			Success = true,
			Message = $"Retrieved {list.Count} columns for hierarchy '{hierarchyName}' in table '{tableName}'",
			Operation = "GetColumns",
			Data = list
		};
	}

	private async Task<UserHierarchyOperationResponse> HandleAddLevelsOperation(UserHierarchyOperationRequest request)
	{
		List<HierarchyLevelDefinition> levelDefinitions = request.LevelDefinitions;
		if (levelDefinitions == null || !levelDefinitions.Any())
		{
			return new UserHierarchyOperationResponse
			{
				Success = false,
				Operation = "AddLevels",
				Message = "LevelDefinitions is required and must contain at least one level definition"
			};
		}
		BatchOperationResponse batchOperationResponse = await UserHierarchyOperations.AddLevels(request.ConnectionName, levelDefinitions, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}, Success={Success}", "UserHierarchyOperationsTool", "AddLevels", request.ConnectionName, levelDefinitions.Count, batchOperationResponse.Success);
		return MapBatchResponse(batchOperationResponse);
	}

	private async Task<UserHierarchyOperationResponse> HandleRemoveLevelsOperation(UserHierarchyOperationRequest request)
	{
		List<HierarchyLevelReference> levelReferences = request.LevelReferences;
		if (levelReferences == null || !levelReferences.Any())
		{
			return new UserHierarchyOperationResponse
			{
				Success = false,
				Operation = "RemoveLevels",
				Message = "LevelReferences is required and must contain at least one level reference"
			};
		}
		BatchOperationResponse batchOperationResponse = await UserHierarchyOperations.RemoveLevels(request.ConnectionName, levelReferences, request.ShouldCascadeDelete, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}, Success={Success}", "UserHierarchyOperationsTool", "RemoveLevels", request.ConnectionName, levelReferences.Count, batchOperationResponse.Success);
		return MapBatchResponse(batchOperationResponse);
	}

	private async Task<UserHierarchyOperationResponse> HandleUpdateLevelsOperation(UserHierarchyOperationRequest request)
	{
		List<HierarchyLevelDefinition> levelDefinitions = request.LevelDefinitions;
		if (levelDefinitions == null || !levelDefinitions.Any())
		{
			return new UserHierarchyOperationResponse
			{
				Success = false,
				Operation = "UpdateLevels",
				Message = "LevelDefinitions is required and must contain at least one level definition"
			};
		}
		BatchOperationResponse batchOperationResponse = await UserHierarchyOperations.UpdateLevels(request.ConnectionName, levelDefinitions, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}, Success={Success}", "UserHierarchyOperationsTool", "UpdateLevels", request.ConnectionName, levelDefinitions.Count, batchOperationResponse.Success);
		return MapBatchResponse(batchOperationResponse);
	}

	private async Task<UserHierarchyOperationResponse> HandleRenameLevelsOperation(UserHierarchyOperationRequest request)
	{
		List<HierarchyLevelRenameDefinition> levelRenameDefinitions = request.LevelRenameDefinitions;
		if (levelRenameDefinitions == null || !levelRenameDefinitions.Any())
		{
			return new UserHierarchyOperationResponse
			{
				Success = false,
				Operation = "RenameLevels",
				Message = "LevelRenameDefinitions is required and must contain at least one rename definition"
			};
		}
		BatchOperationResponse batchOperationResponse = await UserHierarchyOperations.RenameLevels(request.ConnectionName, levelRenameDefinitions, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}, Success={Success}", "UserHierarchyOperationsTool", "RenameLevels", request.ConnectionName, levelRenameDefinitions.Count, batchOperationResponse.Success);
		return MapBatchResponse(batchOperationResponse);
	}

	private async Task<UserHierarchyOperationResponse> HandleReorderLevelsOperation(UserHierarchyOperationRequest request)
	{
		if (request.ReorderLevelsDefinitions == null || !request.ReorderLevelsDefinitions.Any())
		{
			return new UserHierarchyOperationResponse
			{
				Success = false,
				Operation = "ReorderLevels",
				Message = "ReorderLevelsDefinitions is required and must contain at least one reorder definition"
			};
		}
		BatchOperationResponse batchOperationResponse = await UserHierarchyOperations.ReorderLevelsBatch(request.ConnectionName, request.ReorderLevelsDefinitions, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}, Success={Success}", "UserHierarchyOperationsTool", "ReorderLevels", request.ConnectionName, request.ReorderLevelsDefinitions.Count, batchOperationResponse.Success);
		return MapBatchResponse(batchOperationResponse);
	}

	private async Task<UserHierarchyOperationResponse> HandleExportTMDLOperation(UserHierarchyOperationRequest request)
	{
		ExportValidationResult validation = ExportValidationHelper.ValidateReferences(request.References, "Hierarchy");
		if (!validation.IsValid)
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new UserHierarchyOperationResponse
			{
				Success = false,
				Operation = "ExportTMDL",
				Message = validation.ErrorMessage,
				Help = value
			};
		}
		UserHierarchyReference userHierarchyReference = request.References.First();
		string tableName = userHierarchyReference.TableName;
		string hierarchyName = userHierarchyReference.HierarchyName;
		string data = await UserHierarchyOperations.ExportTMDL(request.ConnectionName, tableName, hierarchyName, request.TmdlExportOptions ?? new ExportTmdl());
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "UserHierarchyOperationsTool", "ExportTMDL", request.ConnectionName);
		string objectIdentifier = tableName + "." + hierarchyName;
		string message = ExportValidationHelper.FormatSuccessMessage("Hierarchy", objectIdentifier, validation.WarningMessage);
		List<string> warnings = ((!string.IsNullOrEmpty(validation.WarningMessage)) ? new List<string> { validation.WarningMessage } : null);
		return new UserHierarchyOperationResponse
		{
			Success = true,
			Message = message,
			Operation = "ExportTMDL",
			Data = data,
			Warnings = warnings
		};
	}

	private UserHierarchyOperationResponse HandleHelpOperation(UserHierarchyOperationRequest request, string[] operations)
	{
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Operations={OperationCount}", "UserHierarchyOperationsTool", "Help", request.ConnectionName, operations.Length);
		UserHierarchyOperationResponse userHierarchyOperationResponse = new UserHierarchyOperationResponse();
		userHierarchyOperationResponse.Success = true;
		userHierarchyOperationResponse.Message = "Tool description retrieved successfully";
		userHierarchyOperationResponse.Operation = "Help";
		userHierarchyOperationResponse.Help = new
		{
			ToolName = "user_hierarchy_operations",
			Description = "Perform operations on semantic model user hierarchies.",
			SupportedOperations = operations,
			Examples = toolMetadata.Operations.Where<KeyValuePair<string, OperationMetadata>>((KeyValuePair<string, OperationMetadata> p) => operations.Contains<string>(p.Key, StringComparer.OrdinalIgnoreCase)),
			Notes = new string[6] { "Use the Operation parameter to specify which operation to perform.", "Operations accept lists of items for bulk processing.", "Single-item operations are represented as lists of one.", "Use Options.ContinueOnError to control error handling in batch operations.", "Use Options.UseTransaction to control transactional behavior.", "If the request is declined by the user, the operation should be aborted." }
		};
		return userHierarchyOperationResponse;
	}

	private UserHierarchyOperationResponse MapBatchResponse(BatchOperationResponse batchResponse)
	{
		UserHierarchyOperationResponse userHierarchyOperationResponse = new UserHierarchyOperationResponse
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
			userHierarchyOperationResponse.Exceptions.AddRange(batchResponse.Exceptions);
		}
		return userHierarchyOperationResponse;
	}

	private (bool isValid, string? errorMessage) ValidateRequest(string operation, UserHierarchyOperationRequest request)
	{
		switch (operation.ToUpperInvariant())
		{
		case "UPDATE":
		case "CREATE":
			if (request.Definitions == null || !request.Definitions.Any())
			{
				return (isValid: false, errorMessage: "Definitions is required for Create/Update operation");
			}
			break;
		case "DELETE":
		case "EXPORTTMDL":
		case "GETCOLUMNS":
		case "GET":
			if (request.References == null || !request.References.Any())
			{
				return (isValid: false, errorMessage: "References is required for this operation");
			}
			break;
		case "RENAME":
			if (request.RenameDefinitions == null || !request.RenameDefinitions.Any())
			{
				return (isValid: false, errorMessage: "RenameDefinitions is required for Rename operation");
			}
			break;
		case "ADDLEVELS":
			if (request.LevelDefinitions == null || !request.LevelDefinitions.Any())
			{
				return (isValid: false, errorMessage: "LevelDefinitions is required for AddLevels operation");
			}
			break;
		case "REMOVELEVELS":
			if (request.LevelReferences == null || !request.LevelReferences.Any())
			{
				return (isValid: false, errorMessage: "LevelReferences is required for RemoveLevels operation");
			}
			break;
		case "UPDATELEVELS":
			if (request.LevelDefinitions == null || !request.LevelDefinitions.Any())
			{
				return (isValid: false, errorMessage: "LevelDefinitions is required for UpdateLevels operation");
			}
			break;
		case "RENAMELEVELS":
			if (request.LevelRenameDefinitions == null || !request.LevelRenameDefinitions.Any())
			{
				return (isValid: false, errorMessage: "LevelRenameDefinitions is required for RenameLevels operation");
			}
			break;
		case "REORDERLEVELS":
			if (request.ReorderLevelsDefinitions == null || !request.ReorderLevelsDefinitions.Any())
			{
				return (isValid: false, errorMessage: "ReorderLevelsDefinitions is required for ReorderLevels operation");
			}
			break;
		}
		return (isValid: true, errorMessage: null);
	}
}
