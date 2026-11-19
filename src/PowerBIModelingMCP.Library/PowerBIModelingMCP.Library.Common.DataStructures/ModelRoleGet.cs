using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class ModelRoleGet : ModelRoleBase
{
	public List<Dictionary<string, string>>? TablePermissions { get; set; } = new List<Dictionary<string, string>>();
}
