using System;
using System.ComponentModel;

namespace PowerBIModelingMCP.Library.Common;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class YamlFieldDescriptionAttribute : DescriptionAttribute
{
	public YamlFieldDescriptionAttribute(string toolName, string fieldName)
		: base(ToolDescriptionProvider.GetRequestFieldDescription(toolName, fieldName))
	{
	}
}
