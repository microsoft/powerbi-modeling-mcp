using System;
using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class PartitionGet : PartitionBase
{
	public DateTime? ModifiedTime { get; set; }

	public string? State { get; set; }

	public string? DataView { get; set; }

	public string? ErrorMessage { get; set; }

	public PartitionGet()
	{
		base.Annotations = new List<KeyValuePair<string, string>>();
		base.ExtendedProperties = new List<ExtendedProperty>();
	}
}
