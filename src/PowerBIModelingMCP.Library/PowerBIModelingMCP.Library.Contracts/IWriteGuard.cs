using System.Threading.Tasks;
using ModelContextProtocol.Server;
using PowerBIModelingMCP.Library.Common;

namespace PowerBIModelingMCP.Library.Contracts;

public interface IWriteGuard
{
	bool IsWriteEnabled { get; }

	bool IsSkipConfirmationEnabled { get; }

	(bool allowed, string message) IsWriteAllowed(string opName, string? connectionName);

	Task<WriteOperationResult> ExecuteWriteOperationWithGuards(McpServer mcpServer, string? connectionName, string operationName);

	void AssertFullModeRequired(string opName, string reason);
}
