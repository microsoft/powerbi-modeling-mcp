using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class UserHierarchyOperationResponse : BatchOperationResponseBase, IExportDataResponse
{
	public static UserHierarchyOperationResponse Forbidden(string op, string msg)
	{
		return OperationResponseBase.CreateForbidden<UserHierarchyOperationResponse>(op, msg);
	}
}
