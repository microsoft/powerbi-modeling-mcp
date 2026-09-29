using System;
using System.Collections.Generic;
using Microsoft.AnalysisServices;
using Microsoft.AnalysisServices.Tabular;

namespace PowerBIModelingMCP.Library.Contracts;

public interface ITabularServer
{
	string Name { get; }

	bool IsInTransaction { get; }

	Microsoft.AnalysisServices.Tabular.TraceCollection Traces { get; }

	IReadOnlyList<Microsoft.AnalysisServices.Tabular.Database> Databases { get; }

	Func<AccessToken, AccessToken>? OnAccessTokenExpired { get; set; }

	void BeginTransaction();

	void RollbackTransaction();

	void CommitTransaction();

	void Connect(string connectionString);

	void Disconnect();

	Microsoft.AnalysisServices.Tabular.Database? FindDatabase(string databaseName);
}
