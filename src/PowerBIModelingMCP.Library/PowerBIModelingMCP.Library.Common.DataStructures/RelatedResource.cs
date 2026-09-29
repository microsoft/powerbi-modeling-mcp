using System.Runtime.Serialization;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

[DataContract]
public sealed class RelatedResource
{
	[DataMember(IsRequired = false, Order = 10, Name = "resourceId", EmitDefaultValue = false)]
	public string? ResourceId { get; set; }

	[DataMember(IsRequired = false, Order = 20, Name = "resourceType", EmitDefaultValue = false)]
	public string? ResourceType { get; set; }
}
