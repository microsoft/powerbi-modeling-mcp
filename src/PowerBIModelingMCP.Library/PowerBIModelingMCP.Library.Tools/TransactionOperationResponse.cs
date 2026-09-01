using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class TransactionOperationResponse : OperationResponseBase
{
	public string? TransactionId { get; set; }

	public static TransactionOperationResponse Forbidden(string op, string msg)
	{
		return OperationResponseBase.CreateForbidden<TransactionOperationResponse>(op, msg);
	}
}
