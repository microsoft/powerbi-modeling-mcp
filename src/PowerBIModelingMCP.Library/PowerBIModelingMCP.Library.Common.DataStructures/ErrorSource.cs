using System.Runtime.Serialization;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public enum ErrorSource
{
	[EnumMember(Value = "System")]
	System,
	[EnumMember(Value = "User")]
	User,
	[EnumMember(Value = "External")]
	External
}
