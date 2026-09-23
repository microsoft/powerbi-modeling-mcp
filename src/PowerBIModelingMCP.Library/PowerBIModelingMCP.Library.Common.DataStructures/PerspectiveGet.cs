using System;
using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class PerspectiveGet : PerspectiveBase
{
	public DateTime? ModifiedTime { get; set; }

	public List<PerspectiveTableGet> PerspectiveTables { get; set; } = new List<PerspectiveTableGet>();

	public List<string> Tables { get; set; } = new List<string>();

	public List<string> Columns { get; set; } = new List<string>();

	public List<string> Measures { get; set; } = new List<string>();

	public List<string> Hierarchies { get; set; } = new List<string>();
}
