using System.Threading.Tasks;
using ModelContextProtocol.Server;
using PowerBIModelingMCP.Library.Contracts;

namespace PowerBIModelingMCP.Library.Common;

public class WriteGuard : IWriteGuard
{
	private readonly MCPServerConfiguration _cachedConfig;

	private const string ReadOnlyErrorMessage = "The MCP Server is currently running in read-only mode. To perform this operation, user must explicitly start the MCP Server in read-write mode.";

	public bool IsWriteEnabled => _cachedConfig.Mode == ToolMode.ReadWrite;

	public bool IsSkipConfirmationEnabled => _cachedConfig.SkipConfirmation;

	public WriteGuard(MCPServerConfiguration config)
	{
		_cachedConfig = config;
	}

	public (bool allowed, string message) IsWriteAllowed(string opName, string? connectionName)
	{
		if (IsWriteEnabled)
		{
			return (allowed: true, message: "Write allowed.");
		}
		return (allowed: false, message: "Operation '" + opName + "' is not permitted: The MCP Server is currently running in read-only mode. To perform this operation, user must explicitly start the MCP Server in read-write mode.");
	}

	public async Task<WriteOperationResult> ExecuteWriteOperationWithGuards(McpServer mcpServer, string? connectionName, string operationName)
	{
		var (flag, message) = IsWriteAllowed(operationName, connectionName);
		if (!flag)
		{
			return new WriteOperationResult
			{
				Success = false,
				Message = message
			};
		}
		if (!ConfirmationService.ConfirmRequest(mcpServer, connectionName, ConfirmationType.WriteOperation, this))
		{
			return new WriteOperationResult
			{
				Success = false,
				Message = "The user requested a write operation but declined when asked to confirm. Do not retry or initiate any write operations on your own. Wait until the user explicitly confirms or requests a write operation again.",
				UserDeclinedConfirmation = true
			};
		}
		var (flag2, text) = await SyncService.EnsureFreshMetadataForOperation(mcpServer, connectionName, operationName, this);
		if (!flag2)
		{
			return new WriteOperationResult
			{
				Success = false,
				Message = "Failed to ensure fresh metadata from server before write operation: " + (text ?? "Unknown error")
			};
		}
		return new WriteOperationResult
		{
			Success = true,
			Message = "Write operation guards passed successfully"
		};
	}

	public void AssertFullModeRequired(string opName, string reason)
	{
		if (_cachedConfig.Compatibility == CompatibilityMode.PowerBI)
		{
			throw new CompatibilityException("Operation '" + opName + "' is not supported in PowerBI compatibility mode: " + reason);
		}
	}
}
