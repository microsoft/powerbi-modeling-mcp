using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class SecurityRoleOperationResponse : BatchOperationResponseBase, IExportDataResponse
{
	public static SecurityRoleOperationResponse Forbidden(string op, string msg)
	{
		return OperationResponseBase.CreateForbidden<SecurityRoleOperationResponse>(op, msg);
	}
}
