using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class PartitionOperationResponse : BatchOperationResponseBase, IExportDataResponse
{
	public static PartitionOperationResponse Forbidden(string op, string msg)
	{
		return OperationResponseBase.CreateForbidden<PartitionOperationResponse>(op, msg);
	}
}
