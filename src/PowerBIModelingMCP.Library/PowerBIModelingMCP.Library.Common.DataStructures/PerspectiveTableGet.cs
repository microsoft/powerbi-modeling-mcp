using System;
using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class PerspectiveTableGet : PerspectiveTableBase
{
	public DateTime? ModifiedTime { get; set; }

	public List<PerspectiveColumnGet> PerspectiveColumns { get; set; } = new List<PerspectiveColumnGet>();

	public List<PerspectiveMeasureGet> PerspectiveMeasures { get; set; } = new List<PerspectiveMeasureGet>();

	public List<PerspectiveHierarchyGet> PerspectiveHierarchies { get; set; } = new List<PerspectiveHierarchyGet>();
}
