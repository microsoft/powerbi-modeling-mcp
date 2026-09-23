using PowerBIModelingMCP.Library.Contracts;

namespace PowerBIModelingMCP.Library.Core;

public record PendingAuditEntry(string OperationDescription, OperationType OperationType, IConnectionInfo? ConnectionInfo = null);
