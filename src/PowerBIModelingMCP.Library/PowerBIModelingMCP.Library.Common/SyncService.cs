using System;
using System.Threading.Tasks;
using Microsoft.AnalysisServices.Tabular;
using ModelContextProtocol.Server;
using PowerBIModelingMCP.Library.Common.DataStructures;
using PowerBIModelingMCP.Library.Contracts;
using PowerBIModelingMCP.Library.Core;

namespace PowerBIModelingMCP.Library.Common;

public static class SyncService
{
	public enum SyncMode
	{
		SkipIfLocalChanges,
		DiscardLocalChanges,
		CommitLocalChangesThenSync
	}

	public static async Task SyncWithServer(string? connectionName)
	{
		await SyncWithServer(connectionName, SyncMode.SkipIfLocalChanges);
	}

	public static async Task<bool> SyncWithServer(string? connectionName, SyncMode mode)
	{
		bool result;
		await using (IConnectionInfo info = await ConnectionOperations.GetAsync(connectionName))
		{
			result = SyncWithServerInternal(info, mode);
		}
		return result;
	}

	private static SyncStateInfo GetSyncStateInternal(IConnectionInfo info)
	{
		if (info?.Database?.Model == null)
		{
			return new SyncStateInfo
			{
				CanSync = false,
				HasLocalChanges = false,
				IsOffline = (info?.IsOffline ?? true),
				LastSynced = info?.LastSynced,
				Message = "No valid connection or model available"
			};
		}
		bool hasLocalChanges = info.Database.Model.HasLocalChanges;
		bool canSync = !info.IsOffline && info.TabularServer != null;
		return new SyncStateInfo
		{
			CanSync = canSync,
			HasLocalChanges = hasLocalChanges,
			IsOffline = info.IsOffline,
			IsInTransaction = TransactionOperations.IsInTransaction(info),
			LastSynced = info.LastSynced,
			Message = GetSyncStateMessage(canSync, hasLocalChanges, info.IsOffline)
		};
	}

	private static async Task<SyncStateInfo> GetSyncState(string? connectionName)
	{
		_ = 1;
		try
		{
			SyncStateInfo syncStateInternal;
			await using (IConnectionInfo info = await ConnectionOperations.GetAsync(connectionName))
			{
				syncStateInternal = GetSyncStateInternal(info);
			}
			return syncStateInternal;
		}
		catch (Exception ex)
		{
			return new SyncStateInfo
			{
				CanSync = false,
				HasLocalChanges = false,
				IsOffline = true,
				Message = "Error checking sync state: " + ex.Message
			};
		}
	}

	public static async Task<(bool, string?)> EnsureFreshMetadataForOperation(McpServer mcpServer, string? connectionName, string operationName, IWriteGuard writeGuard)
	{
		(bool, string?) result;
		await using (IConnectionInfo conn = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				result = EnsureFreshMetadataForOperationInternal(mcpServer, connectionName, operationName, writeGuard, conn);
			}
			catch (Exception ex) when (conn.IsCloudConnection && SessionEvictionDetection.IsSessionEvictionError(ex))
			{
				await ConnectionOperations.Reconnect(conn);
				result = EnsureFreshMetadataForOperationInternal(mcpServer, connectionName, operationName, writeGuard, conn);
			}
		}
		return result;
	}

	private static (bool, string?) EnsureFreshMetadataForOperationInternal(McpServer mcpServer, string? connectionName, string operationName, IWriteGuard writeGuard, IConnectionInfo info)
	{
		try
		{
			SyncStateInfo syncStateInternal = GetSyncStateInternal(info);
			if (!syncStateInternal.CanSync)
			{
				return (true, null);
			}
			if (!syncStateInternal.HasLocalChanges)
			{
				return (SyncWithServerInternal(info, SyncMode.SkipIfLocalChanges), null);
			}
			if (syncStateInternal.IsInTransaction)
			{
				string item = "Operation '" + operationName + "': Skipping sync due to active transaction with local changes. Save local work and re-apply changes manually";
				return (true, item);
			}
			if (!ConfirmationService.ConfirmGenericRequest(mcpServer, connectionName, "There are local changes that have not been saved. Do you want to discard local changes and refresh metadata from the server?", writeGuard))
			{
				return (false, "Operation '" + operationName + "': There are local changes that haven't been saved. Either commit the transaction or discard local changes. The user declined to discard local changes. Do not retry or initiate any further write operations on your own. Wait until user explicitly confirms or requests a write operation again. The user must save local changes on the model and re-apply changes manually.");
			}
			if (!SyncWithServerInternal(info, SyncMode.DiscardLocalChanges))
			{
				return (false, "Failed to discard local changes and sync with server.");
			}
			return (true, string.Empty);
		}
		catch (Exception ex) when (SessionEvictionDetection.IsSessionEvictionError(ex))
		{
			throw;
		}
		catch (Exception ex2)
		{
			throw new McpExceptionWithSource("Failed to ensure fresh metadata because: " + ex2.Message, ex2, null, "Failed to ensure fresh metadata.");
		}
	}

	private static bool SyncWithServerInternal(IConnectionInfo info, SyncMode mode)
	{
		if (info.Database?.Model == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Model is not present", ErrorSource.User);
		}
		Model model = info.Database.Model;
		bool hasLocalChanges = model.HasLocalChanges;
		switch (mode)
		{
		case SyncMode.SkipIfLocalChanges:
			if (hasLocalChanges)
			{
				return true;
			}
			break;
		case SyncMode.DiscardLocalChanges:
			if (hasLocalChanges)
			{
				try
				{
					model.UndoLocalChanges();
				}
				catch (Exception ex2)
				{
					throw new McpExceptionWithSource("Failed to discard local changes: " + ex2.Message, ex2, null, "Failed to discard local changes.");
				}
			}
			break;
		case SyncMode.CommitLocalChangesThenSync:
			if (hasLocalChanges)
			{
				try
				{
					model.SaveChanges();
				}
				catch (Exception ex)
				{
					throw new McpExceptionWithSource("Failed to commit local changes before sync: " + ex.Message, ex, null, "Failed to commit local changes before sync.");
				}
			}
			break;
		}
		ConnectionOperations.Sync(info);
		return true;
	}

	private static string GetSyncStateMessage(bool canSync, bool hasLocalChanges, bool isOffline)
	{
		if (isOffline)
		{
			return "Connection is offline - sync not available";
		}
		if (!canSync)
		{
			return "No server connection available for sync";
		}
		if (hasLocalChanges)
		{
			return "Has local changes - sync will be skipped in default mode";
		}
		return "Ready to sync";
	}
}
