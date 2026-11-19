using System;
using System.Data;
using System.Globalization;
using Microsoft.AnalysisServices;
using Microsoft.AnalysisServices.AdomdClient;
using PowerBIModelingMCP.Library.Contracts;

namespace PowerBIModelingMCP.Library.Adapters;

public sealed class AdomdConnectionAdapter : IAdomdConnection, IDisposable
{
	private readonly AdomdConnection _adomdConnection;

	public string SessionID => _adomdConnection.SessionID;

	public ConnectionState State => _adomdConnection.State;

	public Func<AccessToken, AccessToken>? OnAccessTokenExpired
	{
		get
		{
			return _adomdConnection.OnAccessTokenExpired;
		}
		set
		{
			_adomdConnection.OnAccessTokenExpired = value;
		}
	}

	public AdomdConnectionAdapter(string connectionString, AccessToken? accessToken)
	{
		_adomdConnection = new AdomdConnection(connectionString);
		if (accessToken.HasValue)
		{
			_adomdConnection.AccessToken = accessToken.Value;
		}
	}

	public AdomdCommand CreateCommand()
	{
		return _adomdConnection.CreateCommand();
	}

	public AdomdCommand CreateCommand(string commandText)
	{
		return new AdomdCommand(commandText, _adomdConnection);
	}

	public void Open()
	{
		_adomdConnection.Open();
	}

	public int? RetrieveSpID()
	{
		try
		{
			DataSet schemaDataSet = _adomdConnection.GetSchemaDataSet("DISCOVER_SESSIONS", null);
			if (schemaDataSet == null || schemaDataSet.Tables.Count == 0)
			{
				return null;
			}
			string sessionID = _adomdConnection.SessionID;
			foreach (DataRow row in schemaDataSet.Tables[0].Rows)
			{
				if (string.Equals(row["SESSION_ID"].ToString(), sessionID, StringComparison.OrdinalIgnoreCase))
				{
					return int.Parse(row["SESSION_SPID"].ToString(), CultureInfo.InvariantCulture);
				}
			}
		}
		catch
		{
		}
		return null;
	}

	public void Dispose()
	{
		_adomdConnection.Close();
		_adomdConnection.Dispose();
	}
}
