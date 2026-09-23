using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public interface IBatchOperationResponse : IOperationResponse, IResultBase
{
	IList<ItemResult>? Results { get; set; }
}
