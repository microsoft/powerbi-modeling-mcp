using System;
using System.Data;
using Microsoft.AnalysisServices;
using Microsoft.AnalysisServices.AdomdClient;

namespace PowerBIModelingMCP.Library.Contracts;

public interface IAdomdConnection : IDisposable
{
	string SessionID { get; }

	ConnectionState State { get; }

	Func<AccessToken, AccessToken>? OnAccessTokenExpired { get; set; }

	void Open();

	AdomdCommand CreateCommand();

	AdomdCommand CreateCommand(string commandText);

	int? RetrieveSpID();
}
