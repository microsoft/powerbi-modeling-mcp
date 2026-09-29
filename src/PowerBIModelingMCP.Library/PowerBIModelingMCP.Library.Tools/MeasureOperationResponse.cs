using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class MeasureOperationResponse : BatchOperationResponseBase, IExportDataResponse
{
	public static MeasureOperationResponse Forbidden(string op, string msg)
	{
		return OperationResponseBase.CreateForbidden<MeasureOperationResponse>(op, msg);
	}
}
