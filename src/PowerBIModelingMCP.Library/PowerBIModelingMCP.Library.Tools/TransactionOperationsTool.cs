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
public class TransactionOperationsTool
{
	public const string ToolName = "transaction_operations";

	private readonly ILogger<TransactionOperationsTool> _logger;

	public static readonly ToolMetadata toolMetadata = new ToolMetadata
	{
		Operations = new Dictionary<string, OperationMetadata>(StringComparer.OrdinalIgnoreCase)
		{
			["Begin"] = new OperationMetadata
			{
				Description = "Begin a new transaction for a connection. Creates a server-side transaction that can be used to \ngroup multiple operations together for atomic commit or rollback. Throws an exception if a \ntransaction is already active on the connection.",
				Tips = new string[3] { "Offline connections do not support transactions", "Only one transaction can be active per connection at a time", "Always commit or rollback transactions when done to free resources" },
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Begin\",\n        \"ConnectionName\": \"MyConnection\"\n    }\n}" }
			},
			["Commit"] = new OperationMetadata
			{
				Description = "Commit the active transaction for a connection. Saves all pending changes to the database and \nends the transaction. Throws an exception if no transaction is currently active.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Commit\",\n        \"ConnectionName\": \"MyConnection\"\n    }\n}" }
			},
			["Rollback"] = new OperationMetadata
			{
				Description = "Rollback the active transaction for a connection. Discards all pending changes and ends the \ntransaction without saving to the database. Throws an exception if no transaction is currently active.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Rollback\",\n        \"ConnectionName\": \"MyConnection\"\n    }\n}" }
			},
			["GetStatus"] = new OperationMetadata
			{
				Description = "Get the status of the active transaction for a connection. Returns transaction details including \nID, start time, duration, operation count, and server transaction status.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"GetStatus\",\n        \"ConnectionName\": \"MyConnection\"\n    }\n}" }
			},
			["ListActive"] = new OperationMetadata
			{
				Description = "List all active transactions across all connections. Returns a collection of transaction information \nobjects containing transaction ID, start time, duration, operation count, database name, server name, \nand transaction type for each active transaction.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"ListActive\"\n    }\n}" }
			},
			["Help"] = new OperationMetadata
			{
				Description = "Describe the transaction operations tool and its available operations. Returns tool information, \nsupported operations, and usage examples.",
				ExampleRequests = new List<string> { "{\n    \"request\": {\n        \"Operation\": \"Help\"\n    }\n}" }
			}
		}
	};

	public TransactionOperationsTool(ILogger<TransactionOperationsTool> logger)
	{
		_logger = logger;
	}

	[McpServerTool(Name = "transaction_operations", Title = "Transaction Operations", ReadOnly = false, Destructive = true)]
	[YamlToolDescription("transaction_operations")]
	public async Task<CallToolResult> ExecuteTransactionOperation(McpServer mcpServer, TransactionOperationRequest request, IWriteGuard writeGuard)
	{
		_logger.LogDebug("Executing {ToolName}.{Operation}: Connection={ConnectionName}", "TransactionOperationsTool", request.Operation, request.ConnectionName ?? "(last used)");
		string[] validOperations = new string[6] { "BEGIN", "COMMIT", "ROLLBACK", "GETSTATUS", "LISTACTIVE", "HELP" };
		string[] writeOperations = new string[3] { "BEGIN", "COMMIT", "ROLLBACK" };
		string op = request.Operation.ToUpperInvariant();
		ToolCallAnnotations annotations = ToolCallAnnotations.Create("transaction_operations", request.Operation, !Enumerable.Contains(writeOperations, op));
		CallToolResult result = null;
		try
		{
			if (!Enumerable.Contains(validOperations, op))
			{
				_logger.LogWarning("Invalid operation '{Operation}' requested for {ToolName}. Valid operations: {ValidOperations}", request.Operation, "TransactionOperationsTool", string.Join(", ", validOperations));
				CallToolResult result2;
				result = (result2 = CallToolResultHelper.FromResponse(TransactionOperationResponse.Forbidden(request.Operation, "Invalid operation: " + request.Operation + ". Supported operations: " + string.Join(", ", validOperations)), annotations));
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
					_logger.LogWarning("{ToolName}.{Operation} blocked by write guard: {Reason}", "TransactionOperationsTool", request.Operation, writeOperationResult.Message);
					CallToolResult result2;
					result = (result2 = CallToolResultHelper.FromResponse(new TransactionOperationResponse
					{
						Success = false,
						Message = writeOperationResult.Message,
						Operation = request.Operation
					}, annotations));
					return result2;
				}
			}
			bool isWriteEnabled = writeGuard.IsWriteEnabled;
			CallToolResult result3;
			result = (result3 = request.Operation.ToUpperInvariant() switch
			{
				"BEGIN" => CallToolResultHelper.FromResponse(await HandleBeginOperationAsync(request), annotations), 
				"COMMIT" => CallToolResultHelper.FromResponse(await HandleCommitOperationAsync(request), annotations, null, minimalSuccessPayload: true), 
				"ROLLBACK" => CallToolResultHelper.FromResponse(await HandleRollbackOperationAsync(request), annotations, null, minimalSuccessPayload: true), 
				"GETSTATUS" => CallToolResultHelper.FromResponse(await HandleGetStatusOperationAsync(request), annotations), 
				"LISTACTIVE" => CallToolResultHelper.FromResponse(await HandleListActiveOperationAsync(request), annotations), 
				"HELP" => CallToolResultHelper.FromResponse(HandleHelpOperation(request, isWriteEnabled ? validOperations : validOperations.Except(writeOperations).ToArray()), annotations), 
				_ => CallToolResultHelper.FromResponse(TransactionOperationResponse.Forbidden(request.Operation, "Operation " + request.Operation + " is not implemented"), annotations), 
			});
			return result3;
		}
		catch (Exception ex)
		{
			_logger.LogOperationError("TransactionOperationsTool", request.Operation, ex);
			string message = op switch
			{
				"BEGIN" => "Error starting transaction: " + ex.GetErrorMessage(), 
				"COMMIT" => "Error committing transaction: " + ex.GetErrorMessage(), 
				"ROLLBACK" => "Error rolling back transaction: " + ex.GetErrorMessage(), 
				"GETSTATUS" => "Error getting transaction status: " + ex.GetErrorMessage(), 
				"LISTACTIVE" => "Error listing active transactions: " + ex.GetErrorMessage(), 
				_ => "Error executing transaction operation: " + ex.GetErrorMessage(), 
			};
			CallToolResult result2;
			result = (result2 = CallToolResultHelper.FromResponse(new TransactionOperationResponse
			{
				Success = false,
				Message = message,
				Operation = request.Operation,
				TransactionId = request.TransactionId
			}, annotations, ex));
			return result2;
		}
		finally
		{
			_logger.LogToolCallCompleted(annotations.Title, !annotations.ReadOnlyHint, result?.IsError ?? true, ConnectionOperations.ResolveSemanticModelId());
		}
	}

	private async Task<TransactionOperationResponse> HandleBeginOperationAsync(TransactionOperationRequest request)
	{
		TransactionBeginResult transactionBeginResult = await TransactionOperations.BeginTransactionAsync(request.ConnectionName);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "TransactionOperationsTool", "Begin", request.ConnectionName);
		return new TransactionOperationResponse
		{
			Success = true,
			Message = "Transaction '" + transactionBeginResult.TransactionId + "' started successfully",
			Operation = request.Operation,
			TransactionId = transactionBeginResult.TransactionId,
			Data = transactionBeginResult
		};
	}

	private async Task<TransactionOperationResponse> HandleCommitOperationAsync(TransactionOperationRequest request)
	{
		TransactionCommitResult transactionCommitResult = await TransactionOperations.CommitTransactionAsync(request.ConnectionName);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, OperationCount={OperationCount}", "TransactionOperationsTool", "Commit", request.ConnectionName, transactionCommitResult.OperationCount);
		return new TransactionOperationResponse
		{
			Success = true,
			Message = $"Transaction '{transactionCommitResult.TransactionId}' committed successfully with {transactionCommitResult.OperationCount} operations",
			Operation = request.Operation,
			TransactionId = transactionCommitResult.TransactionId,
			Data = transactionCommitResult
		};
	}

	private async Task<TransactionOperationResponse> HandleRollbackOperationAsync(TransactionOperationRequest request)
	{
		TransactionRollbackResult transactionRollbackResult = await TransactionOperations.RollbackTransactionAsync(request.ConnectionName);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}", "TransactionOperationsTool", "Rollback", request.ConnectionName);
		return new TransactionOperationResponse
		{
			Success = true,
			Message = "Transaction '" + transactionRollbackResult.TransactionId + "' rolled back successfully",
			Operation = request.Operation,
			TransactionId = transactionRollbackResult.TransactionId,
			Data = transactionRollbackResult
		};
	}

	private async Task<TransactionOperationResponse> HandleGetStatusOperationAsync(TransactionOperationRequest request)
	{
		TransactionStatusResult transactionStatusResult = await TransactionOperations.GetTransactionStatusAsync(request.ConnectionName);
		_logger.LogInformation("{ToolName}.{Operation} completed: ConnectionName={ConnectionName}, Status={Status}", "TransactionOperationsTool", "GetStatus", request.ConnectionName, transactionStatusResult.Status);
		return new TransactionOperationResponse
		{
			Success = true,
			Message = "Transaction status: " + transactionStatusResult.Status,
			Operation = request.Operation,
			TransactionId = transactionStatusResult.TransactionId,
			Data = transactionStatusResult
		};
	}

	private async Task<TransactionOperationResponse> HandleListActiveOperationAsync(TransactionOperationRequest request)
	{
		List<ActiveTransactionInfo> list = await TransactionOperations.ListActiveTransactionsAsync();
		_logger.LogInformation("{ToolName}.{Operation} completed: Count={Count}", "TransactionOperationsTool", "ListActive", list.Count);
		return new TransactionOperationResponse
		{
			Success = true,
			Message = $"Found {list.Count} active transactions",
			Operation = request.Operation,
			Data = list
		};
	}

	private TransactionOperationResponse HandleHelpOperation(TransactionOperationRequest request, string[] operations)
	{
		_logger.LogInformation("{ToolName}.{Operation} completed: Operations={OperationCount}", "TransactionOperationsTool", "Help", operations.Length);
		TransactionOperationResponse transactionOperationResponse = new TransactionOperationResponse();
		transactionOperationResponse.Success = true;
		transactionOperationResponse.Message = "Tool description retrieved successfully";
		transactionOperationResponse.Operation = request.Operation;
		transactionOperationResponse.Help = new
		{
			ToolName = "transaction_operations",
			Description = "Perform operations on Analysis Services transactions.",
			SupportedOperations = operations,
			Examples = toolMetadata.Operations.Where<KeyValuePair<string, OperationMetadata>>((KeyValuePair<string, OperationMetadata> p) => Enumerable.Contains(operations, p.Key)),
			Notes = new string[3] { "Use the Operation parameter to specify which operation to perform.", "Use the ConnectionName parameter to specify the connection to use for the operation.", "Use the TransactionId parameter to specify the transaction to operate on." }
		};
		return transactionOperationResponse;
	}

	private (bool isValid, string? errorMessage) ValidateRequest(string operation, TransactionOperationRequest request)
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
			string text2 = "Forbidden parameters for " + operation + " operation: " + string.Join(", ", list2);
			_logger.LogWarning(text2);
			return (isValid: false, errorMessage: text2);
		}
		return (isValid: true, errorMessage: null);
	}
}
