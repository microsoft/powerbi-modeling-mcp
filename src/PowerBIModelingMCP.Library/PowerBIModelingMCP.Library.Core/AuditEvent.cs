using PowerBIModelingMCP.Library.Contracts;

namespace PowerBIModelingMCP.Library.Core;

public class AuditEvent
{
	private static volatile AuditEvent _default = new AuditEvent(null);

	private readonly IAuditEventLogger? _auditService;

	public static AuditEvent Default => _default;

	public static void ConfigureDefault(IAuditEventLogger? auditService)
	{
		_default = new AuditEvent(auditService);
	}

	public AuditEvent(IAuditEventLogger? auditService)
	{
		_auditService = auditService;
	}

	public void Emit(string operationDescription, bool success, OperationType operationType, IConnectionInfo connectionInfo)
	{
		_auditService?.LogEvents(operationDescription, success, operationType, connectionInfo);
	}
}
