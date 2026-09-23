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
public class DatabaseOperationsTool
{
	public const string ToolName = "database_operations";

	private readonly ILogger<DatabaseOperationsTool> _logger;

	private readonly MCPServerConfiguration _config;

	public static readonly ToolMetadata toolMetadata = new ToolMetadata
	{
		Operations = new Dictionary<string, OperationMetadata>(StringComparer.OrdinalIgnoreCase)
		{
			["List"] = new OperationMetadata
			{
				Description = "List all databases on the server.\nFor offline connections, returns information about the single loaded database.\nFor online connections, lists all databases on the server.\nMandatory properties: None.\nOptional: ConnectionName.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"List\"\n    }\n}" }
			},
			["Update"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "UpdateDefinition" },
				Description = "Update an existing database's modifiable properties.\nNames cannot be changed - use existing database name only.\nSupports updating Description, CompatibilityLevel, Collation, Language, and Annotations.\nMandatory properties: UpdateDefinition. The database name can come from UpdateDefinition.Name or the currently bound database on the connection.\nOptional: CompatibilityLevel, Description, Collation, Language, Annotations.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Update\",\n        \"ConnectionName\": \"MyConnection\",\n        \"UpdateDefinition\": { \n            \"Name\": \"SalesDB\",\n            \"Description\": \"Updated Sales database\"\n        }\n    }\n}" }
			},
			["Create"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "CreateDefinition" },
				Description = "Create a new empty database with an empty model.\nCurrently only supports offline database creation (IsOffline defaults to true).\nCreates a new offline connection automatically if ConnectionName not provided or doesn't exist.\nMandatory properties: CreateDefinition (with Name).\nOptional: IsOffline (defaults to true), Description, CompatibilityLevel, Collation, Language, Annotations, ModelName.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"CreateDefinition\": { \n            \"Name\": \"NewDB\",\n            \"IsOffline\": true\n        }\n    }\n}" }
			},
			["ImportFromTmdlFolder"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "TmdlFolderPath" },
				Description = "Create an offline connection from a TMDL folder.\nImports and deserializes a TMDL folder structure into a new offline database connection.\nAutomatically generates connection name if not provided.\nMandatory properties: TmdlFolderPath.\nOptional: ConnectionName (auto-generated if not provided).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ImportFromTmdlFolder\",\n        \"TmdlFolderPath\": \"C:/TMDL/Sales.SemanticModel/defintion\"\n    }\n}" }
			},
			["ExportToTmdlFolder"] = new OperationMetadata
			{
				Description = "Serialize a database to a TMDL folder.\nFor online connections, TmdlFolderPath is mandatory.\nFor offline connections, uses stored folder path if available, otherwise TmdlFolderPath is mandatory.\nMandatory properties: TmdlFolderPath (for online connections or offline connections without stored path).\nOptional: TmdlFolderPath (for offline connections with stored path).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ExportToTmdlFolder\",\n        \"ConnectionName\": \"MyConnection\",\n        \"TmdlFolderPath\": \"C:/TMDL/SalesDB\"\n    }\n}" }
			},
			["ImportFromBimFile"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "BimFilePath" },
				Description = "Create an offline connection from a .bim (JSON-serialized TOM) file.\nImports and deserializes a .bim file into a new offline database connection.\nAutomatically generates connection name if not provided.\nMandatory properties: BimFilePath.\nOptional: ConnectionName (auto-generated if not provided).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ImportFromBimFile\",\n        \"BimFilePath\": \"C:/Models/Sales.bim\"\n    }\n}" }
			},
			["ExportToBimFile"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "BimFilePath" },
				Description = "Serialize a database to a .bim (JSON-serialized TOM) file.\nExports the database from the current connection to a .bim file.\nMandatory properties: BimFilePath.\nOptional: ConnectionName.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ExportToBimFile\",\n        \"ConnectionName\": \"MyConnection\",\n        \"BimFilePath\": \"C:/Models/SalesDB.bim\"\n    }\n}" }
			},
			["ExportTMDL"] = new OperationMetadata
			{
				Description = "Export database to TMDL (YAML-like syntax) format.\nTMDL is a human-readable, declarative format for semantic models.\nFor offline connections, exports the loaded database.\nOptional: TmdlExportOptions (TmdlSerializationOptions.IncludeChildren defaults to false).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ExportTMDL\",\n        \"ConnectionName\": \"MyConnection\",\n        \"TmdlExportOptions\": {\n            \"FilePath\": \"C:/TMDL/SalesDB.tmdl\",\n            \"TmdlSerializationOptions\": {\n                \"IncludeChildren\": true\n            }\n        }\n    }\n}" }
			},
			["ExportTMSL"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "TmslExportOptions" },
				Description = "Export database to TMSL (JSON syntax) script format with specified operation type.\nTMSL generates executable JSON scripts for database operations.\nFor offline connections, exports the loaded database.\nMandatory properties: TmslExportOptions (with TmslOperationType).\nOptional: RefreshType, IncludeRestricted, MaxReturnCharacters.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ExportTMSL\",\n        \"ConnectionName\": \"MyConnection\",\n        \"TmslExportOptions\": {\n            \"TmslOperationType\": \"CreateOrReplace\"\n        }\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"ExportTMSL\",\n        \"TmslExportOptions\": {\n            \"FilePath\": \"C:/TMDL/SalesDB.tmsl\",\n            \"TmslOperationType\": \"Refresh\",\n            \"RefreshType\": \"Full\",\n            \"IncludeRestricted\": true\n        }\n    }\n}" }
			},
			["DeployToFabric"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "DeployToFabricRequest" },
				Description = "Deploy a database to Fabric using TMSL createOrReplace script.\nSupports deployment via direct connection string or workspace name.\nRequires either TargetConnectionString OR TargetWorkspaceName in the request.\nMandatory properties: DeployToFabricRequest (with either TargetConnectionString OR TargetWorkspaceName).\nOptional: ConnectionName (uses last used connection if omitted), TargetTenantName (defaults to 'myorg'), NewDatabaseName, IncludeRestricted, ConnectTimeoutSeconds, ClearCredential (defaults to false).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"DeployToFabric\",\n        \"ConnectionName\": \"SourceConn\",\n        \"DeployToFabricRequest\": {\n            \"TargetConnectionString\": \"Data Source=powerbi://api.powerbi.com/v1.0/myorg/MyWorkspace\",\n            \"NewDatabaseName\": \"DeployedModel\",\n            \"ConnectTimeoutSeconds\": 300\n        }\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"DeployToFabric\",\n        \"ConnectionName\": \"SourceConn\",\n        \"DeployToFabricRequest\": {\n            \"TargetWorkspaceName\": \"MyWorkspace\",\n            \"TargetTenantName\": \"myorg\",\n            \"NewDatabaseName\": \"DeployedModel\",\n            \"IncludeRestricted\": false\n        }\n    }\n}" }
			},
			["Help"] = new OperationMetadata
			{
				Description = "Describe the tool and its operations.\nProvides comprehensive information about all available database operations.\nMandatory properties: None.\nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Help\"\n    }\n}" }
			}
		}
	};

	public DatabaseOperationsTool(ILogger<DatabaseOperationsTool> logger, MCPServerConfiguration config)
	{
		_logger = logger;
		_config = config;
	}

	[McpServerTool(Name = "database_operations", Title = "Database Operations", ReadOnly = false, Destructive = true)]
	[YamlToolDescription("database_operations")]
	public async Task<CallToolResult> ExecuteDatabaseOperation(McpServer mcpServer, DatabaseOperationRequest request, IWriteGuard writeGuard)
	{
		_logger.LogDebug("Executing {ToolName}.{Operation}: Connection={ConnectionName}", "DatabaseOperationsTool", request.Operation, request.ConnectionName ?? "(last used)");
		string[] validOperations = new string[11]
		{
			"HELP", "CREATE", "UPDATE", "LIST", "IMPORTFROMTMDLFOLDER", "EXPORTTOTMDLFOLDER", "IMPORTFROMBIMFILE", "EXPORTTOBIMFILE", "EXPORTTMDL", "EXPORTTMSL",
			"DEPLOYTOFABRIC"
		};
		string[] writeOperations = new string[2] { "UPDATE", "DEPLOYTOFABRIC" };
		string op = request.Operation.ToUpperInvariant();
		ToolCallAnnotations annotations = ToolCallAnnotations.Create("database_operations", request.Operation, !Enumerable.Contains(writeOperations, op));
		CallToolResult result = null;
		try
		{
			if (!Enumerable.Contains(validOperations, op))
			{
				_logger.LogWarning("Invalid operation '{Operation}' requested for {ToolName}. Valid operations: {ValidOperations}", request.Operation, "DatabaseOperationsTool", string.Join(", ", validOperations));
				CallToolResult result2;
				result = (result2 = CallToolResultHelper.FromResponse(DatabaseOperationResponse.Forbidden(request.Operation, "Invalid operation: " + request.Operation + ". Supported operations: " + string.Join(", ", validOperations)), annotations));
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
					_logger.LogWarning("{ToolName}.{Operation} blocked by write guard: {Reason}", "DatabaseOperationsTool", request.Operation, writeOperationResult.Message);
					CallToolResult result2;
					result = (result2 = CallToolResultHelper.FromResponse(DatabaseOperationResponse.Forbidden(request.Operation, writeOperationResult.Message), annotations));
					return result2;
				}
			}
			bool isWriteEnabled = writeGuard.IsWriteEnabled;
			if (op == "EXPORTTMDL")
			{
				DatabaseOperationResponse obj = await HandleExportTMDLOperation(request);
				return CallToolResultHelper.FromExportResponse(obj, (obj.DatabaseName ?? "database") + ".tmdl", "text/plain", annotations);
			}
			if (op == "EXPORTTMSL")
			{
				DatabaseOperationResponse obj2 = await HandleExportTMSLOperation(request);
				return CallToolResultHelper.FromExportResponse(obj2, (obj2.DatabaseName ?? "database") + ".json", "text/plain", annotations);
			}
			CallToolResult result3;
			result = (result3 = op switch
			{
				"HELP" => CallToolResultHelper.FromResponse(HandleHelpOperation(request, isWriteEnabled ? validOperations : validOperations.Except(writeOperations).ToArray()), annotations), 
				"CREATE" => CallToolResultHelper.FromResponse(await HandleCreateOperation(request), annotations, null, minimalSuccessPayload: true), 
				"UPDATE" => CallToolResultHelper.FromResponse(await HandleUpdateOperation(request), annotations, null, minimalSuccessPayload: true), 
				"LIST" => CallToolResultHelper.FromResponse(await HandleListOperation(request), annotations), 
				"IMPORTFROMTMDLFOLDER" => CallToolResultHelper.FromResponse(HandleImportFromTmdlFolderOperation(request), annotations, null, minimalSuccessPayload: true), 
				"EXPORTTOTMDLFOLDER" => CallToolResultHelper.FromResponse(await HandleExportToTmdlFolderOperation(request), annotations), 
				"IMPORTFROMBIMFILE" => CallToolResultHelper.FromResponse(HandleImportFromBimFileOperation(request), annotations, null, minimalSuccessPayload: true), 
				"EXPORTTOBIMFILE" => CallToolResultHelper.FromResponse(await HandleExportToBimFileOperation(request), annotations), 
				"DEPLOYTOFABRIC" => CallToolResultHelper.FromResponse(await HandleDeployToFabricOperation(request), annotations, null, minimalSuccessPayload: true), 
				_ => CallToolResultHelper.FromResponse(DatabaseOperationResponse.Forbidden(request.Operation, "Operation " + request.Operation + " is not implemented"), annotations), 
			});
			return result3;
		}
		catch (Exception ex)
		{
			_logger.LogOperationError("DatabaseOperationsTool", request.Operation, ex);
			string message = op switch
			{
				"CREATE" => "Error creating database: " + ex.GetErrorMessage(), 
				"UPDATE" => "Error updating database: " + ex.GetErrorMessage(), 
				"LIST" => "Error listing databases: " + ex.GetErrorMessage(), 
				"IMPORTFROMTMDLFOLDER" => "Error importing database from TMDL folder: " + ex.GetErrorMessage(), 
				"EXPORTTOTMDLFOLDER" => "Error exporting database to TMDL folder: " + ex.GetErrorMessage(), 
				"IMPORTFROMBIMFILE" => "Error importing database from BIM file: " + ex.GetErrorMessage(), 
				"EXPORTTOBIMFILE" => "Error exporting database to BIM file: " + ex.GetErrorMessage(), 
				"EXPORTTMDL" => "Failed to export TMDL: " + ex.GetErrorMessage(), 
				"EXPORTTMSL" => "Failed to export TMSL: " + ex.GetErrorMessage(), 
				"DEPLOYTOFABRIC" => "Error deploying database to Fabric: " + ex.GetErrorMessage(), 
				_ => "Error executing database operation: " + ex.GetErrorMessage(), 
			};
			CallToolResult result2;
			result = (result2 = CallToolResultHelper.FromResponse(new DatabaseOperationResponse
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

	private async Task<DatabaseOperationResponse> HandleListOperation(DatabaseOperationRequest request)
	{
		List<DatabaseGet> list = await DatabaseOperations.ListDatabases(request.ConnectionName);
		_logger.LogInformation("{ToolName}.{Operation} completed: Count={Count}", "DatabaseOperationsTool", "List", list.Count);
		return new DatabaseOperationResponse
		{
			Success = true,
			Message = $"Found {list.Count} databases on server",
			Operation = request.Operation,
			Data = list
		};
	}

	private async Task<DatabaseOperationResponse> HandleUpdateOperation(DatabaseOperationRequest request)
	{
		if (request.UpdateDefinition == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("UpdateDefinition is required for Update operation", ErrorSource.User);
		}
		DatabaseOperationResult databaseOperationResult = await DatabaseOperations.UpdateDatabase(request.ConnectionName, request.UpdateDefinition);
		List<string> list = new List<string>();
		if (!databaseOperationResult.HasChanges)
		{
			list.Add("No changes were detected. The database is already in the requested state.");
		}
		_logger.LogInformation("{ToolName}.{Operation} completed: HasChanges={HasChanges}", "DatabaseOperationsTool", "Update", databaseOperationResult.HasChanges);
		if (list.Any())
		{
			foreach (string item in list)
			{
				_logger.LogOperationWarning("DatabaseOperationsTool", request.Operation, item);
			}
		}
		return new DatabaseOperationResponse
		{
			Success = true,
			Message = (databaseOperationResult.HasChanges ? ("Database '" + databaseOperationResult.DatabaseName + "' updated successfully") : ("Database '" + databaseOperationResult.DatabaseName + "' is already in the requested state")),
			Operation = request.Operation,
			DatabaseName = databaseOperationResult.DatabaseName,
			Data = databaseOperationResult,
			Warnings = list
		};
	}

	private DatabaseOperationResponse HandleImportFromTmdlFolderOperation(DatabaseOperationRequest request)
	{
		TmdlDeserializeResult tmdlDeserializeResult = DatabaseOperations.ImportFromTmdlFolder(request.TmdlFolderPath, request.ConnectionName);
		_logger.LogInformation("{ToolName}.{Operation} completed", "DatabaseOperationsTool", "ImportFromTmdlFolder");
		return new DatabaseOperationResponse
		{
			Success = tmdlDeserializeResult.Success,
			Message = "Successfully created offline connection '" + tmdlDeserializeResult.ConnectionName + "' from TMDL folder",
			Operation = request.Operation,
			DatabaseName = tmdlDeserializeResult.DatabaseName,
			Data = tmdlDeserializeResult
		};
	}

	private async Task<DatabaseOperationResponse> HandleExportToTmdlFolderOperation(DatabaseOperationRequest request)
	{
		TmdlSerializeResult tmdlSerializeResult = await DatabaseOperations.ExportToTmdlFolder(request.ConnectionName, request.TmdlFolderPath);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "DatabaseOperationsTool", "ExportToTmdlFolder", request.ConnectionName);
		return new DatabaseOperationResponse
		{
			Success = tmdlSerializeResult.Success,
			Message = $"Successfully exported database '{tmdlSerializeResult.DatabaseName}' to TMDL folder '{tmdlSerializeResult.FolderPath}'",
			Operation = request.Operation,
			DatabaseName = tmdlSerializeResult.DatabaseName,
			Data = tmdlSerializeResult
		};
	}

	private DatabaseOperationResponse HandleImportFromBimFileOperation(DatabaseOperationRequest request)
	{
		BimDeserializeResult bimDeserializeResult = DatabaseOperations.ImportFromBimFile(request.BimFilePath, request.ConnectionName, _config.Compatibility);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "DatabaseOperationsTool", "ImportFromBimFile", request.ConnectionName);
		return new DatabaseOperationResponse
		{
			Success = bimDeserializeResult.Success,
			Message = (bimDeserializeResult.Message ?? (bimDeserializeResult.Success ? ("Successfully created offline connection '" + bimDeserializeResult.ConnectionName + "' from BIM file") : ("Failed to create offline connection '" + bimDeserializeResult.ConnectionName + "' from BIM file"))),
			Operation = request.Operation,
			DatabaseName = bimDeserializeResult.DatabaseName,
			Data = bimDeserializeResult
		};
	}

	private async Task<DatabaseOperationResponse> HandleExportToBimFileOperation(DatabaseOperationRequest request)
	{
		BimSerializeResult bimSerializeResult = await DatabaseOperations.ExportToBimFile(request.BimFilePath, request.ConnectionName);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "DatabaseOperationsTool", "ExportToBimFile", request.ConnectionName);
		return new DatabaseOperationResponse
		{
			Success = bimSerializeResult.Success,
			Message = (bimSerializeResult.Message ?? (bimSerializeResult.Success ? $"Successfully exported database '{bimSerializeResult.DatabaseName}' to BIM file '{bimSerializeResult.FilePath}'" : $"Failed to export database '{bimSerializeResult.DatabaseName}' to BIM file '{bimSerializeResult.FilePath}'")),
			Operation = request.Operation,
			DatabaseName = bimSerializeResult.DatabaseName,
			Data = bimSerializeResult
		};
	}

	private async Task<DatabaseOperationResponse> HandleDeployToFabricOperation(DatabaseOperationRequest request)
	{
		DatabaseOperationResponse databaseOperationResponse = await DatabaseOperations.DeployToFabric(request.ConnectionName, request.DeployToFabricRequest);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "DatabaseOperationsTool", "DeployToFabric", request.ConnectionName);
		if (databaseOperationResponse.Warnings != null && databaseOperationResponse.Warnings.Any())
		{
			foreach (string warning in databaseOperationResponse.Warnings)
			{
				_logger.LogOperationWarning("DatabaseOperationsTool", request.Operation, warning);
			}
		}
		return databaseOperationResponse;
	}

	private async Task<DatabaseOperationResponse> HandleCreateOperation(DatabaseOperationRequest request)
	{
		var (flag, message) = ValidateRequest(request.Operation, request);
		if (!flag)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage(message, ErrorSource.User);
		}
		DatabaseCreate? createDefinition = request.CreateDefinition;
		if (createDefinition != null && createDefinition.IsOffline == false)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Online database creation is not currently supported. Only offline databases can be created. Set IsOffline to true or omit the property (defaults to true).", ErrorSource.User);
		}
		List<string> warnings = new List<string>();
		DatabaseCreate? createDefinition2 = request.CreateDefinition;
		if (createDefinition2 == null || !createDefinition2.IsOffline.HasValue)
		{
			warnings.Add("Creating offline database (this is the only supported create operation).");
		}
		if (!string.IsNullOrEmpty(request.ConnectionName) && ConnectionOperations.Exists(request.ConnectionName))
		{
			throw new McpExceptionWithSource("Connection '" + request.ConnectionName + "' already exists. For creating a new database, you can only create a new offline database in a new connection. Cannot create new database on existing connections.", ErrorSource.User, "Connection already exists.");
		}
		DatabaseCreateResult databaseCreateResult = await DatabaseOperations.CreateOfflineDb(request.CreateDefinition, request.ConnectionName, _config.ProToolingValue);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "DatabaseOperationsTool", "Create", request.ConnectionName);
		if (warnings.Any())
		{
			foreach (string item in warnings)
			{
				_logger.LogOperationWarning("DatabaseOperationsTool", request.Operation, item);
			}
		}
		return new DatabaseOperationResponse
		{
			Success = true,
			Message = (databaseCreateResult.Message ?? ("Successfully created offline database '" + databaseCreateResult.DatabaseName + "'")),
			Operation = request.Operation,
			DatabaseName = databaseCreateResult.DatabaseName,
			Warnings = ((warnings.Count > 0) ? warnings : null),
			Data = databaseCreateResult
		};
	}

	private async Task<DatabaseOperationResponse> HandleExportTMDLOperation(DatabaseOperationRequest request)
	{
		TmdlExportResult tmdlExportResult = await DatabaseOperations.ExportTMDL(request.ConnectionName, request.TmdlExportOptions ?? new DatabaseExportTmdl());
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "DatabaseOperationsTool", "ExportTMDL", request.ConnectionName);
		return new DatabaseOperationResponse
		{
			Success = tmdlExportResult.Success,
			Message = (tmdlExportResult.Success ? ("TMDL exported for database '" + (tmdlExportResult.ObjectName ?? "loaded database") + "'") : (tmdlExportResult.ErrorMessage ?? "Failed to export TMDL")),
			ErrorSource = tmdlExportResult.ErrorSource,
			Operation = request.Operation,
			DatabaseName = tmdlExportResult.ObjectName,
			Data = tmdlExportResult.Content
		};
	}

	private async Task<DatabaseOperationResponse> HandleExportTMSLOperation(DatabaseOperationRequest request)
	{
		TmslExportResult tmslExportResult = await DatabaseOperations.ExportTMSL(request.ConnectionName, request.TmslExportOptions);
		_logger.LogInformation("{ToolName}.{Operation} completed: OperationType={OperationType}, ConnectionName={ConnectionName}", "DatabaseOperationsTool", "ExportTMSL", tmslExportResult.OperationType, request.ConnectionName);
		return new DatabaseOperationResponse
		{
			Success = tmslExportResult.Success,
			Message = (tmslExportResult.Success ? $"TMSL {tmslExportResult.OperationType} script for database '{tmslExportResult.ObjectName ?? "loaded database"}' generated successfully" : (tmslExportResult.ErrorMessage ?? "Unknown error")),
			ErrorSource = tmslExportResult.ErrorSource,
			Operation = request.Operation,
			DatabaseName = tmslExportResult.ObjectName,
			Data = tmslExportResult.Content
		};
	}

	private DatabaseOperationResponse HandleHelpOperation(DatabaseOperationRequest request, string[] operations)
	{
		_logger.LogInformation("{ToolName}.{Operation} completed: Operations={OperationCount}", "DatabaseOperationsTool", "Help", operations.Length);
		DatabaseOperationResponse databaseOperationResponse = new DatabaseOperationResponse();
		databaseOperationResponse.Success = true;
		databaseOperationResponse.Message = "Tool description retrieved successfully";
		databaseOperationResponse.Operation = request.Operation;
		databaseOperationResponse.Help = new
		{
			ToolName = "database_operations",
			Description = "Perform operations on Analysis Services databases",
			SupportedOperations = operations,
			Examples = toolMetadata.Operations.Where<KeyValuePair<string, OperationMetadata>>((KeyValuePair<string, OperationMetadata> p) => operations.Contains<string>(p.Key, StringComparer.OrdinalIgnoreCase)),
			Notes = new string[6] { "For Update operation, supply the database name via UpdateDefinition or a connection already bound to a database.", "For DeserializeFromFolder operation, the TMDL folder path must be provided.", "For SerializeToFolder operation, the TMDL folder path must be provided.", "For Deploy operation, the target connection name must be provided.", "For CreateOffline operation, the CreateDefinition must be provided.", "For ExportTMDL operation, the ConnectionName must be provided." }
		};
		return databaseOperationResponse;
	}

	private (bool isValid, string? errorMessage) ValidateRequest(string operation, DatabaseOperationRequest request)
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
			_logger.LogWarning("Missing required parameters for {Operation}: {Params}", operation, string.Join(", ", list));
			return (isValid: false, errorMessage: "Missing required parameters for " + operation + " operation: " + string.Join(", ", list));
		}
		if (list2.Any())
		{
			_logger.LogWarning("Forbidden parameters for {Operation}: {Params}", operation, string.Join(", ", list2));
			return (isValid: false, errorMessage: "Forbidden parameters for " + operation + " operation: " + string.Join(", ", list2));
		}
		return (isValid: true, errorMessage: null);
	}
}
