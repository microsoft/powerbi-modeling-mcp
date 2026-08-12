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
public class PartitionOperationsTool
{
	public const string ToolName = "partition_operations";

	private readonly ILogger<PartitionOperationsTool> _logger;

	private readonly IWriteGuard _writeGuard;

	private readonly IEnhancedRefreshService? _enhancedRefreshService;

	public static readonly ToolMetadata toolMetadata = new ToolMetadata
	{
		Operations = new Dictionary<string, OperationMetadata>(StringComparer.OrdinalIgnoreCase)
		{
			["List"] = new OperationMetadata
			{
				Description = "List all partitions in the model, optionally filtered by table.\nReturns partitions grouped by table for easier consumption.\nMandatory properties: None.\nOptional: TableName (filter by specific table), MaxResults (default: 200).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"List\"\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"List\",\n        \"TableName\": \"Sales\",\n        \"MaxResults\": 100\n    }\n}" }
			},
			["Get"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Get details of one or more partitions.\nMandatory properties: References (list of partition identifiers).\nEach Reference requires: TableName, Name.\nOptional: Options (batch execution options).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Get\",\n        \"References\": [\n            { \"TableName\": \"Sales\", \"Name\": \"Partition1\" },\n            { \"TableName\": \"Sales\", \"Name\": \"Partition2\" }\n        ]\n    }\n}" }
			},
			["Create"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Definitions" },
				Description = "Create one or more partitions.\nMandatory properties: Definitions (list of partition definitions).\nEach Definition requires: TableName, Name, SourceType, and source-specific properties.\n\nSource-specific mandatory properties:\n- Calculated: Expression\n- M: Expression  \n- Entity: EntityName, ExpressionSourceName or DataSourceName\n- PolicyRange: StartDateTime, EndDateTime\n- Query: Query, DataSourceName\n\nOptional: Mode, Description, QueryGroupName, Annotations, ExtendedProperties.\nBatch Options: ContinueOnError, UseTransaction.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Create\",\n        \"Definitions\": [\n            { \n                \"TableName\": \"Sales\", \n                \"Name\": \"Partition2024\",\n                \"SourceType\": \"M\",\n                \"Expression\": \"let Source = ... in Source\"\n            },\n            { \n                \"TableName\": \"Sales\", \n                \"Name\": \"Partition2025\",\n                \"SourceType\": \"M\",\n                \"Expression\": \"let Source = ... in Source\"\n            }\n        ],\n        \"Options\": { \"ContinueOnError\": true, \"UseTransaction\": false }\n    }\n}" }
			},
			["Update"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "Definitions" },
				Description = "Update one or more existing partitions. Names cannot be changed - use Rename operation instead.\nMandatory properties: Definitions (list of partition definitions).\nEach Definition requires: TableName. Name is optional when the table contains only one partition (otherwise it is required).\n\nOptional properties per Definition:\n- Description, Mode, SourceType, QueryGroupName, Annotations, ExtendedProperties\n- Source-specific: Expression, Query, DataSourceName, etc.\n\nBatch Options: ContinueOnError, UseTransaction.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Update\",\n        \"Definitions\": [\n            { \n                \"TableName\": \"Sales\", \n                \"Name\": \"Partition2024\",\n                \"Description\": \"Updated description for 2024 partition\"\n            },\n            { \n                \"TableName\": \"Sales\", \n                \"Name\": \"Partition2025\",\n                \"Description\": \"Updated description for 2025 partition\"\n            }\n        ],\n        \"Options\": { \"ContinueOnError\": false, \"UseTransaction\": true }\n    }\n}" }
			},
			["Delete"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Delete one or more partitions.\nMandatory properties: References (list of partition identifiers).\nEach Reference requires: TableName, Name.\nNote: Cannot delete the last partition in a table.\nBatch Options: ContinueOnError, UseTransaction.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Delete\",\n        \"References\": [\n            { \"TableName\": \"Sales\", \"Name\": \"OldPartition1\" },\n            { \"TableName\": \"Sales\", \"Name\": \"OldPartition2\" }\n        ],\n        \"Options\": { \"ContinueOnError\": true }\n    }\n}" }
			},
			["RefreshWithXMLA"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "RefreshDefinitions" },
				Description = "Refresh one or more partitions synchronously using XMLA/TOM.\nBlocks until the refresh completes. Best for quick recalculations.\nFor long-running data refreshes, use RefreshWithAPI instead.\nMandatory properties: RefreshDefinitions (list of partition refresh definitions).\nEach RefreshDefinition requires: TableName, PartitionName.\nEach RefreshDefinition optional: RefreshType (defaults to Automatic).\nValid RefreshType values: Automatic, Full, ClearValues, Calculate, DataOnly, Defragment.\nBatch Options: ContinueOnError, UseTransaction.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"RefreshWithXMLA\",\n        \"RefreshDefinitions\": [\n            { \"TableName\": \"Sales\", \"PartitionName\": \"Partition2024\", \"RefreshType\": \"Full\" }\n        ]\n    }\n}" }
			},
			["RefreshWithAPI"] = new OperationMetadata
			{
				Description = "Start an asynchronous partition refresh via the Power BI Enhanced Refresh REST API.\nReturns immediately with a request ID. Use CheckStatusOfRefreshWithAPI to monitor progress.\nMandatory properties: TableName, PartitionName.\nOptional: RefreshType (Automatic, Full, DataOnly, Calculate).",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"RefreshWithAPI\",\n        \"TableName\": \"Sales\",\n        \"PartitionName\": \"Partition2024\",\n        \"RefreshType\": \"Full\"\n    }\n}" }
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
			["Rename"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "RenameDefinitions" },
				Description = "Rename one or more partitions.\nMandatory properties: RenameDefinitions (list of partition rename definitions).\nEach RenameDefinition requires: TableName, PartitionName, NewName.\nBatch Options: ContinueOnError, UseTransaction.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Rename\",\n        \"RenameDefinitions\": [\n            { \"TableName\": \"Sales\", \"PartitionName\": \"OldPartition1\", \"NewName\": \"NewPartition1\" },\n            { \"TableName\": \"Sales\", \"PartitionName\": \"OldPartition2\", \"NewName\": \"NewPartition2\" }\n        ],\n        \"Options\": { \"ContinueOnError\": false, \"UseTransaction\": true }\n    }\n}" }
			},
			["ExportTMDL"] = new OperationMetadata
			{
				RequiredParams = new string[1] { "References" },
				Description = "Export partition to TMDL (YAML-like syntax) format. TMDL is a human-readable, declarative format for semantic models.\nMandatory properties: References (list with at least one PartitionReference containing TableName).\nOptional: PartitionName (required only if table has multiple partitions), TmdlExportOptions (TmdlSerializationOptions.IncludeChildren defaults to false).\nNote: Only the first reference is used; additional references are ignored with a warning.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ExportTMDL\",\n        \"TmdlExportOptions\": {\n            \"TmdlSerializationOptions\": {\n                \"IncludeChildren\": true\n            }\n        },\n        \"References\": [\n            { \"TableName\": \"Sales\", \"PartitionName\": \"Partition1\" }\n        ]\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"ExportTMDL\",\n        \"References\": [\n            { \"TableName\": \"Product\" }\n        ]\n    }\n}" }
			},
			["ExportTMSL"] = new OperationMetadata
			{
				RequiredParams = new string[2] { "References", "TmslExportOptions" },
				Description = "Export partition to TMSL (JSON syntax) script format with specified operation type. TMSL generates executable JSON scripts for partition operations.\nMandatory properties: References (list with at least one PartitionReference containing TableName), TmslExportOptions (with TmslOperationType).\nOptional: PartitionName (required only if table has multiple partitions), TmslExportOptions properties (IncludeRestricted, RefreshType for Refresh operations, SaveToFile, FilePath, TruncateAfter).\nValid TmslOperationType values: Create, Delete, Refresh, Alter.\nNote: Only the first reference is used; additional references are ignored with a warning.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ExportTMSL\",\n        \"References\": [\n            { \"TableName\": \"Sales\", \"PartitionName\": \"Partition1\" }\n        ],\n        \"TmslExportOptions\": {\n            \"TmslOperationType\": \"Create\",\n            \"IncludeRestricted\": false\n        }\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"ExportTMSL\",\n        \"References\": [\n            { \"TableName\": \"Product\" }\n        ],\n        \"TmslExportOptions\": {\n            \"TmslOperationType\": \"CreateOrReplace\"\n        }\n    }\n}", "{\n    \"request\": {\n        \"Operation\": \"ExportTMSL\",\n        \"References\": [\n            { \"TableName\": \"Sales\", \"PartitionName\": \"Partition1\" }\n        ],\n        \"TmslExportOptions\": {\n            \"TmslOperationType\": \"Refresh\",\n            \"RefreshType\": \"Full\"\n        }\n    }\n}" }
			},
			["Help"] = new OperationMetadata
			{
				Description = "Describe the tool and its operations.\nMandatory properties: None.\nOptional: None.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Help\"\n    }\n}" }
			}
		}
	};

	public PartitionOperationsTool(ILogger<PartitionOperationsTool> logger, IWriteGuard writeGuard, IEnhancedRefreshService? enhancedRefreshService = null)
	{
		_logger = logger;
		_writeGuard = writeGuard;
		_enhancedRefreshService = enhancedRefreshService;
	}

	[McpServerTool(Name = "partition_operations", Title = "Partition Operations", ReadOnly = false, Destructive = true)]
	[YamlToolDescription("partition_operations")]
	public async Task<CallToolResult> ExecutePartitionOperation(McpServer mcpServer, PartitionOperationRequest request)
	{
		_logger.LogDebug("Executing {ToolName}.{Operation}: Connection={ConnectionName}", "PartitionOperationsTool", request.Operation, request.ConnectionName ?? "(last used)");
		string[] validOperations = new string[13]
		{
			"LIST", "GET", "CREATE", "UPDATE", "DELETE", "REFRESHWITHXMLA", "REFRESHWITHAPI", "CHECKSTATUSOFREFRESHWITHAPI", "CANCELREFRESHWITHAPI", "RENAME",
			"EXPORTTMDL", "EXPORTTMSL", "HELP"
		};
		string[] writeOperations = new string[6] { "CREATE", "UPDATE", "DELETE", "REFRESHWITHXMLA", "REFRESHWITHAPI", "RENAME" };
		string op = request.Operation.ToUpperInvariant();
		ToolCallAnnotations annotations = ToolCallAnnotations.Create("partition_operations", request.Operation, !Enumerable.Contains(writeOperations, op));
		CallToolResult result = null;
		try
		{
			if (!Enumerable.Contains(validOperations, op))
			{
				_logger.LogWarning("Invalid operation '{Operation}' requested for {ToolName}. Valid operations: {ValidOperations}", request.Operation, "PartitionOperationsTool", string.Join(", ", validOperations));
				CallToolResult result2;
				result = (result2 = CallToolResultHelper.FromResponse(PartitionOperationResponse.Forbidden(request.Operation, "Invalid operation: " + request.Operation + ". Supported operations: " + string.Join(", ", validOperations)), annotations));
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
					_logger.LogWarning("{ToolName}.{Operation} blocked by write guard: {Reason}", "PartitionOperationsTool", request.Operation, writeOperationResult.Message);
					CallToolResult result2;
					result = (result2 = CallToolResultHelper.FromResponse(PartitionOperationResponse.Forbidden(request.Operation, writeOperationResult.Message), annotations));
					return result2;
				}
			}
			bool isWriteEnabled = _writeGuard.IsWriteEnabled;
			CallToolResult result3;
			result = (result3 = request.Operation.ToUpperInvariant() switch
			{
				"LIST" => CallToolResultHelper.FromResponse(await HandleListOperation(request), annotations), 
				"GET" => CallToolResultHelper.FromResponse(await HandleGetOperation(request), annotations), 
				"CREATE" => CallToolResultHelper.FromResponse(await HandleCreateOperation(request), annotations, null, minimalSuccessPayload: true), 
				"UPDATE" => CallToolResultHelper.FromResponse(await HandleUpdateOperation(request), annotations, null, minimalSuccessPayload: true), 
				"DELETE" => CallToolResultHelper.FromResponse(await HandleDeleteOperation(request), annotations, null, minimalSuccessPayload: true), 
				"REFRESHWITHXMLA" => CallToolResultHelper.FromResponse(await HandleRefreshWithXMLAOperation(request), annotations, null, minimalSuccessPayload: true), 
				"REFRESHWITHAPI" => CallToolResultHelper.FromResponse(await HandleRefreshWithAPIOperation(request), annotations), 
				"CHECKSTATUSOFREFRESHWITHAPI" => CallToolResultHelper.FromResponse(await HandleCheckStatusOfRefreshWithAPIOperation(request), annotations), 
				"CANCELREFRESHWITHAPI" => CallToolResultHelper.FromResponse(await HandleCancelRefreshWithAPIOperation(request), annotations), 
				"RENAME" => CallToolResultHelper.FromResponse(await HandleRenameOperation(request), annotations, null, minimalSuccessPayload: true), 
				"EXPORTTMDL" => CallToolResultHelper.FromExportResponse(await HandleExportTMDLOperation(request), (request.References?.FirstOrDefault()?.Name ?? "partition") + ".tmdl", "text/plain", annotations), 
				"EXPORTTMSL" => CallToolResultHelper.FromExportResponse(await HandleExportTMSLOperation(request), (request.References?.FirstOrDefault()?.Name ?? "partition") + ".json", "text/plain", annotations), 
				"HELP" => CallToolResultHelper.FromResponse(HandleHelpOperation(request, isWriteEnabled ? validOperations : validOperations.Except(writeOperations).ToArray()), annotations), 
				_ => CallToolResultHelper.FromResponse(PartitionOperationResponse.Forbidden(request.Operation, "Operation " + request.Operation + " not implemented"), annotations), 
			});
			return result3;
		}
		catch (Exception ex)
		{
			_logger.LogOperationError("PartitionOperationsTool", request.Operation, ex);
			string message = op switch
			{
				"LIST" => "Failed to list partitions: " + ex.GetErrorMessage(), 
				"GET" => "Failed to get partitions: " + ex.GetErrorMessage(), 
				"CREATE" => "Failed to create partitions: " + ex.GetErrorMessage(), 
				"UPDATE" => "Failed to update partitions: " + ex.GetErrorMessage(), 
				"DELETE" => "Failed to delete partitions: " + ex.GetErrorMessage(), 
				"REFRESHWITHXMLA" => "Failed to refresh partitions: " + ex.GetErrorMessage(), 
				"REFRESHWITHAPI" => "Failed to start refresh: " + ex.GetErrorMessage(), 
				"CHECKSTATUSOFREFRESHWITHAPI" => "Failed to check refresh status: " + ex.GetErrorMessage(), 
				"CANCELREFRESHWITHAPI" => "Failed to cancel refresh: " + ex.GetErrorMessage(), 
				"RENAME" => "Failed to rename partitions: " + ex.GetErrorMessage(), 
				"EXPORTTMDL" => "Failed to export TMDL for partition: " + ex.GetErrorMessage(), 
				"EXPORTTMSL" => "Error exporting partition TMSL: " + ex.GetErrorMessage(), 
				_ => "Error executing partition operation: " + ex.GetErrorMessage(), 
			};
			CallToolResult result2;
			result = (result2 = CallToolResultHelper.FromResponse(new PartitionOperationResponse
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

	private async Task<PartitionOperationResponse> HandleListOperation(PartitionOperationRequest request)
	{
		string tableName = request.Filter?.TableName;
		int maxResults = request.Filter?.MaxResults ?? 200;
		(List<TablePartitionList>, int) obj = await PartitionOperations.ListPartitionsGrouped(request.ConnectionName, tableName, maxResults);
		List<TablePartitionList> item = obj.Item1;
		int item2 = obj.Item2;
		int value = item.Sum((TablePartitionList t) => t.Partitions.Count);
		bool flag = maxResults > 0 && item2 > maxResults;
		List<string> list = new List<string>();
		string message;
		if (string.IsNullOrWhiteSpace(tableName))
		{
			message = $"Found {value} partitions across {item.Count} tables";
			if (flag)
			{
				list.Add($"Results truncated: Showing {value} of {item2} partitions (limited by MaxResults={maxResults})");
			}
		}
		else
		{
			message = $"Found {value} partitions in table '{tableName}'";
			if (flag)
			{
				list.Add($"Results truncated: Showing {value} of {item2} partitions (limited by MaxResults={maxResults})");
			}
		}
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, TablesCount={TablesCount}, TotalPartitions={TotalPartitions}, IsTruncated={IsTruncated}", "PartitionOperationsTool", "List", request.ConnectionName, item.Count, item2, flag);
		return new PartitionOperationResponse
		{
			Success = true,
			Message = message,
			Operation = "LIST",
			Data = item,
			Warnings = (list.Any() ? list : null)
		};
	}

	private async Task<PartitionOperationResponse> HandleGetOperation(PartitionOperationRequest request)
	{
		List<PartitionReference> list = request.References ?? new List<PartitionReference>();
		if (list.Count == 0)
		{
			_logger.LogWarning("References is required for Get operation");
			return new PartitionOperationResponse
			{
				Success = false,
				Message = "References is required for Get operation and must contain at least one partition reference",
				Operation = "GET",
				Help = toolMetadata.Operations.GetValueOrDefault("GET")
			};
		}
		BatchOperationResponse batchOperationResponse = await PartitionOperations.GetPartitions(request.ConnectionName, list, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Total={Total}, Successful={Successful}, Failed={Failed}", "PartitionOperationsTool", "Get", request.ConnectionName, batchOperationResponse.Summary?.TotalItems ?? 0, batchOperationResponse.Summary?.SuccessCount ?? 0, batchOperationResponse.Summary?.FailureCount ?? 0);
		return MapBatchResponse("GET", batchOperationResponse);
	}

	private async Task<PartitionOperationResponse> HandleCreateOperation(PartitionOperationRequest request)
	{
		List<PartitionDefinition> list = request.Definitions ?? new List<PartitionDefinition>();
		if (list.Count == 0)
		{
			_logger.LogWarning("Definitions is required for Create operation");
			return new PartitionOperationResponse
			{
				Success = false,
				Message = "Definitions is required for Create operation and must contain at least one partition definition",
				Operation = "CREATE",
				Help = toolMetadata.Operations.GetValueOrDefault("CREATE")
			};
		}
		BatchOperationResponse batchOperationResponse = await PartitionOperations.CreatePartitions(request.ConnectionName, list, request.Options, _writeGuard);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Total={Total}, Successful={Successful}, Failed={Failed}", "PartitionOperationsTool", "Create", request.ConnectionName, batchOperationResponse.Summary?.TotalItems ?? 0, batchOperationResponse.Summary?.SuccessCount ?? 0, batchOperationResponse.Summary?.FailureCount ?? 0);
		return MapBatchResponse("CREATE", batchOperationResponse);
	}

	private async Task<PartitionOperationResponse> HandleUpdateOperation(PartitionOperationRequest request)
	{
		List<PartitionDefinition> list = request.Definitions ?? new List<PartitionDefinition>();
		if (list.Count == 0)
		{
			_logger.LogWarning("Definitions is required for Update operation");
			return new PartitionOperationResponse
			{
				Success = false,
				Message = "Definitions is required for Update operation and must contain at least one partition definition",
				Operation = "UPDATE",
				Help = toolMetadata.Operations.GetValueOrDefault("UPDATE")
			};
		}
		BatchOperationResponse batchOperationResponse = await PartitionOperations.UpdatePartitions(request.ConnectionName, list, request.Options, _writeGuard);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Total={Total}, Successful={Successful}, Failed={Failed}", "PartitionOperationsTool", "Update", request.ConnectionName, batchOperationResponse.Summary?.TotalItems ?? 0, batchOperationResponse.Summary?.SuccessCount ?? 0, batchOperationResponse.Summary?.FailureCount ?? 0);
		return MapBatchResponse("UPDATE", batchOperationResponse);
	}

	private async Task<PartitionOperationResponse> HandleDeleteOperation(PartitionOperationRequest request)
	{
		List<PartitionReference> list = request.References ?? new List<PartitionReference>();
		if (list.Count == 0)
		{
			_logger.LogWarning("References is required for Delete operation");
			return new PartitionOperationResponse
			{
				Success = false,
				Message = "References is required for Delete operation and must contain at least one partition reference",
				Operation = "DELETE",
				Help = toolMetadata.Operations.GetValueOrDefault("DELETE")
			};
		}
		BatchOperationResponse batchOperationResponse = await PartitionOperations.DeletePartitions(request.ConnectionName, list, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Total={Total}, Successful={Successful}, Failed={Failed}", "PartitionOperationsTool", "Delete", request.ConnectionName, batchOperationResponse.Summary?.TotalItems ?? 0, batchOperationResponse.Summary?.SuccessCount ?? 0, batchOperationResponse.Summary?.FailureCount ?? 0);
		return MapBatchResponse("DELETE", batchOperationResponse);
	}

	private async Task<PartitionOperationResponse> HandleRefreshWithXMLAOperation(PartitionOperationRequest request)
	{
		List<PartitionRefresh> list = request.RefreshDefinitions ?? new List<PartitionRefresh>();
		if (list.Count == 0)
		{
			_logger.LogWarning("RefreshDefinitions is required for RefreshWithXMLA operation");
			return new PartitionOperationResponse
			{
				Success = false,
				Message = "RefreshDefinitions is required for RefreshWithXMLA operation and must contain at least one partition refresh definition",
				Operation = "REFRESHWITHXMLA",
				Help = toolMetadata.Operations.GetValueOrDefault("RefreshWithXMLA")
			};
		}
		BatchOperationResponse batchOperationResponse = await PartitionOperations.RefreshPartitions(request.ConnectionName, list, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Total={Total}, Successful={Successful}, Failed={Failed}", "PartitionOperationsTool", "RefreshWithXMLA", request.ConnectionName, batchOperationResponse.Summary?.TotalItems ?? 0, batchOperationResponse.Summary?.SuccessCount ?? 0, batchOperationResponse.Summary?.FailureCount ?? 0);
		return MapBatchResponse("REFRESHWITHXMLA", batchOperationResponse);
	}

	private async Task<PartitionOperationResponse> HandleRefreshWithAPIOperation(PartitionOperationRequest request)
	{
		PartitionOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(request.ConnectionName))
		{
			if (!connectionInfo.IsCloudConnection)
			{
				result = new PartitionOperationResponse
				{
					Success = false,
					Message = "This operation is only supported for Fabric cloud connections. Use RefreshWithXMLA instead.",
					Operation = request.Operation
				};
			}
			else
			{
				string text = connectionInfo.Database?.ID;
				if (string.IsNullOrEmpty(text))
				{
					result = PartitionOperationResponse.Forbidden("RefreshWithAPI", "No database ID available.");
				}
				else if (string.IsNullOrEmpty(request.TableName))
				{
					result = PartitionOperationResponse.Forbidden("RefreshWithAPI", "TableName is required for partition refresh via API.");
				}
				else if (string.IsNullOrEmpty(request.PartitionName))
				{
					result = PartitionOperationResponse.Forbidden("RefreshWithAPI", "PartitionName is required for partition refresh via API.");
				}
				else
				{
					string workspaceId = connectionInfo.WorkspaceId;
					EnhancedRefreshResult enhancedRefreshResult = await (_enhancedRefreshService ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("RefreshWithAPI requires IEnhancedRefreshService to be registered.")).StartRefreshAsync(workspaceId, text, request.RefreshType ?? "Automatic", request.TableName, request.PartitionName);
					result = new PartitionOperationResponse
					{
						Success = enhancedRefreshResult.Success,
						Message = enhancedRefreshResult.Message + ((enhancedRefreshResult.RequestId != null) ? (" Use CheckStatusOfRefreshWithAPI with RequestId '" + enhancedRefreshResult.RequestId + "' to monitor progress.") : string.Empty),
						Operation = "REFRESHWITHAPI",
						Data = new { enhancedRefreshResult.RequestId }
					};
				}
			}
		}
		return result;
	}

	private async Task<PartitionOperationResponse> HandleCheckStatusOfRefreshWithAPIOperation(PartitionOperationRequest request)
	{
		if (string.IsNullOrEmpty(request.RequestId))
		{
			return PartitionOperationResponse.Forbidden("CheckStatusOfRefreshWithAPI", "RequestId is required. Use the request ID returned by RefreshWithAPI.");
		}
		PartitionOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(request.ConnectionName))
		{
			if (!connectionInfo.IsCloudConnection)
			{
				result = new PartitionOperationResponse
				{
					Success = false,
					Message = "This operation is only supported for Fabric cloud connections. Use RefreshWithXMLA instead.",
					Operation = request.Operation
				};
			}
			else
			{
				string text = connectionInfo.Database?.ID;
				if (string.IsNullOrEmpty(text))
				{
					result = PartitionOperationResponse.Forbidden("CheckStatusOfRefreshWithAPI", "No database ID available.");
				}
				else
				{
					string workspaceId = connectionInfo.WorkspaceId;
					EnhancedRefreshStatusResult enhancedRefreshStatusResult = await (_enhancedRefreshService ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("RefreshWithAPI requires IEnhancedRefreshService to be registered.")).GetRefreshStatusAsync(workspaceId, text, request.RequestId);
					result = new PartitionOperationResponse
					{
						Success = (enhancedRefreshStatusResult.Status != "Failed" && enhancedRefreshStatusResult.Status != "Error"),
						Message = enhancedRefreshStatusResult.Message,
						Operation = "CHECKSTATUSOFREFRESHWITHAPI",
						Data = new { enhancedRefreshStatusResult.Status, enhancedRefreshStatusResult.RequestId, enhancedRefreshStatusResult.StartTime, enhancedRefreshStatusResult.EndTime }
					};
				}
			}
		}
		return result;
	}

	private async Task<PartitionOperationResponse> HandleCancelRefreshWithAPIOperation(PartitionOperationRequest request)
	{
		if (string.IsNullOrEmpty(request.RequestId))
		{
			return PartitionOperationResponse.Forbidden("CancelRefreshWithAPI", "RequestId is required.");
		}
		PartitionOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(request.ConnectionName))
		{
			if (!connectionInfo.IsCloudConnection)
			{
				result = new PartitionOperationResponse
				{
					Success = false,
					Message = "This operation is only supported for Fabric cloud connections. Use RefreshWithXMLA instead.",
					Operation = request.Operation
				};
			}
			else
			{
				string text = connectionInfo.Database?.ID;
				if (string.IsNullOrEmpty(text))
				{
					result = PartitionOperationResponse.Forbidden("CancelRefreshWithAPI", "No database ID available.");
				}
				else
				{
					string workspaceId = connectionInfo.WorkspaceId;
					await (_enhancedRefreshService ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("RefreshWithAPI requires IEnhancedRefreshService to be registered.")).CancelRefreshAsync(workspaceId, text, request.RequestId);
					result = new PartitionOperationResponse
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

	private async Task<PartitionOperationResponse> HandleRenameOperation(PartitionOperationRequest request)
	{
		List<PartitionRename> list = request.RenameDefinitions ?? new List<PartitionRename>();
		if (list.Count == 0)
		{
			_logger.LogWarning("RenameDefinitions is required for Rename operation");
			return new PartitionOperationResponse
			{
				Success = false,
				Message = "RenameDefinitions is required for Rename operation and must contain at least one partition rename definition",
				Operation = "RENAME",
				Help = toolMetadata.Operations.GetValueOrDefault("RENAME")
			};
		}
		BatchOperationResponse batchOperationResponse = await PartitionOperations.RenamePartitions(request.ConnectionName, list, request.Options);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Total={Total}, Successful={Successful}, Failed={Failed}", "PartitionOperationsTool", "Rename", request.ConnectionName, batchOperationResponse.Summary?.TotalItems ?? 0, batchOperationResponse.Summary?.SuccessCount ?? 0, batchOperationResponse.Summary?.FailureCount ?? 0);
		return MapBatchResponse("RENAME", batchOperationResponse);
	}

	private async Task<PartitionOperationResponse> HandleExportTMDLOperation(PartitionOperationRequest request)
	{
		ExportValidationResult validation = ExportValidationHelper.ValidateReferences(request.References, "Partition");
		if (!validation.IsValid)
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new PartitionOperationResponse
			{
				Success = false,
				Operation = "ExportTMDL",
				Message = validation.ErrorMessage,
				Help = value
			};
		}
		PartitionReference partitionReference = request.References.First();
		ExportValidationResult exportValidationResult = ExportValidationHelper.ValidatePartitionReference(partitionReference.TableName);
		if (!exportValidationResult.IsValid)
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value2);
			return new PartitionOperationResponse
			{
				Success = false,
				Operation = "ExportTMDL",
				Message = exportValidationResult.ErrorMessage,
				Help = value2
			};
		}
		string tableName = partitionReference.TableName;
		string partitionName = partitionReference.Name;
		TmdlExportResult tmdlExportResult = await PartitionOperations.ExportTMDL(request.ConnectionName, tableName, partitionName, request.TmdlExportOptions ?? new ExportTmdl());
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "PartitionOperationsTool", request.Operation, request.ConnectionName);
		string objectIdentifier = ((partitionName != null) ? (tableName + "." + partitionName) : tableName);
		string text = ExportValidationHelper.FormatSuccessMessage("Partition", objectIdentifier, validation.WarningMessage);
		List<string> warnings = ((!string.IsNullOrEmpty(validation.WarningMessage)) ? new List<string> { validation.WarningMessage } : null);
		return new PartitionOperationResponse
		{
			Success = tmdlExportResult.Success,
			Message = (tmdlExportResult.Success ? text : (tmdlExportResult.ErrorMessage ?? "Failed to export TMDL")),
			ErrorSource = tmdlExportResult.ErrorSource,
			Operation = request.Operation,
			Data = tmdlExportResult.Content,
			Warnings = warnings
		};
	}

	private async Task<PartitionOperationResponse> HandleExportTMSLOperation(PartitionOperationRequest request)
	{
		ExportValidationResult validation = ExportValidationHelper.ValidateReferences(request.References, "Partition", "ExportTMSL");
		if (!validation.IsValid)
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value);
			return new PartitionOperationResponse
			{
				Success = false,
				Operation = "ExportTMSL",
				Message = validation.ErrorMessage,
				Help = value
			};
		}
		PartitionReference partitionReference = request.References.First();
		ExportValidationResult exportValidationResult = ExportValidationHelper.ValidatePartitionReference(partitionReference.TableName, "ExportTMSL");
		if (!exportValidationResult.IsValid)
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value2);
			return new PartitionOperationResponse
			{
				Success = false,
				Operation = "ExportTMSL",
				Message = exportValidationResult.ErrorMessage,
				Help = value2
			};
		}
		if (request.TmslExportOptions == null)
		{
			toolMetadata.Operations.TryGetValue(request.Operation, out OperationMetadata value3);
			return new PartitionOperationResponse
			{
				Success = false,
				Operation = "ExportTMSL",
				Message = "TmslExportOptions is required for ExportTMSL operation",
				Help = value3
			};
		}
		string tableName = partitionReference.TableName;
		string partitionName = partitionReference.Name;
		TmslExportResult tmslExportResult = await PartitionOperations.ExportTMSL(request.ConnectionName, tableName, partitionName, request.TmslExportOptions);
		if (tmslExportResult.Success)
		{
			_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, TmslType={TmslType}", "PartitionOperationsTool", request.Operation, request.ConnectionName, request.TmslExportOptions.TmslOperationType);
		}
		else
		{
			_logger.LogWarning("{ToolName}.{Operation} failed", "PartitionOperationsTool", request.Operation);
		}
		string objectIdentifier = ((partitionName != null) ? (tableName + "." + partitionName) : tableName);
		string message = (tmslExportResult.Success ? ExportValidationHelper.FormatTmslSuccessMessage("Partition", objectIdentifier, request.TmslExportOptions.TmslOperationType ?? "Unknown", validation.WarningMessage) : (tmslExportResult.ErrorMessage ?? "Unknown error occurred"));
		List<string> warnings = ((!string.IsNullOrEmpty(validation.WarningMessage)) ? new List<string> { validation.WarningMessage } : null);
		return new PartitionOperationResponse
		{
			Success = tmslExportResult.Success,
			Message = message,
			ErrorSource = tmslExportResult.ErrorSource,
			Operation = request.Operation,
			Data = tmslExportResult.Content,
			Warnings = warnings
		};
	}

	private PartitionOperationResponse HandleHelpOperation(PartitionOperationRequest request, string[] operations)
	{
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Operations={OperationCount}", "PartitionOperationsTool", request.Operation, request.ConnectionName, operations.Length);
		PartitionOperationResponse partitionOperationResponse = new PartitionOperationResponse();
		partitionOperationResponse.Success = true;
		partitionOperationResponse.Message = "Help information for partition operations";
		partitionOperationResponse.Operation = request.Operation;
		partitionOperationResponse.Help = new
		{
			ToolName = "partition_operations",
			Description = "Perform operations on semantic model partitions. Supports batch operations for Create, Update, Delete, Refresh, Rename, Get.",
			SupportedOperations = operations,
			Examples = toolMetadata.Operations.Where<KeyValuePair<string, OperationMetadata>>((KeyValuePair<string, OperationMetadata> p) => operations.Contains<string>(p.Key, StringComparer.OrdinalIgnoreCase)),
			Notes = new string[7] { "The ConnectionName parameter is optional and will use the last used connection if not provided.", "The Operation parameter specifies which operation to perform.", "For Create/Update operations, use the Definitions property.", "For Get/Delete operations, use the References property.", "Each definition/reference should include TableName and Name.", "Use Options to control batch behavior: ContinueOnError (default: false), UseTransaction (default: false).", "ExportTMDL and ExportTMSL operate on a single partition (use TableName and PartitionName properties)." }
		};
		return partitionOperationResponse;
	}

	private static PartitionOperationResponse MapBatchResponse(string operation, BatchOperationResponse batchResponse)
	{
		PartitionOperationResponse partitionOperationResponse = new PartitionOperationResponse
		{
			Success = batchResponse.Success,
			Message = batchResponse.Message,
			Operation = operation,
			Summary = batchResponse.Summary,
			Results = batchResponse.Results,
			Warnings = batchResponse.Warnings
		};
		if (batchResponse.Exceptions.Count > 0)
		{
			partitionOperationResponse.Exceptions.AddRange(batchResponse.Exceptions);
		}
		return partitionOperationResponse;
	}

	private (bool isValid, string? errorMessage) ValidateRequest(string operation, PartitionOperationRequest request)
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
