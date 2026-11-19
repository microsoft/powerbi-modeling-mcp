using System;
using System.Collections.Generic;
using PowerBIModelingMCP.Library.Common.DataStructures;
using PowerBIModelingMCP.Library.Contracts;
using PowerBIModelingMCP.Library.Core;

namespace PowerBIModelingMCP.Library.Common;

public static class BatchTransactionHelper
{
	public static TransactionSetupResult HandleTransactionSetup(IConnectionInfo connectionInfo, bool useTransaction, string? connectionName, List<string> warnings)
	{
		if (!useTransaction)
		{
			return new TransactionSetupResult
			{
				TransactionId = null,
				OwnsTransaction = false
			};
		}
		if (connectionInfo.IsOffline)
		{
			return new TransactionSetupResult
			{
				TransactionId = null,
				OwnsTransaction = false
			};
		}
		if (connectionInfo.Transaction != null)
		{
			warnings.Add("Reusing existing transaction - batch operations will NOT commit or rollback. You must explicitly commit or rollback the transaction when ready.");
			return new TransactionSetupResult
			{
				TransactionId = connectionInfo.Transaction.TransactionId,
				OwnsTransaction = false
			};
		}
		try
		{
			TransactionBeginResult transactionBeginResult = TransactionOperations.BeginTransactionInternal(connectionInfo);
			return new TransactionSetupResult
			{
				TransactionId = transactionBeginResult.TransactionId,
				OwnsTransaction = true
			};
		}
		catch (Exception ex)
		{
			warnings.Add("Failed to begin transaction: " + ex.Message + ". Operations will proceed without transaction protection.");
			return new TransactionSetupResult
			{
				TransactionId = null,
				OwnsTransaction = false
			};
		}
	}

	public static void ApplyTransactionFailureFixup(IList<ItemResult> results, ref int successCount, ref int failureCount)
	{
		if (results == null)
		{
			throw new ArgumentNullException("results");
		}
		foreach (ItemResult result in results)
		{
			if (result != null && result.Success)
			{
				result.Success = false;
				result.Message = (string.IsNullOrEmpty(result.Message) ? "(rolled back)" : (result.Message + " (rolled back)"));
			}
		}
		successCount = 0;
		failureCount = results.Count;
	}

	public static void ApplyPerItemFailureRollback(IList<ItemResult> results, ref int successCount, ref int failureCount, List<string> warnings)
	{
		if (warnings == null)
		{
			throw new ArgumentNullException("warnings");
		}
		warnings.Add($"Transaction rolled back due to {failureCount} per-item failure(s). See Results[] for the cause.");
		ApplyTransactionFailureFixup(results, ref successCount, ref failureCount);
	}

	public static bool FinalizeBatchTransaction(IConnectionInfo connectionInfo, BatchOperationResponse response, string? transactionId, bool ownsTransaction, int totalCount, ref int successCount, ref int failureCount, string verb, string itemNoun)
	{
		List<string> list = response.Warnings ?? throw new ArgumentException("response.Warnings must be initialized", "response");
		bool wasRolledBack = false;
		bool flag = false;
		bool flag2 = false;
		if (transactionId != null)
		{
			if (failureCount == 0)
			{
				if (ownsTransaction)
				{
					try
					{
						TransactionOperations.CommitTransactionInternal(connectionInfo);
						list.Add($"Transaction committed. {verb} {successCount} of {totalCount} {itemNoun}.");
					}
					catch (Exception ex)
					{
						flag2 = !ExceptionHelper.HandleCommitFailure(ex, list, response.Exceptions);
						ApplyTransactionFailureFixup(response.Results, ref successCount, ref failureCount);
					}
				}
				else
				{
					list.Add($"{verb} {successCount} of {totalCount} {itemNoun} in existing transaction. Transaction remains open for explicit commit.");
				}
			}
			else if (ownsTransaction)
			{
				try
				{
					TransactionOperations.RollbackTransactionInternal(connectionInfo);
					ApplyPerItemFailureRollback(response.Results, ref successCount, ref failureCount, list);
					wasRolledBack = true;
				}
				catch (Exception ex2)
				{
					flag = true;
					list.Add("Failed to rollback transaction: " + ex2.Message + ". Transaction state is unknown; you may need to retry rollback or reconnect.");
				}
			}
			else
			{
				list.Add("Batch operation failed in existing transaction. Transaction remains open - you should rollback.");
			}
		}
		response.Success = !flag2 && !flag && failureCount == 0;
		response.Message = (flag ? $"Transaction rollback failed. {verb} {successCount} of {totalCount} {itemNoun}. {failureCount} failed." : GetBatchOperationMessage(flag2, wasRolledBack, successCount, failureCount, totalCount, verb, itemNoun));
		return flag2;
	}

	public static string GetBatchOperationMessage(bool transactionFailed, bool wasRolledBack, int successCount, int failureCount, int totalCount, string verb, string itemNoun)
	{
		if (!transactionFailed)
		{
			if (!wasRolledBack)
			{
				string value = (string.IsNullOrEmpty(verb) ? verb : (char.ToLowerInvariant(verb[0]) + verb.Substring(1)));
				if (failureCount != 0)
				{
					return $"{verb} {successCount} of {totalCount} {itemNoun}. {failureCount} failed.";
				}
				return $"Successfully {value} {successCount} {itemNoun}";
			}
			return $"{verb} 0 of {totalCount} {itemNoun}.";
		}
		return $"Transaction commit failed. {totalCount} {itemNoun} were prepared in memory but no changes were saved to the server.";
	}
}
