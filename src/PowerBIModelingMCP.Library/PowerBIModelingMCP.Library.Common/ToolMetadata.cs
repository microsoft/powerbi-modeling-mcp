using System;
using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common;

public class ToolMetadata
{
	public Dictionary<string, OperationMetadata> Operations { get; set; } = new Dictionary<string, OperationMetadata>();
}
public class ToolMetadata<TEnum> where TEnum : struct, Enum
{
	public Dictionary<TEnum, OperationMetadata> Operations { get; set; } = new Dictionary<TEnum, OperationMetadata>();
}
