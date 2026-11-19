using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PowerBIModelingMCP.Library.Common.DataStructures;
using PowerBIModelingMCP.Library.Contracts;

namespace PowerBIModelingMCP.Library.Common;

public static class PostCommitDaxValidator
{
	public readonly record struct Check(string PropertyLabel, string State, string? ErrorMessage);

	public readonly record struct Target(string EntityLabel, string DisplayName, IReadOnlyList<Check> Checks);

	private static readonly HashSet<string> ValidStates = new HashSet<string>(StringComparer.Ordinal) { "Ready", "NoData", "CalculationNeeded" };

	private const string RefreshNeededState = "CalculationNeeded";

	private static readonly Regex QuotedNameToBrackets = new Regex("'((?:[^']|'')*)'", RegexOptions.Compiled);

	public static void Append<TItem>(IConnectionInfo conn, List<string> warnings, List<ItemResult> results, List<TItem> items, string? transactionId, bool ownsTransaction, bool transactionFailed, int failureCount, string verb, Func<TItem, Target?> resolve)
	{
		if (conn == null)
		{
			throw new ArgumentNullException("conn");
		}
		if (warnings == null)
		{
			throw new ArgumentNullException("warnings");
		}
		if (results == null)
		{
			throw new ArgumentNullException("results");
		}
		if (items == null)
		{
			throw new ArgumentNullException("items");
		}
		if (resolve == null)
		{
			throw new ArgumentNullException("resolve");
		}
		if (transactionId != null && !ownsTransaction)
		{
			int num = results.Count((ItemResult r) => r?.Success ?? false);
			if (num > 0)
			{
				warnings.Add($"DAX validation deferred: {num} item(s) {verb} inside an existing caller-owned transaction. " + "Server state (Ready / SemanticError / SyntaxError) is only computed on transaction commit. Re-read each item after committing to confirm its state and errorMessage.");
			}
		}
		else
		{
			if (transactionId != null && ownsTransaction && (failureCount > 0 || transactionFailed))
			{
				return;
			}
			try
			{
				if (conn.IsOffline)
				{
					return;
				}
			}
			catch
			{
			}
			List<string> list = new List<string>();
			for (int num2 = 0; num2 < results.Count && num2 < items.Count; num2++)
			{
				ItemResult itemResult = results[num2];
				if (itemResult == null || !itemResult.Success)
				{
					continue;
				}
				Target? target;
				try
				{
					target = resolve(items[num2]);
				}
				catch
				{
					continue;
				}
				if (!target.HasValue)
				{
					continue;
				}
				Target value = target.Value;
				if (value.Checks == null)
				{
					continue;
				}
				bool flag = false;
				foreach (Check check in value.Checks)
				{
					if (!string.IsNullOrEmpty(check.State))
					{
						if (string.Equals(check.State, "CalculationNeeded", StringComparison.Ordinal))
						{
							flag = true;
						}
						else if (!ValidStates.Contains(check.State))
						{
							string value2 = (string.IsNullOrWhiteSpace(check.ErrorMessage) ? "(no detail reported by server)" : check.ErrorMessage);
							string value3 = (string.IsNullOrEmpty(check.PropertyLabel) ? "" : (" [" + check.PropertyLabel + "]"));
							warnings.Add($"{value.EntityLabel} {value.DisplayName}{value3} persisted but has state={check.State}: {value2}. " + "The expression will not evaluate correctly until fixed.");
						}
					}
				}
				if (flag)
				{
					string text = QuotedNameToBrackets.Replace(value.DisplayName, (Match m) => "[" + m.Groups[1].Value.Replace("''", "'") + "]");
					list.Add(value.EntityLabel + " " + text);
				}
			}
			if (list.Count > 0)
			{
				warnings.Add($"Refresh required: {list.Count} item(s) {verb} but values have not been computed yet ({string.Join("; ", list)}). " + "Important: This must be followed with a model refresh operation call.");
			}
		}
	}
}
