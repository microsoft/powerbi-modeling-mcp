namespace PowerBIModelingMCP.Library.Contracts;

public interface IAuditEventLogger
{
	void LogEvents(string operationDescription, bool success, OperationType operationType, IConnectionInfo connectionInfo);
}
