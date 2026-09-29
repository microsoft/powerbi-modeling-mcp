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
public class ObjectTranslationOperationsTool
{
	public const string ToolName = "object_translation_operations";

	private readonly ILogger<ObjectTranslationOperationsTool> _logger;

	public static readonly ToolMetadata toolMetadata = new ToolMetadata
	{
		Operations = new Dictionary<string, OperationMetadata>(StringComparer.OrdinalIgnoreCase)
		{
			["Create"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Definitions" },
				Description = "Create object translations (single or multiple).\nMandatory properties for each translation: CultureName, ObjectType, Property, Value, and object identification properties based on ObjectType.\nOptional: CreateCultureIfNotExists (default: true), Options.UseTransaction (default: true), Options.ContinueOnError (default: false).\nObject identification requirements by ObjectType:\n- Model: ModelName\n- Table: TableName  \n- Measure/KPI: MeasureName (TableName optional)\n- Column: TableName, ColumnName\n- Hierarchy: TableName, HierarchyName\n- Level: TableName, HierarchyName, LevelName",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            { \n                \"CultureName\": \"fr-FR\", \n                \"ObjectType\": \"Table\", \n                \"TableName\": \"Sales\", \n                \"Property\": \"Caption\", \n                \"Value\": \"Table des Ventes\" \n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            { \n                \"CultureName\": \"es-ES\", \n                \"ObjectType\": \"Table\", \n                \"TableName\": \"Sales\", \n                \"Property\": \"Caption\", \n                \"Value\": \"Ventas\" \n            },\n            { \n                \"CultureName\": \"es-ES\", \n                \"ObjectType\": \"Measure\", \n                \"MeasureName\": \"Total Sales\", \n                \"TableName\": \"Sales\", \n                \"Property\": \"Caption\", \n                \"Value\": \"Ventas Totales\" \n            },\n            { \n                \"CultureName\": \"es-ES\", \n                \"ObjectType\": \"Column\", \n                \"TableName\": \"Sales\", \n                \"ColumnName\": \"Amount\", \n                \"Property\": \"Description\", \n                \"Value\": \"Importe de la venta\" \n            }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": true,\n            \"UseTransaction\": true\n        }\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            { \n                \"CultureName\": \"de-DE\", \n                \"ObjectType\": \"Column\", \n                \"TableName\": \"Products\", \n                \"ColumnName\": \"CategoryName\", \n                \"Property\": \"Caption\", \n                \"Value\": \"Kategoriename\" \n            }\n        ]\n    }\n}" }
			},
			["Update"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Definitions" },
				Description = "Update object translations (single or multiple).\nMandatory properties for each translation: CultureName, ObjectType, Property, Value, and object identification properties based on ObjectType.\nOptional: Options.UseTransaction (default: true), Options.ContinueOnError (default: false).\nObject identification requirements by ObjectType:\n- Model: ModelName\n- Table: TableName  \n- Measure/KPI: MeasureName (TableName optional)\n- Column: TableName, ColumnName\n- Hierarchy: TableName, HierarchyName\n- Level: TableName, HierarchyName, LevelName",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Update\",\n        \"Definitions\": [\n            { \n                \"CultureName\": \"fr-FR\", \n                \"ObjectType\": \"Table\", \n                \"TableName\": \"Sales\", \n                \"Property\": \"Caption\", \n                \"Value\": \"Tableau des Ventes Mis à Jour\" \n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Update\",\n        \"Definitions\": [\n            { \n                \"CultureName\": \"de-DE\", \n                \"ObjectType\": \"Table\", \n                \"TableName\": \"Sales\", \n                \"Property\": \"Caption\", \n                \"Value\": \"Verkäufe Aktualisiert\" \n            },\n            { \n                \"CultureName\": \"de-DE\", \n                \"ObjectType\": \"Measure\", \n                \"MeasureName\": \"Total Sales\", \n                \"TableName\": \"Sales\", \n                \"Property\": \"Caption\", \n                \"Value\": \"Gesamtumsatz\" \n            }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": true,\n            \"UseTransaction\": true\n        }\n    }\n}" }
			},
			["Delete"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Delete object translations (single or multiple).\nMandatory properties for each translation: CultureName, ObjectType, Property, and object identification properties based on ObjectType.\nOptional: Options.UseTransaction (default: true), Options.ContinueOnError (default: false).\nObject identification requirements by ObjectType:\n- Model: ModelName\n- Table: TableName  \n- Measure/KPI: MeasureName (TableName optional)\n- Column: TableName, ColumnName\n- Hierarchy: TableName, HierarchyName\n- Level: TableName, HierarchyName, LevelName",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Delete\",\n        \"References\": [\n            {\n                \"CultureName\": \"fr-FR\",\n                \"ObjectType\": \"Table\",\n                \"TableName\": \"Sales\",\n                \"Property\": \"Caption\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Delete\",\n        \"References\": [\n            {\n                \"CultureName\": \"it-IT\",\n                \"ObjectType\": \"Table\",\n                \"TableName\": \"Sales\",\n                \"Property\": \"Caption\"\n            },\n            {\n                \"CultureName\": \"it-IT\",\n                \"ObjectType\": \"Measure\",\n                \"MeasureName\": \"Total Sales\",\n                \"TableName\": \"Sales\",\n                \"Property\": \"Caption\"\n            },\n            {\n                \"CultureName\": \"it-IT\",\n                \"ObjectType\": \"Column\",\n                \"TableName\": \"Sales\",\n                \"ColumnName\": \"Amount\",\n                \"Property\": \"Description\"\n            }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": true,\n            \"UseTransaction\": true\n        }\n    }\n}" }
			},
			["Get"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Get object translations (single or multiple).\nMandatory properties for each translation: CultureName, ObjectType, Property, and object identification properties based on ObjectType.\nOptional: Options.ContinueOnError (default: false).\nObject identification requirements by ObjectType:\n- Model: ModelName\n- Table: TableName  \n- Measure/KPI: MeasureName (TableName optional)\n- Column: TableName, ColumnName\n- Hierarchy: TableName, HierarchyName\n- Level: TableName, HierarchyName, LevelName",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Get\",\n        \"References\": [\n            {\n                \"CultureName\": \"fr-FR\",\n                \"ObjectType\": \"Table\",\n                \"TableName\": \"Sales\",\n                \"Property\": \"Caption\"\n            }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"Get\",\n        \"References\": [\n            {\n                \"CultureName\": \"ja-JP\",\n                \"ObjectType\": \"Table\",\n                \"TableName\": \"Sales\",\n                \"Property\": \"Caption\"\n            },\n            {\n                \"CultureName\": \"ja-JP\",\n                \"ObjectType\": \"Measure\",\n                \"MeasureName\": \"Total Sales\",\n                \"TableName\": \"Sales\",\n                \"Property\": \"Caption\"\n            },\n            {\n                \"CultureName\": \"ja-JP\",\n                \"ObjectType\": \"Column\",\n                \"TableName\": \"Sales\",\n                \"ColumnName\": \"Amount\",\n                \"Property\": \"Description\"\n            }\n        ],\n        \"Options\": {\n            \"ContinueOnError\": true\n        }\n    }\n}" }
			},
			["List"] = new OperationMetadata
			{
				Description = "List all object translations, with optional filters.\nOptional: ListFilters (with FilterCultureName, FilterObjectType, FilterObjectName, FilterProperty).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"List\",\n        \"ListFilters\": {\n            \"FilterCultureName\": \"fr-FR\"\n        }\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"List\",\n        \"ListFilters\": {\n            \"FilterObjectType\": \"Measure\"\n        }\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"List\",\n        \"ListFilters\": {\n            \"FilterCultureName\": \"es-ES\",\n            \"FilterObjectType\": \"Column\",\n            \"FilterObjectName\": \"Sales\"\n        }\n    }\n}" }
			},
			["Help"] = new OperationMetadata
			{
				Description = "Describe the tool and its operations.\nNo mandatory properties required.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Help\"\n    }\n}" }
			}
		}
	};

	public ObjectTranslationOperationsTool(ILogger<ObjectTranslationOperationsTool> logger)
	{
		_logger = logger;
	}

	[McpServerTool(Name = "object_translation_operations", Title = "Object Translation Operations", ReadOnly = false, Destructive = true)]
	[YamlToolDescription("object_translation_operations")]
	public async Task<CallToolResult> ExecuteObjectTranslationOperation(McpServer mcpServer, ObjectTranslationOperationRequest request, IWriteGuard writeGuard)
	{
		_logger.LogDebug("Executing {ToolName}.{Operation}: Connection={ConnectionName}", "ObjectTranslationOperationsTool", request.Operation, request.ConnectionName ?? "(last used)");
		string[] validOperations = new string[6] { "CREATE", "UPDATE", "DELETE", "GET", "LIST", "HELP" };
		string[] writeOperations = new string[3] { "CREATE", "UPDATE", "DELETE" };
		string op = request.Operation.ToUpperInvariant();
		ToolCallAnnotations annotations = ToolCallAnnotations.Create("object_translation_operations", request.Operation, !Enumerable.Contains(writeOperations, op));
		CallToolResult result = null;
		try
		{
			if (!Enumerable.Contains(validOperations, op))
			{
				_logger.LogWarning("Invalid operation '{Operation}' requested for {ToolName}. Valid operations: {ValidOperations}", request.Operation, "ObjectTranslationOperationsTool", string.Join(", ", validOperations));
				CallToolResult result2;
				result = (result2 = CallToolResultHelper.FromResponse(ObjectTranslationOperationResponse.Forbidden(request.Operation, "Invalid operation: " + request.Operation + ". Supported operations: " + string.Join(", ", validOperations)), annotations));
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
					_logger.LogWarning("{ToolName}.{Operation} blocked by write guard: {Reason}", "ObjectTranslationOperationsTool", request.Operation, writeOperationResult.Message);
					CallToolResult result2;
					result = (result2 = CallToolResultHelper.FromResponse(ObjectTranslationOperationResponse.Forbidden(request.Operation, writeOperationResult.Message), annotations));
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
				"HELP" => CallToolResultHelper.FromResponse(HandleHelpOperation(request, isWriteEnabled ? validOperations : validOperations.Except(writeOperations).ToArray()), annotations), 
				_ => CallToolResultHelper.FromResponse(ObjectTranslationOperationResponse.Forbidden(request.Operation, "Operation " + request.Operation + " is not implemented"), annotations), 
			});
			return result3;
		}
		catch (Exception ex)
		{
			_logger.LogOperationError("ObjectTranslationOperationsTool", request.Operation, ex);
			string message = op switch
			{
				"CREATE" => "Failed to create object translation: " + ex.GetErrorMessage(), 
				"UPDATE" => "Failed to update object translation: " + ex.GetErrorMessage(), 
				"DELETE" => "Failed to delete object translation: " + ex.GetErrorMessage(), 
				"GET" => "Failed to get object translation: " + ex.GetErrorMessage(), 
				"LIST" => "Failed to list object translations: " + ex.GetErrorMessage(), 
				_ => "Error executing object translation operation: " + ex.GetErrorMessage(), 
			};
			CallToolResult result2;
			result = (result2 = CallToolResultHelper.FromResponse(new ObjectTranslationOperationResponse
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

	private async Task<ObjectTranslationOperationResponse> HandleCreateOperation(ObjectTranslationOperationRequest request)
	{
		if (request.Definitions == null || !request.Definitions.Any())
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Definitions must be provided and cannot be empty", ErrorSource.User);
		}
		BatchOperationResponse batchOperationResponse = await ObjectTranslationOperations.CreateObjectTranslations(request.ConnectionName, request.Definitions, request.Options);
		ObjectTranslationOperationResponse objectTranslationOperationResponse = new ObjectTranslationOperationResponse
		{
			Success = batchOperationResponse.Success,
			Message = batchOperationResponse.Message,
			Operation = request.Operation,
			Summary = batchOperationResponse.Summary,
			Results = batchOperationResponse.Results,
			Warnings = batchOperationResponse.Warnings
		};
		if (request.Definitions.Count == 1)
		{
			List<ItemResult> results = batchOperationResponse.Results;
			if (results != null && results.Count > 0 && batchOperationResponse.Results[0].Data is ObjectTranslationOperations.ObjectTranslationOperationResult objectTranslationOperationResult)
			{
				objectTranslationOperationResponse.CultureName = objectTranslationOperationResult.CultureName;
				objectTranslationOperationResponse.ObjectType = objectTranslationOperationResult.ObjectType;
				objectTranslationOperationResponse.ObjectDisplayName = objectTranslationOperationResult.ObjectDisplayName;
				objectTranslationOperationResponse.Property = objectTranslationOperationResult.Property;
				objectTranslationOperationResponse.Value = objectTranslationOperationResult.Value;
				objectTranslationOperationResponse.Data = objectTranslationOperationResult;
			}
		}
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, {SuccessCount}/{TotalCount} succeeded", "ObjectTranslationOperationsTool", request.Operation, request.ConnectionName, batchOperationResponse.Summary?.SuccessCount, batchOperationResponse.Summary?.TotalItems);
		return objectTranslationOperationResponse;
	}

	private async Task<ObjectTranslationOperationResponse> HandleUpdateOperation(ObjectTranslationOperationRequest request)
	{
		if (request.Definitions == null || !request.Definitions.Any())
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Definitions must be provided and cannot be empty", ErrorSource.User);
		}
		BatchOperationResponse batchOperationResponse = await ObjectTranslationOperations.UpdateObjectTranslations(request.ConnectionName, request.Definitions, request.Options);
		ObjectTranslationOperationResponse objectTranslationOperationResponse = new ObjectTranslationOperationResponse
		{
			Success = batchOperationResponse.Success,
			Message = batchOperationResponse.Message,
			Operation = request.Operation,
			Summary = batchOperationResponse.Summary,
			Results = batchOperationResponse.Results,
			Warnings = batchOperationResponse.Warnings
		};
		if (request.Definitions.Count == 1)
		{
			List<ItemResult> results = batchOperationResponse.Results;
			if (results != null && results.Count > 0 && batchOperationResponse.Results[0].Data is ObjectTranslationOperations.ObjectTranslationOperationResult objectTranslationOperationResult)
			{
				objectTranslationOperationResponse.CultureName = objectTranslationOperationResult.CultureName;
				objectTranslationOperationResponse.ObjectType = objectTranslationOperationResult.ObjectType;
				objectTranslationOperationResponse.ObjectDisplayName = objectTranslationOperationResult.ObjectDisplayName;
				objectTranslationOperationResponse.Property = objectTranslationOperationResult.Property;
				objectTranslationOperationResponse.Value = objectTranslationOperationResult.Value;
				objectTranslationOperationResponse.Data = objectTranslationOperationResult;
			}
		}
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, {SuccessCount}/{TotalCount} succeeded", "ObjectTranslationOperationsTool", request.Operation, request.ConnectionName, batchOperationResponse.Summary?.SuccessCount, batchOperationResponse.Summary?.TotalItems);
		return objectTranslationOperationResponse;
	}

	private async Task<ObjectTranslationOperationResponse> HandleDeleteOperation(ObjectTranslationOperationRequest request)
	{
		if (request.References == null || !request.References.Any())
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("References must be provided and cannot be empty", ErrorSource.User);
		}
		BatchOperationResponse batchOperationResponse = await ObjectTranslationOperations.DeleteObjectTranslations(request.ConnectionName, request.References, request.Options);
		ObjectTranslationOperationResponse objectTranslationOperationResponse = new ObjectTranslationOperationResponse
		{
			Success = batchOperationResponse.Success,
			Message = batchOperationResponse.Message,
			Operation = request.Operation,
			Summary = batchOperationResponse.Summary,
			Results = batchOperationResponse.Results,
			Warnings = batchOperationResponse.Warnings
		};
		if (request.References.Count == 1)
		{
			List<ItemResult> results = batchOperationResponse.Results;
			if (results != null && results.Count > 0 && batchOperationResponse.Results[0].Data is ObjectTranslationOperations.ObjectTranslationOperationResult objectTranslationOperationResult)
			{
				objectTranslationOperationResponse.CultureName = objectTranslationOperationResult.CultureName;
				objectTranslationOperationResponse.ObjectType = objectTranslationOperationResult.ObjectType;
				objectTranslationOperationResponse.ObjectDisplayName = objectTranslationOperationResult.ObjectDisplayName;
				objectTranslationOperationResponse.Property = objectTranslationOperationResult.Property;
				objectTranslationOperationResponse.Data = objectTranslationOperationResult;
			}
		}
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, {SuccessCount}/{TotalCount} succeeded", "ObjectTranslationOperationsTool", request.Operation, request.ConnectionName, batchOperationResponse.Summary?.SuccessCount, batchOperationResponse.Summary?.TotalItems);
		return objectTranslationOperationResponse;
	}

	private async Task<ObjectTranslationOperationResponse> HandleGetOperation(ObjectTranslationOperationRequest request)
	{
		if (request.References == null || !request.References.Any())
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("References must be provided and cannot be empty", ErrorSource.User);
		}
		BatchOperationResponse batchOperationResponse = await ObjectTranslationOperations.GetObjectTranslations(request.ConnectionName, request.References, request.Options);
		ObjectTranslationOperationResponse objectTranslationOperationResponse = new ObjectTranslationOperationResponse
		{
			Success = batchOperationResponse.Success,
			Message = batchOperationResponse.Message,
			Operation = request.Operation,
			Summary = batchOperationResponse.Summary,
			Results = batchOperationResponse.Results,
			Warnings = batchOperationResponse.Warnings
		};
		if (request.References.Count == 1)
		{
			List<ItemResult> results = batchOperationResponse.Results;
			if (results != null && results.Count > 0)
			{
				if (batchOperationResponse.Results[0].Data is ObjectTranslationGet objectTranslationGet)
				{
					objectTranslationOperationResponse.CultureName = objectTranslationGet.CultureName;
					objectTranslationOperationResponse.ObjectType = objectTranslationGet.ObjectType;
					objectTranslationOperationResponse.ObjectDisplayName = TranslationHelper.GetObjectDisplayName(objectTranslationGet);
					objectTranslationOperationResponse.Property = objectTranslationGet.Property;
					objectTranslationOperationResponse.Value = objectTranslationGet.Value;
					objectTranslationOperationResponse.Data = objectTranslationGet;
				}
				goto IL_0223;
			}
		}
		if (batchOperationResponse.Results != null && batchOperationResponse.Results.Count > 0)
		{
			List<object> data = (from r in batchOperationResponse.Results
				where r.Success && r.Data != null
				select r.Data).ToList();
			objectTranslationOperationResponse.Data = data;
		}
		goto IL_0223;
		IL_0223:
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, {SuccessCount}/{TotalCount} succeeded", "ObjectTranslationOperationsTool", request.Operation, request.ConnectionName, batchOperationResponse.Summary?.SuccessCount, batchOperationResponse.Summary?.TotalItems);
		return objectTranslationOperationResponse;
	}

	private async Task<ObjectTranslationOperationResponse> HandleListOperation(ObjectTranslationOperationRequest request)
	{
		string cultureName = request.Filter?.CultureName;
		string objectType = request.Filter?.ObjectType;
		string objectName = request.Filter?.ObjectName;
		List<ObjectTranslationList> list = await ObjectTranslationOperations.ListObjectTranslations(request.ConnectionName, cultureName, objectType, objectName);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Count={Count}", "ObjectTranslationOperationsTool", request.Operation, request.ConnectionName, list.Count);
		return new ObjectTranslationOperationResponse
		{
			Success = true,
			Message = $"Retrieved {list.Count} object translations",
			Operation = request.Operation,
			Data = list
		};
	}

	private ObjectTranslationOperationResponse HandleHelpOperation(ObjectTranslationOperationRequest request, string[] operations)
	{
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Operations={OperationCount}", "ObjectTranslationOperationsTool", request.Operation, request.ConnectionName, operations.Length);
		ObjectTranslationOperationResponse objectTranslationOperationResponse = new ObjectTranslationOperationResponse();
		objectTranslationOperationResponse.Success = true;
		objectTranslationOperationResponse.Message = "Tool description retrieved successfully";
		objectTranslationOperationResponse.Operation = request.Operation;
		objectTranslationOperationResponse.Help = new
		{
			ToolName = "object_translation_operations",
			Description = "Perform operations on object translations within semantic model cultures.",
			SupportedOperations = operations,
			Examples = toolMetadata.Operations.Where<KeyValuePair<string, OperationMetadata>>((KeyValuePair<string, OperationMetadata> p) => operations.Contains<string>(p.Key, StringComparer.OrdinalIgnoreCase)),
			SupportedObjectTypes = TranslationHelper.ValidObjectTypes.ToArray(),
			ObjectIdentificationRequirements = new
			{
				Model = new string[1] { "ModelName" },
				Table = new string[1] { "TableName" },
				Measure = new string[2] { "MeasureName", "TableName (optional)" },
				Column = new string[2] { "TableName", "ColumnName" },
				Hierarchy = new string[2] { "TableName", "HierarchyName" },
				Level = new string[3] { "TableName", "HierarchyName", "LevelName" },
				KPI = new string[2] { "MeasureName", "TableName (optional)" }
			},
			Notes = new string[8] { "Use Definitions for Create/Update operations (batch processing supported).", "Use References for Delete/Get operations (batch processing supported).", "Single-item operations are just lists with one element.", "Use Options.UseTransaction (default: true) to control transaction behavior.", "Use Options.ContinueOnError (default: false) to continue processing after failures.", "For List operation, use optional filters (FilterCultureName, FilterObjectType, FilterObjectName).", "Object identification uses specific properties instead of composite names.", "Response includes Summary (TotalItems, SuccessCount, FailureCount, ExecutionTime) and Results array with per-item details." }
		};
		return objectTranslationOperationResponse;
	}

	private (bool isValid, string? errorMessage) ValidateRequest(string operation, ObjectTranslationOperationRequest request)
	{
		if (!toolMetadata.Operations.TryGetValue(operation, out OperationMetadata _))
		{
			return (isValid: true, errorMessage: null);
		}
		switch (operation.ToUpperInvariant())
		{
		case "CREATE":
		case "UPDATE":
			if (request.Definitions == null || !request.Definitions.Any())
			{
				string item2 = "Definitions must be provided and cannot be empty for Create/Update operations";
				return (isValid: false, errorMessage: item2);
			}
			break;
		case "DELETE":
		case "GET":
			if (request.References == null || !request.References.Any())
			{
				string item = "References must be provided and cannot be empty for Delete/Get operations";
				return (isValid: false, errorMessage: item);
			}
			break;
		}
		return (isValid: true, errorMessage: null);
	}
}
