using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class SemanticModelsListResult
{
	public List<FabricSemanticModelGet> SemanticModels { get; set; } = new List<FabricSemanticModelGet>();

	public int Count { get; set; }
}
