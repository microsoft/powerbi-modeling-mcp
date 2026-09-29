using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class CalculationGroupOperationResponse : BatchOperationResponseBase, IExportDataResponse
{
	public static CalculationGroupOperationResponse Forbidden(string op, string msg)
	{
		return OperationResponseBase.CreateForbidden<CalculationGroupOperationResponse>(op, msg);
	}
}
