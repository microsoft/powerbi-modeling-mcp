using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;
using PowerBIModelingMCP.Library.Common.Telemetry;
using PowerBIModelingMCP.Library.Core;

namespace PowerBIModelingMCP.Library.Tools;

[McpServerToolType]
public class ConnectionOperationsTool
{
	public const string ToolName = "connection_operations";

	private readonly ILogger<ConnectionOperationsTool> _logger;

	public static readonly ToolMetadata<ConnectionOperation> toolMetadata = ConnectionOperationMetadata.CreateToolMeatadata();

	public ConnectionOperationsTool(ILogger<ConnectionOperationsTool> logger)
	{
		_logger = logger;
	}

	[McpServerTool(Name = "connection_operations", Title = "Connection Operations", ReadOnly = true)]
	[YamlToolDescription("connection_operations")]
	public CallToolResult ExecuteConnectionOperation(McpServer mcpServer, ConnectionOperationRequest request)
	{
		_logger.LogDebug("Executing {ToolName}.{Operation}: ConnectionName={ConnectionName}", "ConnectionOperationsTool", request.Operation, request.ConnectionName ?? "(none)");
		ToolCallAnnotations toolCallAnnotations = ToolCallAnnotations.Create("connection_operations", request.Operation, readOnly: true);
		CallToolResult callToolResult = null;
		try
		{
			if (!Enum.TryParse<ConnectionOperation>(request.Operation, ignoreCase: true, out var result))
			{
				string[] names = Enum.GetNames<ConnectionOperation>();
				_logger.LogWarning("Invalid operation '{Operation}' requested for {ToolName}. Valid operations: {ValidOperations}", request.Operation, "ConnectionOperationsTool", string.Join(", ", names));
				return callToolResult = CallToolResultHelper.FromResponse(new ConnectionOperationResponse
				{
					Success = false,
					Message = "Invalid operation: " + request.Operation + ". Supported operations: " + string.Join(", ", names),
					Operation = request.Operation,
					ErrorSource = ErrorSource.User
				}, toolCallAnnotations);
			}
			var (flag, text) = ValidateRequest(result, request);
			if (!flag)
			{
				_logger.LogWarning("Invalid request for {Operation} operation: {ValidationError}", request.Operation, text);
				return callToolResult = CallToolResultHelper.Error(request.Operation, text, toolCallAnnotations, ErrorSource.User);
			}
			return callToolResult = result switch
			{
				ConnectionOperation.Connect => CallToolResultHelper.FromResponse(HandleConnectOperation(request), toolCallAnnotations), 
				ConnectionOperation.ConnectFabric => CallToolResultHelper.FromResponse(HandleConnectFabricOperation(request), toolCallAnnotations), 
				ConnectionOperation.ConnectFolder => CallToolResultHelper.FromResponse(HandleConnectFolderOperation(request), toolCallAnnotations), 
				ConnectionOperation.ConnectBimFile => CallToolResultHelper.FromResponse(HandleConnectBimFileOperation(request), toolCallAnnotations), 
				ConnectionOperation.Disconnect => CallToolResultHelper.FromResponse(HandleDisconnectOperation(request), toolCallAnnotations, null, minimalSuccessPayload: true), 
				ConnectionOperation.GetConnection => CallToolResultHelper.FromResponse(HandleGetConnectionOperation(request), toolCallAnnotations), 
				ConnectionOperation.ListConnections => CallToolResultHelper.FromResponse(HandleListConnectionsOperation(request), toolCallAnnotations), 
				ConnectionOperation.ListLocalInstances => CallToolResultHelper.FromResponse(HandleListLocalInstancesOperation(request), toolCallAnnotations), 
				ConnectionOperation.Help => CallToolResultHelper.FromResponse(HandleHelpOperation(request, Enum.GetNames<ConnectionOperation>()), toolCallAnnotations), 
				_ => CallToolResultHelper.FromResponse(new ConnectionOperationResponse
				{
					Success = false,
					Message = $"Operation {result} is not implemented",
					Operation = request.Operation
				}, toolCallAnnotations), 
			};
		}
		catch (Exception ex)
		{
			_logger.LogOperationError("ConnectionOperationsTool", request.Operation, ex);
			OperationMetadata value = null;
			if (Enum.TryParse<ConnectionOperation>(request.Operation, ignoreCase: true, out var result2))
			{
				toolMetadata.Operations.TryGetValue(result2, out value);
			}
			string message = result2 switch
			{
				ConnectionOperation.Connect => "Error connecting: " + ex.GetErrorMessage(), 
				ConnectionOperation.ConnectFabric => "Error connecting to Fabric: " + ex.GetErrorMessage(), 
				ConnectionOperation.ConnectFolder => "Error connecting to folder: " + ex.GetErrorMessage(), 
				ConnectionOperation.ConnectBimFile => "Error connecting to BIM file: " + ex.GetErrorMessage(), 
				ConnectionOperation.Disconnect => "Error disconnecting: " + ex.GetErrorMessage(), 
				ConnectionOperation.GetConnection => ex.GetErrorMessage(), 
				ConnectionOperation.ListConnections => "Error listing connections: " + ex.GetErrorMessage(), 
				ConnectionOperation.ListLocalInstances => "Error listing local instances: " + ex.GetErrorMessage(), 
				_ => "Error executing connection operation: " + ex.GetErrorMessage(), 
			};
			return callToolResult = CallToolResultHelper.FromResponse(new ConnectionOperationResponse
			{
				Success = false,
				Message = message,
				Operation = request.Operation,
				Help = value
			}, toolCallAnnotations, ex);
		}
		finally
		{
			_logger.LogToolCallCompleted(toolCallAnnotations.Title, !toolCallAnnotations.ReadOnlyHint, callToolResult?.IsError ?? true, ConnectionOperations.ResolveSemanticModelId());
		}
	}

