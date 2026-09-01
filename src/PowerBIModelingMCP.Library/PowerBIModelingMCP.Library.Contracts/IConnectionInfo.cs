using System;
using Microsoft.AnalysisServices.Tabular;
using PowerBIModelingMCP.Library.Common.DataStructures;
using PowerBIModelingMCP.Library.Core;

namespace PowerBIModelingMCP.Library.Contracts;

public interface IConnectionInfo : IAsyncDisposable
{
	string ConnectionName { get; set; }

	Database Database { get; }

	ITabularServer? TabularServer { get; }

	IAdomdConnection? AdomdConnection { get; }

	bool IsOffline { get; }

	bool IsCloudConnection { get; }

	bool IsLLMCreated { get; }

	string? SourcePath { get; }

	string? ServerConnectionString { get; }

	DateTime? ConnectedAt { get; }

	DateTime? LastUsedAt { get; set; }

	DateTime? LastSynced { get; set; }

	string? SessionId { get; }

	string? SemanticModelId { get; }

	string? WorkspaceId { get; }

	TransactionContext? Transaction { get; set; }

	TraceContext? Trace { get; set; }

	void Disconnect();
}
