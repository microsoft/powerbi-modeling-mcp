using System;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class ModelGet : ModelBase
{
	public DateTime? ModifiedTime { get; set; }

	public DateTime? StructureModifiedTime { get; set; }
}
