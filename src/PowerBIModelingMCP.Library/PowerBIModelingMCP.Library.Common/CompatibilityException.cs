using System;

namespace PowerBIModelingMCP.Library.Common;

public class CompatibilityException : Exception
{
	public CompatibilityException(string message)
		: base(message)
	{
	}
}
