using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class RelationshipOperationResponse : BatchOperationResponseBase, IExportDataResponse
{
	public static RelationshipOperationResponse Forbidden(string op, string msg)
	{
		return OperationResponseBase.CreateForbidden<RelationshipOperationResponse>(op, msg);
	}
}
