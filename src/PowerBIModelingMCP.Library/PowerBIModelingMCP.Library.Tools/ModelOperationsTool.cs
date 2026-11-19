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
public class ModelOperationsTool
{
	public const string ToolName = "model_operations";

	private readonly ILogger<ModelOperationsTool> _logger;

	private readonly IEnhancedRefreshService? _enhancedRefreshService;

	private readonly MCPServerConfiguration _config;

	public static readonly ToolMetadata toolMetadata = new ToolMetadata
	{
		Operations = new Dictionary<string, OperationMetadata>(StringComparer.OrdinalIgnoreCase)
		{
			["Help"] = new OperationMetadata
			{
				Description = "Describe the tool and its operations.\nMandatory properties: None.\nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Help\"\n    }\n}" }
			},
			["Get"] = new OperationMetadata
			{
				Description = "Get the model definition and properties including metadata, configuration settings, and structural information.\nMandatory properties: None.\nOptional: ModelName.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Get\",\n        \"ConnectionName\": \"MyConnection\"\n    }\n}" }
			},
			["Create"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Definition" },
				Description = "Create a new offline model within a new database. Only offline database creation is currently supported.\nMandatory properties: Definition (with Name).\nOptional: Description, Collation, ModelName, IsOffline (defaults to true), Annotations, ExtendedProperties, BindingInfos.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"ConnectionName\": \"NewModel\",\n        \"Definition\": {\n            \"Name\": \"SalesModel\",\n            \"Description\": \"Sales analysis model\",\n            \"IsOffline\": true\n        }\n    }\n}" }
			},
			["Update"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Definition" },
				Description = "Update the model properties. Model names cannot be changed and must use the Rename operation instead.\nMandatory properties: Definition.\nOptional: Name (defaults to current model), Description, StorageLocation, DefaultMode, DefaultDataView, Culture, Collation, DataAccessOptions, DefaultMeasureTable, DefaultMeasureName, DefaultPowerBIDataSourceVersion, ForceUniqueNames, DiscourageImplicitMeasures, DiscourageReportMeasures, DataSourceVariablesOverrideBehavior, DataSourceDefaultMaxConnections, SourceQueryCulture, MAttributes, DiscourageCompositeModels, AutomaticAggregationOptions, DirectLakeBehavior, ValueFilterBehavior, SelectionExpressionBehavior, MetadataAccessPolicy, Annotations, ExtendedProperties, BindingInfos.\nNote: When ExtendedProperties, Annotations, or BindingInfos are provided, existing collections will be completely replaced (replace-all semantics).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Update\",\n        \"ConnectionName\": \"MyConnection\",\n        \"Definition\": { \n            \"Description\": \"Updated model to direct query\",\n            \"DefaultMode\": \"DirectQuery\" \n        }\n    }\n}" }
			},
			["RefreshWithXMLA"] = new OperationMetadata
			{
				Description = "Refresh the model to reload data from data sources, synchronously using XMLA/TOM.\nBlocks until the refresh completes.\nBest for quick recalculations (Calculate) after measure changes.\nFor long-running data refreshes (Full, DataOnly), use RefreshWithAPI instead.\nMandatory properties: None.\nOptional: RefreshType (Automatic, Full, ClearValues, Calculate, DataOnly, Defragment).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"RefreshWithXMLA\",\n        \"ConnectionName\": \"MyConnection\",\n        \"RefreshType\": \"Calculate\"\n    }\n}" }
			},
			["RefreshWithAPI"] = new OperationMetadata
			{
				Description = "Start an asynchronous data refresh via the Power BI Enhanced Refresh REST API.\nReturns immediately with a request ID. Use CheckStatusOfRefreshWithAPI to monitor progress.\nBest for long-running data refreshes (Full, DataOnly) that may take minutes to hours.\nMandatory properties: None.\nOptional: RefreshType (Automatic, Full, DataOnly, Calculate), TableName.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"RefreshWithAPI\",\n        \"RefreshType\": \"Full\"\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"RefreshWithAPI\",\n        \"RefreshType\": \"Full\",\n        \"TableName\": \"Customer\"\n    }\n}" }
			},
			["CheckStatusOfRefreshWithAPI"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "RequestId" },
				Description = "Check the status of an async refresh started with RefreshWithAPI.\nMandatory properties: RequestId (from RefreshWithAPI response).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"CheckStatusOfRefreshWithAPI\",\n        \"RequestId\": \"abc-123-def\"\n    }\n}" }
			},
			["CancelRefreshWithAPI"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "RequestId" },
				Description = "Cancel an in-progress async refresh started with RefreshWithAPI.\nOnly one refresh can run per dataset at a time — cancel is needed to start a new one.\nMandatory properties: RequestId (from RefreshWithAPI response).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"CancelRefreshWithAPI\",\n        \"RequestId\": \"abc-123-def\"\n    }\n}" }
			},
			["GetStats"] = new OperationMetadata
			{
				Description = "Get model statistics including object counts and memory usage information.\nMandatory properties: None.\nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"GetStats\",\n        \"ConnectionName\": \"MyConnection\"\n    }\n}" }
			},
			["Rename"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "NewName" },
				Description = "Rename the model to a new name.\nMandatory properties: NewName.\nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Rename\",\n        \"NewName\": \"NewModel\"\n    }\n}" }
			},
			["ExportTMDL"] = new OperationMetadata
			{
				Description = "Export model to TMDL format.\nMandatory properties: None.\nOptional: TmdlExportOptions (TmdlSerializationOptions.IncludeChildren defaults to false).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ExportTMDL\",\n        \"TmdlExportOptions\": {\n            \"TmdlSerializationOptions\": {\n                \"IncludeChildren\": true\n            }\n        },\n        \"ConnectionName\": \"MyConnection\"\n    }\n}" }
			}
		}
	};

	public ModelOperationsTool(ILogger<ModelOperationsTool> logger, MCPServerConfiguration config, IEnhancedRefreshService? enhancedRefreshService = null)
	{
		_logger = logger;
		_config = config;
		_enhancedRefreshService = enhancedRefreshService;
	}

	[McpServerTool(Name = "model_operations", Title = "Model Operations", ReadOnly = false, Destructive = true)]
	[YamlToolDescription("model_operations")]
	public async Task<CallToolResult> ExecuteModelOperation(McpServer mcpServer, ModelOperationRequest request, IWriteGuard writeGuard)
	{
		_logger.LogDebug("Executing {ToolName}.{Operation}: ModelName={ModelName}, Connection={ConnectionName}", "ModelOperationsTool", request.Operation, request.ModelName ?? "(current)", request.ConnectionName ?? "(last used)");
		string[] validOperations = new string[11]
		{
			"HELP", "GET", "CREATE", "UPDATE", "REFRESHWITHXMLA", "REFRESHWITHAPI", "CHECKSTATUSOFREFRESHWITHAPI", "CANCELREFRESHWITHAPI", "GETSTATS", "RENAME",
			"EXPORTTMDL"
		};
		string[] writeOperations = new string[5] { "CREATE", "UPDATE", "REFRESHWITHXMLA", "REFRESHWITHAPI", "RENAME" };
		string op = request.Operation.ToUpperInvariant();
		ToolCallAnnotations annotations = ToolCallAnnotations.Create("model_operations", request.Operation, !Enumerable.Contains(writeOperations, op));
		CallToolResult result = null;
		try
		{
			if (!Enumerable.Contains(validOperations, op))
			{
				_logger.LogWarning("Invalid operation '{Operation}' requested for {ToolName}. Valid operations: {ValidOperations}", request.Operation, "ModelOperationsTool", string.Join(", ", validOperations));
				CallToolResult result2;
				result = (result2 = CallToolResultHelper.FromResponse(ModelOperationResponse.Forbidden(request.Operation, "Invalid operation: " + request.Operation + ". Supported operations: " + string.Join(", ", validOperations), request.ModelName), annotations));
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
					_logger.LogWarning("{ToolName}.{Operation} blocked by write guard: {Reason}", "ModelOperationsTool", request.Operation, writeOperationResult.Message);
					CallToolResult result2;
					result = (result2 = CallToolResultHelper.FromResponse(ModelOperationResponse.Forbidden(request.Operation, writeOperationResult.Message, request.ModelName), annotations));
					return result2;
				}
			}
			bool isWriteEnabled = writeGuard.IsWriteEnabled;
			CallToolResult result3;
			result = (result3 = op switch
			{
				"HELP" => CallToolResultHelper.FromResponse(HandleHelpOperation(request, isWriteEnabled ? validOperations : validOperations.Except(writeOperations).ToArray()), annotations), 
				"GET" => CallToolResultHelper.FromResponse(await HandleGetOperation(request), annotations), 
				"CREATE" => CallToolResultHelper.FromResponse(await HandleCreateOperation(request), annotations, null, minimalSuccessPayload: true), 
				"UPDATE" => CallToolResultHelper.FromResponse(await HandleUpdateOperation(request), annotations, null, minimalSuccessPayload: true), 
				"REFRESHWITHXMLA" => CallToolResultHelper.FromResponse(await HandleRefreshWithXMLAOperation(request), annotations, null, minimalSuccessPayload: true), 
				"REFRESHWITHAPI" => CallToolResultHelper.FromResponse(await HandleRefreshWithAPIOperation(request), annotations), 
				"CHECKSTATUSOFREFRESHWITHAPI" => CallToolResultHelper.FromResponse(await HandleCheckStatusOfRefreshWithAPIOperation(request), annotations), 
				"CANCELREFRESHWITHAPI" => CallToolResultHelper.FromResponse(await HandleCancelRefreshWithAPIOperation(request), annotations), 
				"GETSTATS" => CallToolResultHelper.FromResponse(await HandleGetStatsOperation(request), annotations), 
				"RENAME" => CallToolResultHelper.FromResponse(await HandleRenameOperation(request), annotations, null, minimalSuccessPayload: true), 
				"EXPORTTMDL" => CallToolResultHelper.FromExportResponse(await HandleExportTMDLOperation(request), "model.tmdl", "text/plain", annotations), 
				_ => CallToolResultHelper.FromResponse(ModelOperationResponse.Forbidden(request.Operation, "Operation " + request.Operation + " not implemented", request.ModelName), annotations), 
			});
			return result3;
		}
		catch (Exception ex)
		{
			_logger.LogOperationError("ModelOperationsTool", request.Operation, ex);
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			string message = op switch
			{
				"GET" => "Failed to get model: " + ex.GetErrorMessage(), 
				"CREATE" => "Failed to create model: " + ex.GetErrorMessage(), 
				"UPDATE" => "Failed to update model: " + ex.GetErrorMessage(), 
				"REFRESHWITHXMLA" => "Failed to refresh model: " + ex.GetErrorMessage(), 
				"REFRESHWITHAPI" => "Failed to start refresh: " + ex.GetErrorMessage(), 
				"CHECKSTATUSOFREFRESHWITHAPI" => "Failed to check refresh status: " + ex.GetErrorMessage(), 
				"CANCELREFRESHWITHAPI" => "Failed to cancel refresh: " + ex.GetErrorMessage(), 
				"GETSTATS" => "Failed to get model statistics: " + ex.GetErrorMessage(), 
				"RENAME" => "Failed to rename model: " + ex.GetErrorMessage(), 
				"EXPORTTMDL" => "Failed to export TMDL for model: " + ex.GetErrorMessage(), 
				_ => "Error executing model operation: " + ex.GetErrorMessage(), 
			};
			CallToolResult result2;
			result = (result2 = CallToolResultHelper.FromResponse(new ModelOperationResponse
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

	private async Task<ModelOperationResponse> HandleGetOperation(ModelOperationRequest request)
	{
		var (flag, message) = ValidateRequest(request.Operation, request);
		if (!flag)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage(message, ErrorSource.User);
		}
		ModelGet modelGet = await ModelOperations.GetModel(request.ConnectionName);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "ModelOperationsTool", request.Operation, request.ConnectionName);
		return new ModelOperationResponse
		{
			Success = true,
			Message = "Retrieved model '" + modelGet.Name + "' successfully",
			Operation = "GET",
			ModelName = modelGet.Name,
			Data = modelGet
		};
	}

	private async Task<ModelOperationResponse> HandleCreateOperation(ModelOperationRequest request)
	{
		var (flag, text) = ValidateRequest(request.Operation, request);
		if (!flag)
		{
			_logger.LogWarning("Invalid request for {Operation} operation: {ValidationError}", request.Operation, text);
			return new ModelOperationResponse
			{
				Success = false,
				Message = text,
				Operation = request.Operation,
				Help = toolMetadata.Operations.GetValueOrDefault(request.Operation)
			};
		}
		if (request.Definition == null)
		{
			_logger.LogWarning("Definition is required for Create operation");
			return new ModelOperationResponse
			{
				Success = false,
				Message = "Definition is required for Create operation.",
				Operation = request.Operation,
				Help = toolMetadata.Operations.GetValueOrDefault("Create")
			};
		}
		if (request.Definition.IsOffline == false)
		{
			_logger.LogWarning("Only offline database creation is currently supported");
			return new ModelOperationResponse
			{
				Success = false,
				Message = "Only offline database creation is currently supported. Please set IsOffline to true or omit it (defaults to true).",
				Operation = request.Operation,
				Help = toolMetadata.Operations.GetValueOrDefault("Create")
			};
		}
		List<string> warnings = new List<string>();
		if (!request.Definition.IsOffline.HasValue)
		{
			warnings.Add("Creating a new offline database to hold the new model (this is the only supported create operation).");
		}
		DatabaseCreateResult databaseCreateResult = await DatabaseOperations.CreateOfflineDb(new DatabaseCreate
		{
			Name = request.Definition.Name,
			Description = request.Definition.Description,
			Collation = request.Definition.Collation,
			Annotations = request.Definition.Annotations,
			ModelName = request.Definition.ModelName,
			IsOffline = request.Definition.IsOffline
		}, request.ConnectionName, _config.ProToolingValue);
		if (warnings.Count > 0)
		{
			foreach (string item in warnings)
			{
				_logger.LogOperationWarning("ModelOperationsTool", request.Operation, item);
			}
		}
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "ModelOperationsTool", request.Operation, request.ConnectionName);
		return new ModelOperationResponse
		{
			Success = true,
			Message = $"Created model '{databaseCreateResult.ModelName}' in database '{databaseCreateResult.DatabaseName}' successfully",
			Operation = "CREATE",
			ModelName = databaseCreateResult.ModelName,
			Warnings = ((warnings.Count > 0) ? warnings : null),
			Data = new
			{
				ConnectionName = databaseCreateResult.ConnectionName,
				DatabaseName = databaseCreateResult.DatabaseName,
				ModelName = databaseCreateResult.ModelName,
				CreatedAt = databaseCreateResult.CreatedAt,
				IsOffline = true
			}
		};
	}

	private async Task<ModelOperationResponse> HandleUpdateOperation(ModelOperationRequest request)
	{
		if (request.Definition == null)
		{
			_logger.LogWarning("Definition is required for Update operation");
			return new ModelOperationResponse
			{
				Success = false,
				Message = "Definition is required for Update operation.",
				Operation = request.Operation,
				Help = toolMetadata.Operations.GetValueOrDefault("Update")
			};
		}
		if (string.IsNullOrEmpty(request.Definition.Name) && !string.IsNullOrEmpty(request.ModelName))
		{
			request.Definition.Name = request.ModelName;
		}
		else if (!string.IsNullOrEmpty(request.ModelName) && !string.IsNullOrEmpty(request.Definition.Name) && request.Definition.Name != request.ModelName)
		{
			_logger.LogWarning("Model name mismatch");
			return new ModelOperationResponse
			{
				Success = false,
				Message = $"Model name mismatch: Request specifies '{request.ModelName}' but Definition specifies '{request.Definition.Name}'",
				Operation = request.Operation,
				Help = toolMetadata.Operations.GetValueOrDefault("Update")
			};
		}
		await ModelOperations.UpdateModel(request.ConnectionName, request.Definition);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "ModelOperationsTool", request.Operation, request.ConnectionName);
		return new ModelOperationResponse
		{
			Success = true,
			Message = "Updated model successfully",
			Operation = "UPDATE",
			ModelName = (request.Definition.Name ?? request.ModelName)
		};
	}

	private async Task<ModelOperationResponse> HandleRefreshWithXMLAOperation(ModelOperationRequest request)
	{
		string refreshType = request.RefreshType ?? "Automatic";
		await ModelOperations.RefreshModel(request.ConnectionName, refreshType);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, RefreshType={RefreshType}", "ModelOperationsTool", request.Operation, request.ConnectionName, refreshType);
		return new ModelOperationResponse
		{
			Success = true,
			Message = "Refreshed model with refresh type '" + refreshType + "' successfully",
			Operation = "REFRESHWITHXMLA",
			ModelName = request.ModelName
		};
	}

	private async Task<ModelOperationResponse> HandleRefreshWithAPIOperation(ModelOperationRequest request)
	{
		ModelOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(request.ConnectionName))
		{
			if (!connectionInfo.IsCloudConnection)
			{
				result = new ModelOperationResponse
				{
					Success = false,
					Message = "RefreshWithAPI is only supported for Fabric cloud connections. Use RefreshWithXMLA instead.",
					Operation = "REFRESHWITHAPI"
				};
			}
			else
			{
				string text = connectionInfo.Database?.ID;
				if (string.IsNullOrEmpty(text))
				{
					result = new ModelOperationResponse
					{
						Success = false,
						Message = "No database ID available on the current connection.",
						Operation = "REFRESHWITHAPI"
					};
				}
				else
				{
					string workspaceId = connectionInfo.WorkspaceId;
					EnhancedRefreshResult enhancedRefreshResult = await (_enhancedRefreshService ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("RefreshWithAPI requires IEnhancedRefreshService to be registered.")).StartRefreshAsync(workspaceId, text, request.RefreshType ?? "Automatic", request.TableName);
					result = new ModelOperationResponse
					{
						Success = enhancedRefreshResult.Success,
						Message = enhancedRefreshResult.Message + ((enhancedRefreshResult.RequestId != null) ? (" Use CheckStatusOfRefreshWithAPI with RequestId '" + enhancedRefreshResult.RequestId + "' to monitor progress.") : string.Empty),
						Operation = "REFRESHWITHAPI",
						ModelName = request.ModelName,
						Data = new { enhancedRefreshResult.RequestId }
					};
				}
			}
		}
		return result;
	}

	private async Task<ModelOperationResponse> HandleCheckStatusOfRefreshWithAPIOperation(ModelOperationRequest request)
	{
		if (string.IsNullOrEmpty(request.RequestId))
		{
			return new ModelOperationResponse
			{
				Success = false,
				Message = "RequestId is required. Use the request ID returned by RefreshWithAPI.",
				Operation = "CHECKSTATUSOFREFRESHWITHAPI"
			};
		}
		ModelOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(request.ConnectionName))
		{
			if (!connectionInfo.IsCloudConnection)
			{
				result = new ModelOperationResponse
				{
					Success = false,
					Message = "CheckStatusOfRefreshWithAPI is only supported for Fabric cloud connections.",
					Operation = "CHECKSTATUSOFREFRESHWITHAPI"
				};
			}
			else
			{
				string text = connectionInfo.Database?.ID;
				if (string.IsNullOrEmpty(text))
				{
					result = new ModelOperationResponse
					{
						Success = false,
						Message = "No database ID available on the current connection.",
						Operation = "CHECKSTATUSOFREFRESHWITHAPI"
					};
				}
				else
				{
					string workspaceId = connectionInfo.WorkspaceId;
					EnhancedRefreshStatusResult enhancedRefreshStatusResult = await (_enhancedRefreshService ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("RefreshWithAPI requires IEnhancedRefreshService to be registered.")).GetRefreshStatusAsync(workspaceId, text, request.RequestId);
					result = new ModelOperationResponse
					{
						Success = (enhancedRefreshStatusResult.Status != "Failed" && enhancedRefreshStatusResult.Status != "Error"),
						Message = enhancedRefreshStatusResult.Message,
						Operation = "CHECKSTATUSOFREFRESHWITHAPI",
						ModelName = request.ModelName,
						Data = new { enhancedRefreshStatusResult.Status, enhancedRefreshStatusResult.RequestId, enhancedRefreshStatusResult.StartTime, enhancedRefreshStatusResult.EndTime }
					};
				}
			}
		}
		return result;
	}

	private async Task<ModelOperationResponse> HandleCancelRefreshWithAPIOperation(ModelOperationRequest request)
	{
		if (string.IsNullOrEmpty(request.RequestId))
		{
			return new ModelOperationResponse
			{
				Success = false,
				Message = "RequestId is required. Use the request ID returned by RefreshWithAPI.",
				Operation = "CANCELREFRESHWITHAPI"
			};
		}
		ModelOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(request.ConnectionName))
		{
			if (!connectionInfo.IsCloudConnection)
			{
				result = new ModelOperationResponse
				{
					Success = false,
					Message = "CancelRefreshWithAPI is only supported for Fabric cloud connections.",
					Operation = "CANCELREFRESHWITHAPI"
				};
			}
			else
			{
				string text = connectionInfo.Database?.ID;
				if (string.IsNullOrEmpty(text))
				{
					result = new ModelOperationResponse
					{
						Success = false,
						Message = "No database ID available.",
						Operation = "CANCELREFRESHWITHAPI"
					};
				}
				else
				{
					string workspaceId = connectionInfo.WorkspaceId;
					await (_enhancedRefreshService ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("CancelRefreshWithAPI requires IEnhancedRefreshService to be registered.")).CancelRefreshAsync(workspaceId, text, request.RequestId);
					result = new ModelOperationResponse
					{
						Success = true,
						Message = "Refresh " + request.RequestId + " cancelled successfully.",
						Operation = "CANCELREFRESHWITHAPI"
					};
				}
			}
		}
		return result;
	}

	private async Task<ModelOperationResponse> HandleGetStatsOperation(ModelOperationRequest request)
	{
		Dictionary<string, object> dictionary = await ModelOperations.GetModelStats(request.ConnectionName);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "ModelOperationsTool", request.Operation, request.ConnectionName);
		return new ModelOperationResponse
		{
			Success = true,
			Message = "Retrieved model statistics successfully",
			Operation = "GETSTATS",
			ModelName = ((!dictionary.ContainsKey("ModelName")) ? null : dictionary["ModelName"]?.ToString()),
			Data = dictionary
		};
	}

	private async Task<ModelOperationResponse> HandleRenameOperation(ModelOperationRequest request)
	{
		await ModelOperations.RenameModel(request.ConnectionName, request.NewName);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "ModelOperationsTool", request.Operation, request.ConnectionName);
		return new ModelOperationResponse
		{
			Success = true,
			Message = "Renamed model to '" + request.NewName + "' successfully",
			Operation = "RENAME",
			ModelName = request.NewName
		};
	}

	private async Task<ModelOperationResponse> HandleExportTMDLOperation(ModelOperationRequest request)
	{
		string data = await ModelOperations.ExportTMDL(request.ConnectionName, request.TmdlExportOptions ?? new ExportTmdl());
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "ModelOperationsTool", request.Operation, request.ConnectionName);
		return new ModelOperationResponse
		{
			Success = true,
			Message = "TMDL exported for model",
			Operation = request.Operation,
			ModelName = request.ModelName,
			Data = data
		};
	}

	private ModelOperationResponse HandleHelpOperation(ModelOperationRequest request, string[] operations)
	{
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Operations={OperationCount}", "ModelOperationsTool", request.Operation, request.ConnectionName, operations.Length);
		ModelOperationResponse modelOperationResponse = new ModelOperationResponse();
		modelOperationResponse.Success = true;
		modelOperationResponse.Message = "Tool description retrieved successfully";
		modelOperationResponse.Operation = request.Operation;
		modelOperationResponse.Help = new
		{
			ToolName = "model_operations",
			Description = "Perform operations on semantic model.",
			SupportedOperations = operations,
			Examples = toolMetadata.Operations.Where<KeyValuePair<string, OperationMetadata>>((KeyValuePair<string, OperationMetadata> p) => operations.Contains<string>(p.Key, StringComparer.OrdinalIgnoreCase)),
			Notes = new string[3] { "Check the operation description for details on each parameter.", "Use the Help operation to get this information.", "Use the Get operation to retrieve the current model or a specific model by name." }
		};
		return modelOperationResponse;
	}

	private (bool isValid, string? errorMessage) ValidateRequest(string operation, ModelOperationRequest request)
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
			string text = "Missing required parameters for " + operation + " operation: " + string.Join(", ", list);
			_logger.LogWarning(text);
			return (isValid: false, errorMessage: text);
		}
		if (list2.Any())
		{
			string text2 = "Forbidden parameters provided for " + operation + " operation: " + string.Join(", ", list2);
			_logger.LogWarning(text2);
			return (isValid: false, errorMessage: text2);
		}
		return (isValid: true, errorMessage: null);
	}
}
