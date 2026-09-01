using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class ModelOperationResponse : OperationResponseBase, IExportDataResponse
{
	public string? ModelName { get; set; }

	public static ModelOperationResponse Forbidden(string op, string msg, string? modelName = null)
	{
		ModelOperationResponse modelOperationResponse = OperationResponseBase.CreateForbidden<ModelOperationResponse>(op, msg);
		modelOperationResponse.ModelName = modelName;
		return modelOperationResponse;
	}
}
