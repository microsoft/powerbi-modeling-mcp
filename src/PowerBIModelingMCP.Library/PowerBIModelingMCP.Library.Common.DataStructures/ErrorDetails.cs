using System.Runtime.Serialization;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

[DataContract]
public class ErrorDetails
{
	[DataMember(IsRequired = true, Order = 10, Name = "errorCode", EmitDefaultValue = false)]
	public string? ErrorCode { get; set; }

	[DataMember(IsRequired = true, Order = 20, Name = "message", EmitDefaultValue = false)]
	public string? Message { get; set; }

	[DataMember(IsRequired = false, Order = 30, Name = "relatedResource", EmitDefaultValue = false)]
	public RelatedResource? RelatedResource { get; set; }
}
