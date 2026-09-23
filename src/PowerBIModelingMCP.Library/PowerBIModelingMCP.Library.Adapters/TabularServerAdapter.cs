using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AnalysisServices;
using Microsoft.AnalysisServices.Tabular;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Contracts;

namespace PowerBIModelingMCP.Library.Adapters;

public sealed class TabularServerAdapter : ITabularServer
{
	private readonly Microsoft.AnalysisServices.Tabular.Server _server;

	public string Name => _server.Name;

	public bool IsInTransaction => _server.IsInTransaction;

	public Microsoft.AnalysisServices.Tabular.TraceCollection Traces => _server.Traces;

	public Func<AccessToken, AccessToken>? OnAccessTokenExpired
	{
		get
		{
			return _server.OnAccessTokenExpired;
		}
		set
		{
			_server.OnAccessTokenExpired = value;
		}
	}

	public IReadOnlyList<Microsoft.AnalysisServices.Tabular.Database> Databases
	{
		get
		{
			List<Microsoft.AnalysisServices.Tabular.Database> list = new List<Microsoft.AnalysisServices.Tabular.Database>();
			foreach (Microsoft.AnalysisServices.Tabular.Database database in _server.Databases)
			{
				list.Add(database);
			}
			return list;
		}
	}

	public TabularServerAdapter(AccessToken? accessToken)
	{
		_server = new Microsoft.AnalysisServices.Tabular.Server();
		if (accessToken.HasValue)
		{
			_server.AccessToken = accessToken.Value;
		}
	}

	public void BeginTransaction()
	{
		_server.BeginTransaction();
	}

	public void CommitTransaction()
	{
		_server.CommitTransaction();
	}

	public void Connect(string connectionString)
	{
		RetryHelper.Default.ExecuteWithRetryAsync(delegate
		{
			_server.Connect(connectionString);
			return Task.CompletedTask;
		}, new RetryOptions
		{
			ShouldRetry = (Exception ex) => ex is ConnectionException
		}).GetAwaiter().GetResult();
	}

	public void Disconnect()
	{
		_server.Disconnect();
	}

	public Microsoft.AnalysisServices.Tabular.Database? FindDatabase(string databaseName)
	{
		return _server.Databases.FindByName(databaseName);
	}

	public void RollbackTransaction()
	{
		_server.RollbackTransaction();
	}
}
