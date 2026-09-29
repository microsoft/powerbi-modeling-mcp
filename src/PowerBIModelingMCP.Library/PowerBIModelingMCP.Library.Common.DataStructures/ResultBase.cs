using System.Text.Json.Serialization;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public abstract class ResultBase : IResultBase
{
	[JsonIgnore]
	public bool Success { get; set; }
}
