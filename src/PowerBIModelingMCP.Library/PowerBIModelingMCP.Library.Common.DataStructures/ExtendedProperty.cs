using System.Text.Json;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class ExtendedProperty
{
	public string Name { get; set; } = string.Empty;

	public string Value { get; set; } = string.Empty;

	public string Type { get; set; } = "String";

	public static ExtendedProperty Create(string name, string value)
	{
		return new ExtendedProperty
		{
			Name = name,
			Value = value,
			Type = "String"
		};
	}

	public static ExtendedProperty Create(string name, string value, string type)
	{
		return new ExtendedProperty
		{
			Name = name,
			Value = value,
			Type = type
		};
	}

	public static ExtendedProperty CreateJson(string name, string jsonValue)
	{
		JsonDocument.Parse(jsonValue);
		return new ExtendedProperty
		{
			Name = name,
			Value = jsonValue,
			Type = "Json"
		};
	}
}
