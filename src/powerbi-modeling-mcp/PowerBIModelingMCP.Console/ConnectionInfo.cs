using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AnalysisServices.Tabular;
using PowerBIModelingMCP.Library.Common.DataStructures;
using PowerBIModelingMCP.Library.Contracts;
using PowerBIModelingMCP.Library.Core;

namespace PowerBIModelingMCP.Console;

public class ConnectionInfo : IConnectionInfo, IAsyncDisposable
{
	private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

	private int? _cachedSpid;

	public required string ConnectionName { get; set; }

	public ITabularServer? TabularServer { get; set; }

	public required Database Database { get; set; }

	public string? ServerConnectionString { get; set; }

	public bool IsCloudConnection { get; set; }

	public bool IsOffline { get; set; }

	public string? SourcePath { get; set; }

	public IAdomdConnection? AdomdConnection { get; set; }

	public DateTime? ConnectedAt { get; set; }

	public DateTime? LastUsedAt { get; set; }

	public DateTime? LastSynced { get; set; }

	public TransactionContext? Transaction { get; set; }

	public TraceContext? Trace { get; set; }

	public bool IsLLMCreated { get; set; }

	public SemaphoreSlim Lock => _lock;

	public string? SessionId
	{
		get
		{
			if (IsOffline)
			{
				return null;
			}
			if (AdomdConnection == null)
			{
				return null;
			}
			try
			{
				if (AdomdConnection.State == ConnectionState.Open)
				{
					return AdomdConnection.SessionID;
				}
			}
			catch
			{
			}
			return null;
		}
	}

	public string? SemanticModelId { get; set; }

	public string? WorkspaceId { get; set; }

	public int? Spid
	{
		get
		{
			if (IsOffline || AdomdConnection == null)
			{
				return null;
			}
			if (_cachedSpid.HasValue)
			{
				return _cachedSpid;
			}
			try
			{
				if (AdomdConnection.State == ConnectionState.Open)
				{
					_cachedSpid = AdomdConnection.RetrieveSpID();
					return _cachedSpid;
				}
			}
			catch
			{
			}
			return null;
		}
	}

	public void Disconnect()
	{
		if (Trace != null)
		{
			try
			{
				Trace.Trace.Stop();
				Trace.Trace.Drop();
				Trace.Trace.Dispose();
			}
			catch (Exception)
			{
			}
			finally
			{
				Trace = null;
			}
		}
		if (Transaction != null)
		{
			try
			{
				TabularServer?.RollbackTransaction();
				Transaction.Operations.Add($"Server transaction automatically rolled back during connection disposal at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
			}
			catch (Exception)
			{
			}
			finally
			{
				Transaction = null;
			}
		}
		try
		{
			AdomdConnection?.Dispose();
		}
		catch (Exception)
		{
		}
		try
		{
			TabularServer?.Disconnect();
		}
		catch (Exception)
		{
		}
		_lock?.Dispose();
	}

	public ValueTask DisposeAsync()
	{
		try
		{
			_lock.Release();
		}
		catch (ObjectDisposedException)
		{
		}
		catch (SemaphoreFullException)
		{
		}
		return ValueTask.CompletedTask;
	}
}
