using System;
using System.Collections.Generic;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public interface IOperationResponse : IResultBase
{
	string Operation { get; set; }

	string Message { get; set; }

	IList<string>? Warnings { get; set; }

	IList<Exception> Exceptions { get; }

	ErrorSource? ErrorSource { get; set; }
}
