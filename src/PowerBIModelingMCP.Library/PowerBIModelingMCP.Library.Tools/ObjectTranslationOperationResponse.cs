using System.ComponentModel;
using PowerBIModelingMCP.Library.Common.DataStructures;

namespace PowerBIModelingMCP.Library.Tools;

public class ObjectTranslationOperationResponse : BatchOperationResponseBase
{
	[Description("For single-item operations")]
	public string? CultureName { get; set; }

	[Description("For single-item operations")]
	public string? ObjectType { get; set; }

	[Description("For single-item operations")]
	public string? ObjectDisplayName { get; set; }

	[Description("For single-item operations")]
	public string? Property { get; set; }

	[Description("For Get, Create, Update single-item")]
	public string? Value { get; set; }

	public static ObjectTranslationOperationResponse Forbidden(string op, string msg)
	{
		return OperationResponseBase.CreateForbidden<ObjectTranslationOperationResponse>(op, msg);
	}
}
