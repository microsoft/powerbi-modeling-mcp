using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using PowerBIModelingMCP.Library.Common.DataStructures;
using PowerBIModelingMCP.Library.Contracts;
using PowerBIModelingMCP.Library.Core;

namespace PowerBIModelingMCP.Library.Common;

public static class BatchExecutor
{
	public delegate void ProcessItemDelegate<TItem>(BatchItemContext<TItem> ctx);

	public delegate void PostCommitDelegate<TItem>(IConnectionInfo connection, List<TItem> items, BatchOperationResponse response, List<string> warnings, string? transactionId, bool ownsTransaction, bool transactionFailed, int failureCount);

	public static async Task<BatchOperationResponse> ExecuteAsync<TItem>(string? connectionName, List<TItem>? items, BatchOptions options, string operation, string verb, string itemNoun, Func<TItem, string?> getItemIdentifier, ProcessItemDelegate<TItem> processItem, PostCommitDelegate<TItem>? postCommit = null)
	{
		ArgumentNullException.ThrowIfNull(options, "options");
		ArgumentNullException.ThrowIfNull(getItemIdentifier, "getItemIdentifier");
		ArgumentNullException.ThrowIfNull(processItem, "processItem");
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = operation,
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (items == null || items.Count == 0)
		{
			response.Success = false;
			response.Message = "No " + itemNoun + " provided for " + operation.ToLowerInvariant();
			response.Summary = new BatchSummary
			{
				TotalItems = 0,
				SuccessCount = 0,
				FailureCount = 0,
				ExecutionTime = stopwatch.Elapsed
			};
			return response;
		}
		int successCount = 0;
		int failureCount = 0;
		int totalCount = items.Count;
		BatchOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			TransactionSetupResult transactionSetupResult = BatchTransactionHelper.HandleTransactionSetup(connectionInfo, options.UseTransaction, connectionName, warnings);
			string transactionId = transactionSetupResult.TransactionId;
			bool ownsTransaction = transactionSetupResult.OwnsTransaction;
			try
			{
				for (int i = 0; i < items.Count; i++)
				{
					TItem val = items[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = (getItemIdentifier(val) ?? string.Empty)
					};
					try
					{
						processItem(new BatchItemContext<TItem>
						{
							Connection = connectionInfo,
							Item = val,
							Index = i,
							Result = itemResult,
							Warnings = warnings,
							TransactionId = transactionId
						});
						if (itemResult.Success)
						{
							successCount++;
						}
						else
						{
							failureCount++;
						}
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						if (string.IsNullOrEmpty(itemResult.Message))
						{
							itemResult.Message = $"Error {PresentParticipleOf(verb)} {Singularize(itemNoun)} '{itemResult.ItemIdentifier}': {ex.Message}";
						}
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				bool transactionFailed = BatchTransactionHelper.FinalizeBatchTransaction(connectionInfo, response, transactionId, ownsTransaction, totalCount, ref successCount, ref failureCount, verb, itemNoun);
				try
				{
					postCommit?.Invoke(connectionInfo, items, response, warnings, transactionId, ownsTransaction, transactionFailed, failureCount);
				}
				catch (Exception ex2)
				{
					response.Success = false;
					response.Message = operation + " operation failed during post-commit step: " + ex2.Message;
					response.Exceptions.Add(ex2);
				}
			}
			catch (Exception ex3)
			{
				if (transactionId != null && ownsTransaction)
				{
					try
					{
						TransactionOperations.RollbackTransactionInternal(connectionInfo);
						BatchTransactionHelper.ApplyTransactionFailureFixup(response.Results, ref successCount, ref failureCount);
					}
					catch
					{
					}
				}
				response.Success = false;
				response.Message = operation + " operation failed: " + ex3.Message;
				response.Exceptions.Add(ex3);
			}
			finally
			{
				stopwatch.Stop();
			}
			response.Summary = new BatchSummary
			{
				TotalItems = totalCount,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	internal static string PresentParticipleOf(string pastVerb)
	{
		if (string.IsNullOrEmpty(pastVerb))
		{
			return pastVerb;
		}
		string text = pastVerb.ToLowerInvariant();
		if (!text.EndsWith("ed"))
		{
			return text + "ing";
		}
		string text2 = text;
		return text2.Substring(0, text2.Length - 2) + "ing";
	}

	internal static string Singularize(string plural)
	{
		if (string.IsNullOrEmpty(plural))
		{
			return plural;
		}
		if (plural.EndsWith("ies", StringComparison.Ordinal))
		{
			string text = plural;
			return text.Substring(0, text.Length - 3) + "y";
		}
		if (plural.EndsWith('s'))
		{
			string text = plural;
			return text.Substring(0, text.Length - 1);
		}
		return plural;
	}
}
