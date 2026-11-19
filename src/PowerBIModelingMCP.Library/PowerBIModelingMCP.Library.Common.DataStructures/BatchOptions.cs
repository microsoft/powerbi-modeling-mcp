using System.ComponentModel;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class BatchOptions
{
	public bool ContinueOnError { get; set; }

	[Description("Default: true")]
	public bool UseTransaction { get; set; } = true;
}
