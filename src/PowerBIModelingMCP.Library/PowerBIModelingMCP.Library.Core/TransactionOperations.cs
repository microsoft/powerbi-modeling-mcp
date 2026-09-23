using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AnalysisServices.Tabular;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;
using PowerBIModelingMCP.Library.Contracts;

namespace PowerBIModelingMCP.Library.Core;

public static class TransactionOperations
{
	internal static TransactionBeginResult BeginTransactionInternal(IConnectionInfo info)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		ConnectionValidator.ValidateForTransactions(info);
		if (info.Transaction != null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Connection '" + info.ConnectionName + "' already has an active transaction. Commit or rollback the existing transaction first.", ErrorSource.User);
		}
		try
		{
			info.TabularServer.BeginTransaction();
		}
		catch (Exception ex)
		{
			throw new McpExceptionWithSource("Failed to begin server transaction: " + ex.Message, ex, null, "Failed to begin server transaction.");
		}
		TransactionContext transactionContext = (info.Transaction = new TransactionContext
		{
			Server = info.TabularServer,
			Database = info.Database
		});
		transactionContext.Operations.Add($"Server transaction started at {transactionContext.StartTime:yyyy-MM-dd HH:mm:ss} UTC");
		return new TransactionBeginResult
		{
			TransactionId = transactionContext.TransactionId,
			Status = "active",
			StartTime = transactionContext.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
			TransactionType = "server-side"
		};
	}

	public static async Task<TransactionBeginResult> BeginTransactionAsync(string? connectionName)
	{
		TransactionBeginResult result;
		await using (IConnectionInfo info = await ConnectionOperations.GetAsync(connectionName))
		{
			result = BeginTransactionInternal(info);
		}
		return result;
	}

	public static async Task<TransactionCommitResult> CommitTransactionAsync(string? connectionName)
	{
		TransactionCommitResult result;
		await using (IConnectionInfo info = await ConnectionOperations.GetAsync(connectionName))
		{
			result = CommitTransactionInternal(info);
		}
		return result;
	}

	internal static TransactionCommitResult CommitTransactionInternal(IConnectionInfo info)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (info.Transaction == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Connection '" + info.ConnectionName + "' does not have an active transaction", ErrorSource.User);
		}
		TransactionContext transaction = info.Transaction;
		try
		{
			ModelOperationResult modelOperationResult = transaction.Database.Model.SaveChanges();
			transaction.Server.CommitTransaction();
			foreach (PendingAuditEntry pendingAuditEntry in transaction.PendingAuditEntries)
			{
				AuditEvent.Default.Emit(pendingAuditEntry.OperationDescription, success: true, pendingAuditEntry.OperationType, pendingAuditEntry.ConnectionInfo);
			}
			transaction.Operations.Add($"Server transaction committed and database updated at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
			return new TransactionCommitResult
			{
				TransactionId = transaction.TransactionId,
				Status = "committed",
				OperationCount = transaction.Operations.Count - 2,
				Duration = (DateTime.UtcNow - transaction.StartTime).TotalSeconds,
				Operations = transaction.Operations,
				TransactionType = "server-side",
				Impact = ((ModelOperationResult.Empty == modelOperationResult) ? null : ObjectImpactSerializer.SerializeToString(modelOperationResult.Impact))
			};
		}
		catch (Exception ex)
		{
			foreach (PendingAuditEntry pendingAuditEntry2 in transaction.PendingAuditEntries)
			{
				AuditEvent.Default.Emit(pendingAuditEntry2.OperationDescription, success: false, pendingAuditEntry2.OperationType, pendingAuditEntry2.ConnectionInfo);
			}
			transaction.Operations.Add("Server transaction commit failed: " + ex.Message);
			throw;
		}
		finally
		{
			info.Transaction = null;
		}
	}

	internal static TransactionRollbackResult RollbackTransactionInternal(IConnectionInfo info)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (info.Transaction == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Connection '" + info.ConnectionName + "' does not have an active transaction", ErrorSource.User);
		}
		TransactionContext transaction = info.Transaction;
		try
		{
			transaction.Server.RollbackTransaction();
			transaction.Operations.Add($"Server transaction rolled back at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
			return new TransactionRollbackResult
			{
				TransactionId = transaction.TransactionId,
				Status = "rolled back",
				OperationCount = transaction.Operations.Count - 2,
				Duration = (DateTime.UtcNow - transaction.StartTime).TotalSeconds,
				Operations = transaction.Operations,
				TransactionType = "server-side"
			};
		}
		catch (Exception ex)
		{
			transaction.Operations.Add("Server transaction rollback failed: " + ex.Message);
			throw new McpExceptionWithSource("Failed to rollback server transaction: " + ex.Message, "Failed to rollback server transaction.");
		}
		finally
		{
			info.Transaction = null;
		}
	}

	public static async Task<TransactionRollbackResult> RollbackTransactionAsync(string? connectionName)
	{
		TransactionRollbackResult result;
		await using (IConnectionInfo info = await ConnectionOperations.GetAsync(connectionName))
		{
			result = RollbackTransactionInternal(info);
		}
		return result;
	}

	public static async Task<TransactionStatusResult> GetTransactionStatusAsync(string? connectionName)
	{
		TransactionStatusResult result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			if (connectionInfo.Transaction == null)
			{
				result = new TransactionStatusResult
				{
					Status = "no active transaction"
				};
			}
			else
			{
				TransactionContext transaction = connectionInfo.Transaction;
				result = new TransactionStatusResult
				{
					TransactionId = transaction.TransactionId,
					Status = "active",
					StartTime = transaction.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
					Duration = (DateTime.UtcNow - transaction.StartTime).TotalSeconds,
					OperationCount = transaction.Operations.Count,
					Operations = transaction.Operations,
					TransactionType = "server-side"
				};
			}
		}
		return result;
	}

	public static async Task<List<ActiveTransactionInfo>> ListActiveTransactionsAsync()
	{
		List<ActiveTransactionInfo> activeTransactions = new List<ActiveTransactionInfo>();
		IReadOnlyList<string> readOnlyList = ConnectionOperations.ListConnectionNames();
		foreach (string item in readOnlyList)
		{
			try
			{
				await using IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(item);
				if (connectionInfo.Transaction != null)
				{
					TransactionContext transaction = connectionInfo.Transaction;
					activeTransactions.Add(new ActiveTransactionInfo
					{
						TransactionId = transaction.TransactionId,
						StartTime = transaction.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
						Duration = (DateTime.UtcNow - transaction.StartTime).TotalSeconds,
						OperationCount = transaction.Operations.Count,
						Database = transaction.Database.Name,
						Server = transaction.Server.Name,
						IsCurrent = true,
						TransactionType = "server-side"
					});
				}
			}
			catch
			{
			}
		}
		return activeTransactions;
	}

	public static bool IsInTransaction(IConnectionInfo info)
	{
		try
		{
			return info.Transaction != null;
		}
		catch
		{
			return false;
		}
	}

	public static void RecordOperation(IConnectionInfo info, string operation)
	{
		try
		{
			if (info.Transaction != null)
			{
				info.Transaction.Operations.Add($"{DateTime.UtcNow:HH:mm:ss} - {operation}");
			}
		}
		catch
		{
		}
	}

	public static async Task CleanupServerTransactionsAsync(Server server)
	{
		IReadOnlyList<string> readOnlyList = ConnectionOperations.ListConnectionNames();
		foreach (string item in readOnlyList)
		{
			try
			{
				await using IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(item);
				if (connectionInfo.Transaction == null || connectionInfo.Transaction.Server != server)
				{
					continue;
				}
				try
				{
					server.RollbackTransaction();
					connectionInfo.Transaction.Operations.Add($"Server transaction automatically rolled back during cleanup at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
				}
				catch (Exception ex)
				{
					connectionInfo.Transaction.Operations.Add("Failed to rollback transaction during cleanup: " + ex.Message);
				}
				finally
				{
					connectionInfo.Transaction = null;
				}
			}
			catch
			{
			}
		}
	}
}
