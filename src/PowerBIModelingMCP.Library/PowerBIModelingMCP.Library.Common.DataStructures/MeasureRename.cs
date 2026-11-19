using System.ComponentModel;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class MeasureRename : ObjectRenameBase
{
	[Description("Optional if measure name is unique")]
	public string? TableName { get; set; }
}
