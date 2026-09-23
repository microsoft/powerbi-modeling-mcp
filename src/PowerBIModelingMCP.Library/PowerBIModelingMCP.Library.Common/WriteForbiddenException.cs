using System;

namespace PowerBIModelingMCP.Library.Common;

public class WriteForbiddenException : Exception
{
	public WriteForbiddenException(string message)
		: base(message)
	{
	}
}
