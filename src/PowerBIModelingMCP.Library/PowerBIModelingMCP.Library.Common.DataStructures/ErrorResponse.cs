using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

[DataContract]
public sealed class ErrorResponse : ErrorDetails
{
	[DataMember(IsRequired = false, Order = 10, Name = "requestId", EmitDefaultValue = false)]
	public Guid RequestId { get; set; }

	[DataMember(IsRequired = false, Order = 20, Name = "moreDetails", EmitDefaultValue = false)]
	public IList<ErrorDetails>? MoreDetails { get; set; }

	[DataMember(IsRequired = false, Order = 30, Name = "isRetriable", EmitDefaultValue = true)]
	public bool IsRetriable { get; set; }

	[DataMember(IsRequired = false, Order = 40, Name = "parameters", EmitDefaultValue = false)]
	public IList<ErrorParameter>? Parameters { get; set; }

	[IgnoreDataMember]
	public ErrorSource ErrorSource { get; set; }

	public bool IsValid()
	{
		if (!string.IsNullOrEmpty(base.Message))
		{
			return !string.IsNullOrEmpty(base.ErrorCode);
		}
		return false;
	}
}
