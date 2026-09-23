using System.Runtime.Serialization;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

[DataContract]
public sealed class ErrorParameter
{
	[DataMember(IsRequired = false, Order = 10, Name = "name", EmitDefaultValue = false)]
	public string? Name { get; set; }

	[DataMember(IsRequired = false, Order = 20, Name = "value", EmitDefaultValue = false)]
	public string? Value { get; set; }
}
