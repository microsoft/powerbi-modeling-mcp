using System;
using System.Collections.Generic;
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
public class RelationshipOperationsTool
{
	public const string ToolName = "relationship_operations";

	private readonly ILogger<RelationshipOperationsTool> _logger;

	public static readonly ToolMetadata toolMetadata = new ToolMetadata
	{
		Operations = new Dictionary<string, OperationMetadata>(StringComparer.OrdinalIgnoreCase)
		{
			["List"] = new OperationMetadata
			{
				Description = "List all relationships in the model.\nReturns detailed information about each relationship including tables, columns, cardinality, and filtering behavior.\nMandatory properties: None.\nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"List\"\n    }\n}" }
			},
			["Create"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Definitions" },
				Description = "Create one or more relationships between tables.\nIn PowerBI relationships, the 'from' side is the many side (fact table in BI terms, child table in database terms),\nand the 'to' side is the one side (dimension table in BI terms, parent/lookup table in database terms).\nThis follows standard foreign key relationship patterns where multiple records in the child table reference a single record in the parent table.\nIf no relationship name is provided, one will be auto-generated using the pattern FromTable_FromColumn_ToTable_ToColumn.\nCardinality and filtering behaviors must be set consistently or the Analysis Services engine will reject the save:\n  - Many-to-One (default): CrossFilteringBehavior may be OneDirection or BothDirections.\n  - One-to-One (FromCardinality=One, ToCardinality=One): CrossFilteringBehavior MUST be BothDirections.\n  - Many-to-Many (FromCardinality=Many, ToCardinality=Many): CrossFilteringBehavior may be BothDirections OR OneDirection. When OneDirection is used, the filter flows from the 'from' side to the 'to' side; to flow the other way, put the columns on the opposite sides when creating.\n  - SecurityFilteringBehavior=BothDirections REQUIRES CrossFilteringBehavior=BothDirections. Do not set SecurityFilteringBehavior=BothDirections while leaving CrossFilteringBehavior=OneDirection.\nMandatory properties: Definitions (list, each with FromTable, FromColumn, ToTable, ToColumn).\nOptional: Name, IsActive, Type, CrossFilteringBehavior, FromCardinality, ToCardinality, SecurityFilteringBehavior, JoinOnDateBehavior, RelyOnReferentialIntegrity, Annotations, ExtendedProperties, Options.",
				CommonMistakes = new string[3] { "Setting the dimension table as 'from' side and fact table as 'to' side - should be the other way around", "Creating a One-to-One relationship (FromCardinality=One, ToCardinality=One) without setting CrossFilteringBehavior=BothDirections - save will fail with 'CrossFilterDirection for One-to-One relationships should always be set to BothDirections'", "Setting SecurityFilteringBehavior=BothDirections while leaving CrossFilteringBehavior=OneDirection - save will fail with 'cannot have SecurityFilterBehavior set to BothDirections when the CrossFilterBehavior is set to OneDirection'" },
				Tips = new string[8] { "PowerBI relationships are predominantly many-to-one", "One-to-One relationships REQUIRE CrossFilteringBehavior=BothDirections", "Many-to-Many relationships accept CrossFilteringBehavior=BothDirections or OneDirection (the OneDirection flow is chosen by which side is 'from' vs 'to'); use with caution", "CrossFilteringBehavior values: OneDirection, BothDirections, Automatic", "FromCardinality/ToCardinality values: One, Many", "SecurityFilteringBehavior values: OneDirection, BothDirections", "JoinOnDateBehavior values: DateAndTime, DatePartOnly", "Type values: SingleColumn" },
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            {\n                \"Name\": \"SalesToRegion\",\n                \"FromTable\": \"Sales\",\n                \"FromColumn\": \"RegionID\",\n                \"FromCardinality\": \"Many\",\n                \"ToTable\": \"Region\",\n                \"ToColumn\": \"RegionID\",\n                \"ToCardinality\": \"One\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            {\n                \"FromTable\": \"Sales\",\n                \"FromColumn\": \"ProductID\",\n                \"FromCardinality\": \"Many\",\n                \"ToTable\": \"Product\",\n                \"ToColumn\": \"ID\",\n                \"ToCardinality\": \"One\"\n            },\n            {\n                \"FromTable\": \"Sales\",\n                \"FromColumn\": \"RegionID\",\n                \"FromCardinality\": \"Many\",\n                \"ToTable\": \"Region\",\n                \"ToColumn\": \"RegionID\",\n                \"ToCardinality\": \"One\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            {\n                \"Name\": \"CustomerToProfile\",\n                \"FromTable\": \"Customer\",\n                \"FromColumn\": \"CustomerID\",\n                \"FromCardinality\": \"One\",\n                \"ToTable\": \"CustomerProfile\",\n                \"ToColumn\": \"CustomerID\",\n                \"ToCardinality\": \"One\",\n                \"CrossFilteringBehavior\": \"BothDirections\"\n            }\n        ]\n    }\n}" }
			},
			["Update"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Definitions" },
				Description = "Update one or more existing relationships' properties, including cardinality.\nNames cannot be changed and must use the Rename operation instead.\nTables and columns (FromTable, FromColumn, ToTable, ToColumn) also cannot be changed - delete and recreate the relationship instead.\nCardinality CAN be changed via FromCardinality/ToCardinality, but the resulting combination with CrossFilteringBehavior must be consistent or the Analysis Services engine will reject the save:\n  - One-to-One (FromCardinality=One, ToCardinality=One): CrossFilteringBehavior MUST be BothDirections.\n  - Many-to-Many (FromCardinality=Many, ToCardinality=Many): CrossFilteringBehavior may be BothDirections OR OneDirection (the OneDirection flow direction is fixed by the existing 'from'/'to' column ordering and cannot be swapped via Update - delete and recreate to flip direction).\n  - SecurityFilteringBehavior=BothDirections REQUIRES CrossFilteringBehavior=BothDirections.\nIMPORTANT: If changing cardinality requires swapping which side is 'many' (e.g., converting a One-to-One into a Many-to-One in the opposite direction), you cannot do it via Update because that is treated as a column change. Delete the relationship and Create a new one with the columns in the correct order.\nWhen changing cardinality to or from these shapes, always coordinate CrossFilteringBehavior in the same request.\nMandatory properties: Definitions (list, each with Name).\nOptional: IsActive, Type, CrossFilteringBehavior, FromCardinality, ToCardinality, SecurityFilteringBehavior, JoinOnDateBehavior, RelyOnReferentialIntegrity, Annotations, ExtendedProperties, Options.",
				CommonMistakes = new string[4] { "Changing FromCardinality/ToCardinality to produce a One-to-One relationship without also setting CrossFilteringBehavior to BothDirections (save will fail with 'CrossFilterDirection for One-to-One relationships should always be set to BothDirections')", "Setting SecurityFilteringBehavior=BothDirections while leaving CrossFilteringBehavior=OneDirection (save will fail with 'cannot have SecurityFilterBehavior set to BothDirections when the CrossFilterBehavior is set to OneDirection')", "Attempting to change FromTable/FromColumn/ToTable/ToColumn on an existing relationship - not supported, delete and recreate instead", "Trying to convert a One-to-One into a Many-to-One (or vice versa) where the 'many' side must switch tables - Update will fail with 'Cannot change the tables or columns of an existing relationship'; delete and recreate with the correct column ordering" },
				Tips = new string[7] { "CrossFilteringBehavior values: OneDirection, BothDirections, Automatic", "FromCardinality/ToCardinality values: One, Many", "One-to-One relationships require CrossFilteringBehavior=BothDirections", "Many-to-Many relationships accept CrossFilteringBehavior=BothDirections or OneDirection", "SecurityFilteringBehavior values: OneDirection, BothDirections", "JoinOnDateBehavior values: DateAndTime, DatePartOnly", "Type values: SingleColumn (currently only supported value)" },
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Update\",\n        \"Definitions\": [\n            {\n                \"Name\": \"SalesToRegion\",\n                \"IsActive\": false\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Update\",\n        \"Definitions\": [\n            {\n                \"Name\": \"SalesToRegion\",\n                \"FromCardinality\": \"Many\",\n                \"ToCardinality\": \"Many\",\n                \"CrossFilteringBehavior\": \"BothDirections\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Update\",\n        \"Definitions\": [\n            {\n                \"Name\": \"CustomerToProfile\",\n                \"FromCardinality\": \"One\",\n                \"ToCardinality\": \"One\",\n                \"CrossFilteringBehavior\": \"BothDirections\"\n            }\n        ]\n    }\n}" }
			},
			["Delete"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Delete one or more relationships from the model.\nThis operation permanently removes the relationships and cannot be undone.\nMandatory properties: References (list, each with Name).\nOptional: Options.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Delete\",\n        \"References\": [\n            { \"Name\": \"ObsoleteRelationship1\" },\n            { \"Name\": \"ObsoleteRelationship2\" }\n        ]\n    }\n}" }
			},
			["Get"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Get detailed information about one or more specific relationships.\nReturns comprehensive relationship properties including tables, columns, cardinality, filtering behavior, and current state.\nMandatory properties: References (list, each with Name).\nOptional: Options.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Get\",\n        \"References\": [\n            { \"Name\": \"SalesToRegion\" },\n            { \"Name\": \"SalesToProduct\" }\n        ]\n    }\n}" }
			},
			["Rename"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "RenameDefinitions" },
				Description = "Rename one or more relationships in the model.\nMandatory properties: RenameDefinitions (list, each with CurrentName and NewName).\nOptional: Options.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Rename\",\n        \"RenameDefinitions\": [\n            {\n                \"CurrentName\": \"OldRelationship\",\n                \"NewName\": \"NewRelationship\"\n            }\n        ]\n    }\n}" }
			},
			["Activate"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Activate one or more relationships in the model.\nSets the relationship's IsActive property to true, enabling it for data filtering and calculations.\nMandatory properties: References (list, each with Name).\nOptional: Options.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Activate\",\n        \"References\": [\n            { \"Name\": \"SalesToRegion\" }\n        ]\n    }\n}" }
			},
			["Deactivate"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Deactivate one or more relationships in the model.\nSets the relationship's IsActive property to false, disabling it from data filtering and calculations.\nMandatory properties: References (list, each with Name).\nOptional: Options.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Deactivate\",\n        \"References\": [\n            { \"Name\": \"SalesToRegion\" }\n        ]\n    }\n}" }
			},
			["Find"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Find all relationships connected to a specific table.\nReturns list of relationships where the table appears as either the 'from' or 'to' side.\nMandatory properties: References (list with single item containing Name as table name).\nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Find\",\n        \"References\": [\n            { \"Name\": \"Sales\" }\n        ]\n    }\n}" }
			},
			["ExportTMDL"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Export a relationship to TMDL format.\nMandatory properties: References (list with single item containing Name).\nOptional: TmdlExportOptions (TmdlSerializationOptions.IncludeChildren defaults to false).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ExportTMDL\",\n        \"TmdlExportOptions\": {\n            \"TmdlSerializationOptions\": {\n                \"IncludeChildren\": true\n            }\n        },\n        \"References\": [\n            { \"Name\": \"SalesToRegion\" }\n        ]\n    }\n}" }
			},
			["Help"] = new OperationMetadata
			{
				Description = "Describe the relationship operations tool and its available operations.\nReturns comprehensive help information including supported operations, examples, and usage notes.\nMandatory properties: None.\nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Help\"\n    }\n}" }
			}
		}
	};

	public RelationshipOperationsTool(ILogger<RelationshipOperationsTool> logger)
	{
		_logger = logger;
	}

	[McpServerTool(Name = "relationship_operations", Title = "Relationship Operations", ReadOnly = false, Destructive = true)]
	[YamlToolDescription("relationship_operations")]
	public async Task<CallToolResult> ExecuteRelationshipOperation(McpServer mcpServer, RelationshipOperationRequest request, IWriteGuard writeGuard)
	{
		_logger.LogDebug("Executing {ToolName}.{Operation}: Connection={ConnectionName}", "RelationshipOperationsTool", request.Operation, request.ConnectionName ?? "(last used)");
		string[] validOperations = new string[11]
		{
			"LIST", "GET", "CREATE", "UPDATE", "DELETE", "RENAME", "ACTIVATE", "DEACTIVATE", "FIND", "EXPORTTMDL",
			"HELP"
		};
		string[] writeOperations = new string[6] { "CREATE", "UPDATE", "DELETE", "RENAME", "ACTIVATE", "DEACTIVATE" };
		string op = request.Operation.ToUpperInvariant();
		ToolCallAnnotations annotations = ToolCallAnnotations.Create("relationship_operations", request.Operation, !Enumerable.Contains(writeOperations, op));
		CallToolResult result = null;
		try
		{
			if (!Enumerable.Contains(validOperations, op))
			{
				_logger.LogWarning("Invalid operation '{Operation}' requested for {ToolName}. Valid operations: {ValidOperations}", request.Operation, "RelationshipOperationsTool", string.Join(", ", validOperations));
				CallToolResult result2;
				result = (result2 = CallToolResultHelper.FromResponse(RelationshipOperationResponse.Forbidden(request.Operation, "Invalid operation: " + request.Operation + ". Supported operations: " + string.Join(", ", validOperations)), annotations));
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
					_logger.LogWarning("{ToolName}.{Operation} blocked by write guard: {Reason}", "RelationshipOperationsTool", request.Operation, writeOperationResult.Message);
					CallToolResult result2;
					result = (result2 = CallToolResultHelper.FromResponse(RelationshipOperationResponse.Forbidden(request.Operation, writeOperationResult.Message), annotations));
					return result2;
				}
			}
			bool isWriteEnabled = writeGuard.IsWriteEnabled;
			CallToolResult result3;
			result = (result3 = request.Operation.ToUpperInvariant() switch
			{
				"LIST" => CallToolResultHelper.FromResponse(await HandleListOperation(request), annotations), 
				"GET" => CallToolResultHelper.FromResponse(await HandleGetOperation(request), annotations), 
				"CREATE" => CallToolResultHelper.FromResponse(await HandleCreateOperation(request), annotations, null, minimalSuccessPayload: true), 
				"UPDATE" => CallToolResultHelper.FromResponse(await HandleUpdateOperation(request), annotations, null, minimalSuccessPayload: true), 
				"DELETE" => CallToolResultHelper.FromResponse(await HandleDeleteOperation(request), annotations, null, minimalSuccessPayload: true), 
				"RENAME" => CallToolResultHelper.FromResponse(await HandleRenameOperation(request), annotations, null, minimalSuccessPayload: true), 
				"ACTIVATE" => CallToolResultHelper.FromResponse(await HandleActivateOperation(request), annotations), 
				"DEACTIVATE" => CallToolResultHelper.FromResponse(await HandleDeactivateOperation(request), annotations), 
				"FIND" => CallToolResultHelper.FromResponse(await HandleFindOperation(request), annotations), 
				"EXPORTTMDL" => CallToolResultHelper.FromExportResponse(await HandleExportTMDLOperation(request), (request.References?.FirstOrDefault()?.Name ?? "relationship") + ".tmdl", "text/plain", annotations), 
				"HELP" => CallToolResultHelper.FromResponse(await HandleHelpOperation(request, isWriteEnabled ? validOperations : validOperations.Except(writeOperations).ToArray()), annotations), 
				_ => CallToolResultHelper.FromResponse(RelationshipOperationResponse.Forbidden(request.Operation, "Operation " + request.Operation + " not implemented"), annotations), 
			});
			return result3;
		}
		catch (Exception ex)
		{
			_logger.LogOperationError("RelationshipOperationsTool", request.Operation, ex);
			string message = op switch
			{
				"LIST" => "Failed to list relationships: " + ex.GetErrorMessage(), 
				"GET" => "Failed to get relationships: " + ex.GetErrorMessage(), 
				"CREATE" => "Failed to create relationships: " + ex.GetErrorMessage(), 
				"UPDATE" => "Failed to update relationships: " + ex.GetErrorMessage(), 
				"DELETE" => "Failed to delete relationships: " + ex.GetErrorMessage(), 
				"RENAME" => "Failed to rename relationships: " + ex.GetErrorMessage(), 
				"ACTIVATE" => "Failed to activate relationship: " + ex.GetErrorMessage(), 
				"DEACTIVATE" => "Failed to deactivate relationship: " + ex.GetErrorMessage(), 
				"FIND" => "Failed to find relationships for table: " + ex.GetErrorMessage(), 
				"EXPORTTMDL" => "Failed to export relationship TMDL: " + ex.GetErrorMessage(), 
				_ => "Error executing relationship operation: " + ex.GetErrorMessage(), 
			};
			CallToolResult result2;
			result = (result2 = CallToolResultHelper.FromResponse(new RelationshipOperationResponse
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

	private async Task<RelationshipOperationResponse> HandleListOperation(RelationshipOperationRequest request)
	{
		List<RelationshipList> list = await RelationshipOperations.ListRelationships(request.ConnectionName);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}", "RelationshipOperationsTool", "LIST", request.ConnectionName, list.Count);
		return new RelationshipOperationResponse
		{
			Success = true,
			Message = $"Found {list.Count} relationships",
			Operation = "LIST",
			Data = list
		};
	}

	private async Task<RelationshipOperationResponse> HandleGetOperation(RelationshipOperationRequest request)
	{
		if (request.References == null || !request.References.Any())
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new RelationshipOperationResponse
			{
				Success = false,
				Message = "References is required for Get operation",
				Operation = "GET",
				Help = value
			};
		}
		BatchOperationResponse batchOperationResponse = await RelationshipOperations.GetRelationships(request.ConnectionName, request.References, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}, Success={Success}", "RelationshipOperationsTool", "GET", request.ConnectionName, request.References.Count, batchOperationResponse.Success);
		return new RelationshipOperationResponse
		{
			Success = batchOperationResponse.Success,
			Message = batchOperationResponse.Message,
			Operation = "GET",
			Summary = batchOperationResponse.Summary,
			Results = batchOperationResponse.Results,
			Warnings = batchOperationResponse.Warnings
		};
	}

	private async Task<RelationshipOperationResponse> HandleCreateOperation(RelationshipOperationRequest request)
	{
		if (request.Definitions == null || !request.Definitions.Any())
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new RelationshipOperationResponse
			{
				Success = false,
				Message = "Definitions is required for Create operation",
				Operation = "CREATE",
				Help = value
			};
		}
		BatchOperationResponse batchOperationResponse = await RelationshipOperations.CreateRelationships(request.ConnectionName, request.Definitions, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}, Success={Success}", "RelationshipOperationsTool", "CREATE", request.ConnectionName, request.Definitions.Count, batchOperationResponse.Success);
		if (batchOperationResponse.Warnings != null && batchOperationResponse.Warnings.Any())
		{
			foreach (string warning in batchOperationResponse.Warnings)
			{
				_logger.LogOperationWarning("RelationshipOperationsTool", "CREATE", warning);
			}
		}
		return new RelationshipOperationResponse
		{
			Success = batchOperationResponse.Success,
			Message = batchOperationResponse.Message,
			Operation = "CREATE",
			Summary = batchOperationResponse.Summary,
			Results = batchOperationResponse.Results,
			Warnings = batchOperationResponse.Warnings
		};
	}

	private async Task<RelationshipOperationResponse> HandleUpdateOperation(RelationshipOperationRequest request)
	{
		if (request.Definitions == null || !request.Definitions.Any())
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new RelationshipOperationResponse
			{
				Success = false,
				Message = "Definitions is required for Update operation",
				Operation = "UPDATE",
				Help = value
			};
		}
		BatchOperationResponse batchOperationResponse = await RelationshipOperations.UpdateRelationships(request.ConnectionName, request.Definitions, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}, Success={Success}", "RelationshipOperationsTool", "UPDATE", request.ConnectionName, request.Definitions.Count, batchOperationResponse.Success);
		return new RelationshipOperationResponse
		{
			Success = batchOperationResponse.Success,
			Message = batchOperationResponse.Message,
			Operation = "UPDATE",
			Summary = batchOperationResponse.Summary,
			Results = batchOperationResponse.Results,
			Warnings = batchOperationResponse.Warnings
		};
	}

	private async Task<RelationshipOperationResponse> HandleDeleteOperation(RelationshipOperationRequest request)
	{
		if (request.References == null || !request.References.Any())
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new RelationshipOperationResponse
			{
				Success = false,
				Message = "References is required for Delete operation",
				Operation = "DELETE",
				Help = value
			};
		}
		BatchOperationResponse batchOperationResponse = await RelationshipOperations.DeleteRelationships(request.ConnectionName, request.References, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}, Success={Success}", "RelationshipOperationsTool", "DELETE", request.ConnectionName, request.References.Count, batchOperationResponse.Success);
		return new RelationshipOperationResponse
		{
			Success = batchOperationResponse.Success,
			Message = batchOperationResponse.Message,
			Operation = "DELETE",
			Summary = batchOperationResponse.Summary,
			Results = batchOperationResponse.Results,
			Warnings = batchOperationResponse.Warnings
		};
	}

	private async Task<RelationshipOperationResponse> HandleRenameOperation(RelationshipOperationRequest request)
	{
		if (request.RenameDefinitions == null || !request.RenameDefinitions.Any())
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new RelationshipOperationResponse
			{
				Success = false,
				Message = "RenameDefinitions is required for Rename operation",
				Operation = "RENAME",
				Help = value
			};
		}
		BatchOperationResponse batchOperationResponse = await RelationshipOperations.RenameRelationships(request.ConnectionName, request.RenameDefinitions, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}, Success={Success}", "RelationshipOperationsTool", "RENAME", request.ConnectionName, request.RenameDefinitions.Count, batchOperationResponse.Success);
		return new RelationshipOperationResponse
		{
			Success = batchOperationResponse.Success,
			Message = batchOperationResponse.Message,
			Operation = "RENAME",
			Summary = batchOperationResponse.Summary,
			Results = batchOperationResponse.Results,
			Warnings = batchOperationResponse.Warnings
		};
	}

	private async Task<RelationshipOperationResponse> HandleActivateOperation(RelationshipOperationRequest request)
	{
		if (request.References == null || !request.References.Any())
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new RelationshipOperationResponse
			{
				Success = false,
				Message = "References is required for Activate operation",
				Operation = "ACTIVATE",
				Help = value
			};
		}
		string relationshipName = request.References.First().Name;
		RelationshipOperationResult relationshipOperationResult = await RelationshipOperations.ActivateRelationship(request.ConnectionName, relationshipName);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, HasChanges={HasChanges}", "RelationshipOperationsTool", "ACTIVATE", request.ConnectionName, relationshipOperationResult.HasChanges);
		if (relationshipOperationResult.Warnings != null && relationshipOperationResult.Warnings.Any())
		{
			foreach (string warning in relationshipOperationResult.Warnings)
			{
				_logger.LogOperationWarning("RelationshipOperationsTool", "ACTIVATE", warning);
			}
		}
		return new RelationshipOperationResponse
		{
			Success = true,
			Message = (relationshipOperationResult.HasChanges ? ("Activated relationship '" + relationshipName + "'") : ("Relationship '" + relationshipName + "' was already active")),
			Operation = "ACTIVATE",
			Data = relationshipOperationResult,
			Warnings = relationshipOperationResult.Warnings
		};
	}

	private async Task<RelationshipOperationResponse> HandleDeactivateOperation(RelationshipOperationRequest request)
	{
		if (request.References == null || !request.References.Any())
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new RelationshipOperationResponse
			{
				Success = false,
				Message = "References is required for Deactivate operation",
				Operation = "DEACTIVATE",
				Help = value
			};
		}
		string relationshipName = request.References.First().Name;
		RelationshipOperationResult relationshipOperationResult = await RelationshipOperations.DeactivateRelationship(request.ConnectionName, relationshipName);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, HasChanges={HasChanges}", "RelationshipOperationsTool", "DEACTIVATE", request.ConnectionName, relationshipOperationResult.HasChanges);
		if (relationshipOperationResult.Warnings != null && relationshipOperationResult.Warnings.Any())
		{
			foreach (string warning in relationshipOperationResult.Warnings)
			{
				_logger.LogOperationWarning("RelationshipOperationsTool", "DEACTIVATE", warning);
			}
		}
		return new RelationshipOperationResponse
		{
			Success = true,
			Message = (relationshipOperationResult.HasChanges ? ("Deactivated relationship '" + relationshipName + "'") : ("Relationship '" + relationshipName + "' was already inactive")),
			Operation = "DEACTIVATE",
			Data = relationshipOperationResult,
			Warnings = relationshipOperationResult.Warnings
		};
	}

	private async Task<RelationshipOperationResponse> HandleFindOperation(RelationshipOperationRequest request)
	{
		if (request.References == null || !request.References.Any())
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new RelationshipOperationResponse
			{
				Success = false,
				Message = "References is required for Find operation",
				Operation = "FIND",
				Help = value
			};
		}
		string tableName = request.References.First().Name;
		List<string> list = await RelationshipOperations.FindRelationshipsForTable(request.ConnectionName, tableName);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}", "RelationshipOperationsTool", "FIND", request.ConnectionName, list.Count);
		return new RelationshipOperationResponse
		{
			Success = true,
			Message = $"Found {list.Count} relationships for table '{tableName}'",
			Operation = "FIND",
			Data = list
		};
	}

	private async Task<RelationshipOperationResponse> HandleExportTMDLOperation(RelationshipOperationRequest request)
	{
		ExportValidationResult validation = ExportValidationHelper.ValidateReferences(request.References, "Relationship");
		if (!validation.IsValid)
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new RelationshipOperationResponse
			{
				Success = false,
				Message = validation.ErrorMessage,
				Operation = "ExportTMDL",
				Help = value
			};
		}
		string relationshipName = request.References.First().Name;
		string data = await RelationshipOperations.ExportTMDL(request.ConnectionName, relationshipName, request.TmdlExportOptions ?? new ExportTmdl());
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "RelationshipOperationsTool", "ExportTMDL", request.ConnectionName);
		string message = ExportValidationHelper.FormatSuccessMessage("Relationship", relationshipName, validation.WarningMessage);
		List<string> warnings = ((!string.IsNullOrEmpty(validation.WarningMessage)) ? new List<string> { validation.WarningMessage } : null);
		return new RelationshipOperationResponse
		{
			Success = true,
			Message = message,
			Operation = "ExportTMDL",
			Data = data,
			Warnings = warnings
		};
	}

	private Task<RelationshipOperationResponse> HandleHelpOperation(RelationshipOperationRequest request, string[] operations)
	{
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Operations={OperationCount}", "RelationshipOperationsTool", request.Operation, request.ConnectionName, operations.Length);
		RelationshipOperationResponse relationshipOperationResponse = new RelationshipOperationResponse();
		relationshipOperationResponse.Success = true;
		relationshipOperationResponse.Message = "Help information for relationship operations";
		relationshipOperationResponse.Operation = request.Operation;
		relationshipOperationResponse.Help = new
		{
			ToolName = "relationship_operations",
			Description = "Perform operations on semantic model relationships.",
			SupportedOperations = operations,
			Examples = toolMetadata.Operations.Where<KeyValuePair<string, OperationMetadata>>((KeyValuePair<string, OperationMetadata> p) => operations.Contains<string>(p.Key, StringComparer.OrdinalIgnoreCase)),
			Notes = new string[3] { "Relationship names are case-insensitive.", "Relationship names must be unique within the model.", "Relationship names must not contain spaces or special characters." }
		};
		return Task.FromResult(relationshipOperationResponse);
	}

	private (bool isValid, string? errorMessage) ValidateRequest(string operation, RelationshipOperationRequest request)
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
			return (isValid: false, errorMessage: "Missing required parameters needed for " + operation + " operation: " + string.Join(", ", list));
		}
		if (list2.Any())
		{
			return (isValid: false, errorMessage: "Forbidden parameters not allowed for " + operation + " operation: " + string.Join(", ", list2));
		}
		return (isValid: true, errorMessage: null);
	}
}