	private ConnectionOperationResponse HandleConnectOperation(ConnectionOperationRequest request)
	{
		string text = request.ConnectionString;
		if (string.IsNullOrWhiteSpace(text))
		{
			if (string.IsNullOrWhiteSpace(request.DataSource))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Either ConnectionString or DataSource must be supplied.", ErrorSource.User);
			}
			string text2 = request.DataSource.Trim();
			if (text2.Contains("Desktop", StringComparison.OrdinalIgnoreCase) || (!text2.Contains(":") && !text2.StartsWith("powerbi://", StringComparison.OrdinalIgnoreCase)))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("A valid connection string cannot be generated automatically. Use ListLocalInstances to discover the exact Data Source and Initial Catalog.", ErrorSource.User);
			}
			text = ConnectionOperations.BuildConnectionString(text2, request.InitialCatalog);
		}
		string result = ConnectionOperations.Connect(text, request.ClearCredential).GetAwaiter().GetResult();
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "ConnectionOperationsTool", "Connect", result);
		return new ConnectionOperationResponse
		{
			Success = true,
			Message = "Connection '" + result + "' established successfully",
			Operation = request.Operation,
			Data = result
		};
	}

	private ConnectionOperationResponse HandleConnectFabricOperation(ConnectionOperationRequest request)
	{
		string workspaceName = request.WorkspaceName;
		string tenantName = request.TenantName ?? "myorg";
		string text = ConnectionOperations.BuildPowerBiXmlaEndpoint(workspaceName, tenantName);
		string result = ConnectionOperations.Connect(ConnectionOperations.BuildConnectionString(text, request.SemanticModelName), request.ClearCredential).GetAwaiter().GetResult();
		var data = new
		{
			ConnectionName = result,
			WorkspaceName = workspaceName,
			SemanticModelName = request.SemanticModelName,
			TenantName = tenantName,
			XmlaEndpoint = text
		};
		string message = ((!string.IsNullOrWhiteSpace(request.SemanticModelName)) ? $"Connected to Fabric workspace '{workspaceName}' semantic model '{request.SemanticModelName}' as '{result}'" : $"Connected to Fabric workspace '{workspaceName}' as '{result}'");
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "ConnectionOperationsTool", "ConnectFabric", result);
		return new ConnectionOperationResponse
		{
			Success = true,
			Message = message,
			Operation = request.Operation,
			Data = data
		};
	}

	private ConnectionOperationResponse HandleConnectFolderOperation(ConnectionOperationRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.FolderPath))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("FolderPath is required for ConnectFolder operation", ErrorSource.User);
		}
		if (!string.IsNullOrWhiteSpace(request.ConnectionName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionName cannot be specified for ConnectFolder operation - it is auto-generated", ErrorSource.User);
		}
		TmdlDeserializeResult tmdlDeserializeResult = ConnectionOperations.ConnectFolder(request.FolderPath);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "ConnectionOperationsTool", "ConnectFolder", tmdlDeserializeResult.ConnectionName);
		return new ConnectionOperationResponse
		{
			Success = true,
			Message = tmdlDeserializeResult.Message,
			Operation = request.Operation,
			Data = new { tmdlDeserializeResult.ConnectionName, tmdlDeserializeResult.DatabaseName, tmdlDeserializeResult.FolderPath, tmdlDeserializeResult.TablesLoaded, tmdlDeserializeResult.MeasuresLoaded, tmdlDeserializeResult.RelationshipsLoaded, tmdlDeserializeResult.LoadedAt }
		};
	}

	private ConnectionOperationResponse HandleConnectBimFileOperation(ConnectionOperationRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.BimFilePath))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("BimFilePath is required for ConnectBimFile operation", ErrorSource.User);
		}
		if (!string.IsNullOrWhiteSpace(request.ConnectionName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionName cannot be specified for ConnectBimFile operation - it is auto-generated", ErrorSource.User);
		}
		BimDeserializeResult bimDeserializeResult = ConnectionOperations.ConnectBimFile(request.BimFilePath);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "ConnectionOperationsTool", "ConnectBimFile", bimDeserializeResult.ConnectionName);
		return new ConnectionOperationResponse
		{
			Success = true,
			Message = bimDeserializeResult.Message,
			Operation = request.Operation,
			Data = new { bimDeserializeResult.ConnectionName, bimDeserializeResult.DatabaseName, bimDeserializeResult.FilePath, bimDeserializeResult.TablesLoaded, bimDeserializeResult.MeasuresLoaded, bimDeserializeResult.RelationshipsLoaded, bimDeserializeResult.LoadedAt }
		};
	}

	private ConnectionOperationResponse HandleDisconnectOperation(ConnectionOperationRequest request)
	{
		ConnectionOperations.Disconnect(request.ConnectionName);
		string message = ((request.ConnectionName != null) ? ("Disconnected from connection '" + request.ConnectionName + "'") : "Disconnected all connections");
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "ConnectionOperationsTool", "Disconnect", request.ConnectionName);
		return new ConnectionOperationResponse
		{
			Success = true,
			Message = message,
			Operation = request.Operation
		};
	}

	private ConnectionOperationResponse HandleListLocalInstancesOperation(ConnectionOperationRequest request)
	{
		IReadOnlyList<LocalAnalysisServicesInstance> readOnlyList = ConnectionOperations.ListLocalAnalysisServicesInstances();
		_logger.LogInformation("{ToolName}.{Operation} completed: Count={Count}", "ConnectionOperationsTool", "ListLocalInstances", readOnlyList.Count);
		return new ConnectionOperationResponse
		{
			Success = true,
			Message = $"Found {readOnlyList.Count} local PowerBI Desktop and Analysis Services instances",
			Operation = request.Operation,
			Data = readOnlyList
		};
	}

	private ConnectionOperationResponse HandleListConnectionsOperation(ConnectionOperationRequest request)
	{
		IReadOnlyList<ConnectionGet> readOnlyList = ConnectionOperations.ListConnections();
		_logger.LogInformation("{ToolName}.{Operation} completed: Count={Count}", "ConnectionOperationsTool", "ListConnections", readOnlyList.Count);
		return new ConnectionOperationResponse
		{
			Success = true,
			Message = $"Found {readOnlyList.Count} connections",
			Operation = request.Operation,
			Data = readOnlyList
		};
	}

	private ConnectionOperationResponse HandleGetConnectionOperation(ConnectionOperationRequest request)
	{
		ConnectionGet connection = ConnectionOperations.GetConnection(request.ConnectionName);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "ConnectionOperationsTool", "GetConnection", request.ConnectionName);
		return new ConnectionOperationResponse
		{
			Success = true,
			Message = "Connection '" + request.ConnectionName + "' details retrieved successfully",
			Operation = request.Operation,
			Data = connection
		};
	}

	private ConnectionOperationResponse HandleHelpOperation(ConnectionOperationRequest request, string[] operations)
	{
		_logger.LogInformation("{ToolName}.{Operation} completed: Operations={OperationCount}", "ConnectionOperationsTool", "Help", operations.Length);
		ConnectionOperationResponse connectionOperationResponse = new ConnectionOperationResponse();
		connectionOperationResponse.Success = true;
		connectionOperationResponse.Message = "Tool description retrieved successfully";
		connectionOperationResponse.Operation = request.Operation;
		connectionOperationResponse.Help = new
		{
			ToolName = "connection_operations",
			Description = "Perform operations on Microsoft tabular semantic model data source connections (PowerBI Desktop, Analysis Services, Fabric).",
			SupportedOperations = operations,
			Examples = from p in toolMetadata.Operations.ToDictionary<KeyValuePair<ConnectionOperation, OperationMetadata>, string, OperationMetadata>((KeyValuePair<ConnectionOperation, OperationMetadata> p) => p.Key.ToString(), (KeyValuePair<ConnectionOperation, OperationMetadata> p) => p.Value)
				where operations.Contains<string>(p.Key, StringComparer.OrdinalIgnoreCase)
				select p,
			Notes = new string[2] { "Connection names are case-insensitive and must be unique.", "Connection strings are case-insensitive and must be unique." }
		};
		return connectionOperationResponse;
	}

	private (bool isValid, string? errorMessage) ValidateRequest(ConnectionOperation operation, ConnectionOperationRequest request)
	{
		if (!toolMetadata.Operations.TryGetValue(operation, out OperationMetadata value))
		{
			_logger.LogWarning("No metadata found for operation {Operation}, skipping validation", operation);
			return (isValid: true, errorMessage: null);
		}
		JsonObject requestDict = JsonSerializer.SerializeToNode(request) as JsonObject;
		List<string> list = value.RequiredParams.Where((string p) => requestDict != null && requestDict[p] == null).ToList();
		List<string> list2 = value.ForbiddenParams.Where((string p) => requestDict != null && requestDict[p] != null).ToList();
		if (list.Any())
		{
			string item = $"Missing required parameters needed for {operation} operation: {string.Join(", ", list)}";
			return (isValid: false, errorMessage: item);
		}
		if (list2.Any())
		{
			string item2 = $"Forbidden parameters not allowed for {operation} operation: {string.Join(", ", list2)}";
			return (isValid: false, errorMessage: item2);
		}
		return (isValid: true, errorMessage: null);
	}
}
