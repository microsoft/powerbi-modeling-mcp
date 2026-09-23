using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AnalysisServices.Tabular;
using PowerBIModelingMCP.Library.Common;
using PowerBIModelingMCP.Library.Common.DataStructures;
using PowerBIModelingMCP.Library.Contracts;

namespace PowerBIModelingMCP.Library.Core;

public static class CalculationGroupOperations
{
	internal static PostCommitDaxValidator.Target? ResolveCalculationGroupForValidation(IConnectionInfo conn, CalculationGroupDefinition def)
	{
		if (def == null || string.IsNullOrEmpty(def.Name))
		{
			return null;
		}
		Database database = conn?.Database;
		if (database == null)
		{
			return null;
		}
		Table table = database.Model.Tables.Find(def.Name);
		CalculationGroup calculationGroup = table?.CalculationGroup;
		if (calculationGroup == null)
		{
			return null;
		}
		List<PostCommitDaxValidator.Check> list = new List<PostCommitDaxValidator.Check>();
		if (calculationGroup.NoSelectionExpression != null)
		{
			list.Add(new PostCommitDaxValidator.Check("noSelectionExpression", calculationGroup.NoSelectionExpression.State.ToString(), calculationGroup.NoSelectionExpression.ErrorMessage));
			if (calculationGroup.NoSelectionExpression.FormatStringDefinition != null)
			{
				list.Add(new PostCommitDaxValidator.Check("noSelectionExpression.formatStringDefinition", calculationGroup.NoSelectionExpression.FormatStringDefinition.State.ToString(), calculationGroup.NoSelectionExpression.FormatStringDefinition.ErrorMessage));
			}
		}
		if (calculationGroup.MultipleOrEmptySelectionExpression != null)
		{
			list.Add(new PostCommitDaxValidator.Check("multipleOrEmptySelectionExpression", calculationGroup.MultipleOrEmptySelectionExpression.State.ToString(), calculationGroup.MultipleOrEmptySelectionExpression.ErrorMessage));
			if (calculationGroup.MultipleOrEmptySelectionExpression.FormatStringDefinition != null)
			{
				list.Add(new PostCommitDaxValidator.Check("multipleOrEmptySelectionExpression.formatStringDefinition", calculationGroup.MultipleOrEmptySelectionExpression.FormatStringDefinition.State.ToString(), calculationGroup.MultipleOrEmptySelectionExpression.FormatStringDefinition.ErrorMessage));
			}
		}
		foreach (CalculationItem calculationItem in calculationGroup.CalculationItems)
		{
			list.Add(new PostCommitDaxValidator.Check("calculationItems['" + calculationItem.Name + "']", calculationItem.State.ToString(), calculationItem.ErrorMessage));
			if (calculationItem.FormatStringDefinition != null)
			{
				list.Add(new PostCommitDaxValidator.Check("calculationItems['" + calculationItem.Name + "'].formatStringDefinition", calculationItem.FormatStringDefinition.State.ToString(), calculationItem.FormatStringDefinition.ErrorMessage));
			}
		}
		if (list.Count == 0)
		{
			return null;
		}
		return new PostCommitDaxValidator.Target("Calculation group", "'" + table.Name + "'", list);
	}

	internal static PostCommitDaxValidator.Target? ResolveCalculationItemForValidation(IConnectionInfo conn, CalculationItemDefinition def)
	{
		if (def == null || string.IsNullOrEmpty(def.Name) || string.IsNullOrEmpty(def.CalculationGroupName))
		{
			return null;
		}
		Database database = conn?.Database;
		if (database == null)
		{
			return null;
		}
		CalculationItem calculationItem = (database.Model.Tables.Find(def.CalculationGroupName)?.CalculationGroup)?.CalculationItems.Find(def.Name);
		if (calculationItem == null)
		{
			return null;
		}
		List<PostCommitDaxValidator.Check> list = new List<PostCommitDaxValidator.Check>
		{
			new PostCommitDaxValidator.Check(string.Empty, calculationItem.State.ToString(), calculationItem.ErrorMessage)
		};
		if (calculationItem.FormatStringDefinition != null)
		{
			list.Add(new PostCommitDaxValidator.Check("formatStringDefinition", calculationItem.FormatStringDefinition.State.ToString(), calculationItem.FormatStringDefinition.ErrorMessage));
		}
		return new PostCommitDaxValidator.Target("Calculation item", $"'{calculationItem.Name}' in group '{def.CalculationGroupName}'", list);
	}

	public static void ValidateCalculationGroupBase(CalculationGroupBase def, bool isCreate)
	{
		if (def == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Calculation group definition cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(def.Name))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Name is required", ErrorSource.User);
		}
		if (def.Precedence.HasValue && def.Precedence.Value < 0)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Precedence must be non-negative", ErrorSource.User);
		}
		if (def.MultipleOrEmptySelectionExpression != null && isCreate && string.IsNullOrWhiteSpace(def.MultipleOrEmptySelectionExpression.Expression))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Expression is required for MultipleOrEmptySelectionExpression", ErrorSource.User);
		}
		if (def.NoSelectionExpression != null && isCreate && string.IsNullOrWhiteSpace(def.NoSelectionExpression.Expression))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Expression is required for NoSelectionExpression", ErrorSource.User);
		}
	}

	public static void ValidateCalculationItemBase(CalculationItemBase def, bool isCreate)
	{
		if (def == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Calculation item definition cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(def.Name))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Name is required", ErrorSource.User);
		}
		if (isCreate && string.IsNullOrWhiteSpace(def.Expression))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Expression is required for creation", ErrorSource.User);
		}
		if (def.Ordinal.HasValue && def.Ordinal.Value < 0)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Ordinal must be non-negative", ErrorSource.User);
		}
	}

	private static CalculationGroupExpressionInfo? ConvertToExpressionInfo(CalculationGroupExpression? expr)
	{
		if (expr == null)
		{
			return null;
		}
		return new CalculationGroupExpressionInfo
		{
			Expression = expr.Expression,
			Description = expr.Description,
			FormatStringExpression = expr.FormatStringDefinition?.Expression,
			State = expr.State.ToString(),
			ErrorMessage = expr.ErrorMessage,
			ModifiedTime = expr.ModifiedTime
		};
	}

	private static void UpdateCalculationGroupExpression(CalculationGroupExpression target, CalculationGroupExpressionInfo source)
	{
		if (!string.IsNullOrWhiteSpace(source.Expression))
		{
			target.Expression = source.Expression;
		}
		if (source.Description != null)
		{
			target.Description = source.Description;
		}
		if (source.FormatStringExpression == null)
		{
			return;
		}
		if (string.IsNullOrWhiteSpace(source.FormatStringExpression))
		{
			target.FormatStringDefinition = null;
			return;
		}
		if (target.FormatStringDefinition == null)
		{
			target.FormatStringDefinition = new FormatStringDefinition();
		}
		target.FormatStringDefinition.Expression = source.FormatStringExpression;
	}

	private static CalculationGroupExpression CreateCalculationGroupExpression(CalculationGroupExpressionInfo source)
	{
		CalculationGroupExpression calculationGroupExpression = new CalculationGroupExpression
		{
			Expression = source.Expression
		};
		if (!string.IsNullOrWhiteSpace(source.Description))
		{
			calculationGroupExpression.Description = source.Description;
		}
		if (!string.IsNullOrWhiteSpace(source.FormatStringExpression))
		{
			calculationGroupExpression.FormatStringDefinition = new FormatStringDefinition
			{
				Expression = source.FormatStringExpression
			};
		}
		return calculationGroupExpression;
	}

	private static CalculationGroup FindCalculationGroup(Model model, string calculationGroupName)
	{
		foreach (Table table in model.Tables)
		{
			if (table.CalculationGroup != null && table.Name == calculationGroupName)
			{
				return table.CalculationGroup;
			}
		}
		throw McpExceptionWithSource.FromTelemetrySafeMessage("Calculation group '" + calculationGroupName + "' not found in the model", ErrorSource.User);
	}

	private static Table GetCalculationGroupTable(Model model, string calculationGroupName)
	{
		foreach (Table table in model.Tables)
		{
			if (table.CalculationGroup != null && table.Name == calculationGroupName)
			{
				return table;
			}
		}
		throw McpExceptionWithSource.FromTelemetrySafeMessage("Table for calculation group '" + calculationGroupName + "' not found in the model", ErrorSource.User);
	}

	public static async Task<List<CalculationGroupList>> ListCalculationGroups(string? connectionName = null)
	{
		List<CalculationGroupList> result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				List<CalculationGroupList> list = ListCalculationGroupsInternal(connectionInfo.Database);
				AuditEvent.Default.Emit("list calculation groups", success: true, OperationType.Read, connectionInfo);
				result = list;
			}
			catch
			{
				AuditEvent.Default.Emit("list calculation groups", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	internal static List<CalculationGroupList> ListCalculationGroupsInternal(Database db)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database is null", ErrorSource.User);
		}
		List<CalculationGroupList> list = new List<CalculationGroupList>();
		foreach (Table table in db.Model.Tables)
		{
			if (table.CalculationGroup != null)
			{
				list.Add(new CalculationGroupList
				{
					Name = table.Name,
					Description = ((!string.IsNullOrEmpty(table.Description)) ? table.Description : null),
					IsHidden = (table.IsHidden ? new bool?(true) : ((bool?)null)),
					CalculationItems = (from item in table.CalculationGroup.CalculationItems
						orderby item.Ordinal
						select new CalculationItemList
						{
							Name = item.Name,
							Description = ((!string.IsNullOrEmpty(item.Description)) ? item.Description : null),
							Ordinal = item.Ordinal
						}).ToList()
				});
			}
		}
		return list;
	}

	internal static CalculationGroupGet GetCalculationGroupInternal(Database db, string calculationGroupName)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database is null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(calculationGroupName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("calculationGroupName is required", ErrorSource.User);
		}
		CalculationGroup calculationGroup = FindCalculationGroup(db.Model, calculationGroupName);
		Table calculationGroupTable = GetCalculationGroupTable(db.Model, calculationGroupName);
		CalculationGroupGet calculationGroupGet = new CalculationGroupGet
		{
			Name = calculationGroupTable.Name,
			Description = calculationGroup.Description,
			IsHidden = calculationGroupTable.IsHidden,
			Precedence = calculationGroup.Precedence,
			MultipleOrEmptySelectionExpression = ConvertToExpressionInfo(calculationGroup.MultipleOrEmptySelectionExpression),
			NoSelectionExpression = ConvertToExpressionInfo(calculationGroup.NoSelectionExpression),
			ModifiedTime = calculationGroupTable.ModifiedTime,
			StructureModifiedTime = calculationGroupTable.StructureModifiedTime
		};
		foreach (CalculationItem item2 in calculationGroup.CalculationItems.OrderBy((CalculationItem i) => i.Ordinal))
		{
			CalculationItemGet item = new CalculationItemGet
			{
				Name = item2.Name,
				Description = item2.Description,
				Expression = item2.Expression,
				Ordinal = item2.Ordinal,
				FormatStringExpression = item2.FormatStringDefinition?.Expression,
				FormatStringExpressionState = item2.FormatStringDefinition?.State.ToString(),
				FormatStringExpressionErrorMessage = item2.FormatStringDefinition?.ErrorMessage,
				State = item2.State.ToString(),
				ErrorMessage = item2.ErrorMessage,
				ModifiedTime = item2.ModifiedTime
			};
			calculationGroupGet.CalculationItems.Add(item);
		}
		calculationGroupGet.Annotations = new List<KeyValuePair<string, string>>();
		foreach (Annotation annotation in calculationGroup.Annotations)
		{
			calculationGroupGet.Annotations.Add(new KeyValuePair<string, string>(annotation.Name, annotation.Value));
		}
		return calculationGroupGet;
	}

	public static async Task<string> ExportTMDL(string? connectionName, string calculationGroupName, ExportTmdl options)
	{
		if (string.IsNullOrWhiteSpace(calculationGroupName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("calculationGroupName is required", ErrorSource.User);
		}
		string result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				string text = ExportTMDLInternal(connectionInfo.Database, calculationGroupName, options);
				AuditEvent.Default.Emit("export calculation group to TMDL", success: true, OperationType.Read, connectionInfo);
				result = text;
			}
			catch
			{
				AuditEvent.Default.Emit("export calculation group to TMDL", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	internal static string ExportTMDLInternal(Database db, string calculationGroupName, ExportTmdl options)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database is null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(calculationGroupName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("calculationGroupName is required", ErrorSource.User);
		}
		return ExportContentProcessor.ProcessExportContent(TmdlSerializer.SerializeObject(FindCalculationGroup(db.Model, calculationGroupName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Calculation group '" + calculationGroupName + "' not found", ErrorSource.User), options.SerializationOptions.ToMetadataSerializationOptions()), options).Content;
	}

	internal static CalculationGroupOperationResult CreateCalculationGroupInternal(IConnectionInfo info, CalculationGroupDefinition def)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo is null", ErrorSource.User);
		}
		ValidateCalculationGroupBase(def, isCreate: true);
		Database database = info.Database;
		if (!database.Model.DiscourageImplicitMeasures)
		{
			database.Model.DiscourageImplicitMeasures = true;
			TransactionOperations.RecordOperation(info, "Set Model.DiscourageImplicitMeasures to true (required for calculation groups)");
		}
		foreach (Table table2 in database.Model.Tables)
		{
			if (table2.CalculationGroup != null && table2.Name == def.Name)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Calculation group '" + def.Name + "' already exists", ErrorSource.User);
			}
		}
		if (def.CalculationItems != null && def.CalculationItems.Count > 0)
		{
			bool num = def.CalculationItems.Any((CalculationItemDefinition item) => item.Ordinal.HasValue);
			bool flag = def.CalculationItems.All((CalculationItemDefinition item) => item.Ordinal.HasValue);
			if (num && !flag)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Either all calculation items must have ordinals specified or none. Mixed ordinals are not allowed.", ErrorSource.User);
			}
			foreach (CalculationItemDefinition calculationItem2 in def.CalculationItems)
			{
				ValidateCalculationItemBase(calculationItem2, isCreate: true);
			}
			if (flag)
			{
				HashSet<int> hashSet = new HashSet<int>();
				foreach (CalculationItemDefinition calculationItem3 in def.CalculationItems)
				{
					if (!hashSet.Add(calculationItem3.Ordinal.Value))
					{
						throw McpExceptionWithSource.FromTelemetrySafeMessage($"Duplicate ordinal {calculationItem3.Ordinal.Value} found", ErrorSource.User);
					}
				}
				List<int> list = hashSet.OrderBy((int o) => o).ToList();
				for (int num2 = 0; num2 < list.Count; num2++)
				{
					if (list[num2] != num2)
					{
						throw McpExceptionWithSource.FromTelemetrySafeMessage($"Calculation item ordinals must be continuous starting from 0. Missing ordinal {num2}", ErrorSource.User);
					}
				}
			}
			HashSet<string> hashSet2 = new HashSet<string>();
			foreach (CalculationItemDefinition calculationItem4 in def.CalculationItems)
			{
				if (!hashSet2.Add(calculationItem4.Name))
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage("Duplicate calculation item name '" + calculationItem4.Name + "' found", ErrorSource.User);
				}
			}
		}
		CalculationGroup calculationGroup = new CalculationGroup();
		if (!string.IsNullOrWhiteSpace(def.Description))
		{
			calculationGroup.Description = def.Description;
		}
		if (def.Precedence.HasValue)
		{
			calculationGroup.Precedence = def.Precedence.Value;
		}
		if (def.MultipleOrEmptySelectionExpression != null)
		{
			calculationGroup.MultipleOrEmptySelectionExpression = CreateCalculationGroupExpression(def.MultipleOrEmptySelectionExpression);
		}
		if (def.NoSelectionExpression != null)
		{
			calculationGroup.NoSelectionExpression = CreateCalculationGroupExpression(def.NoSelectionExpression);
		}
		if (def.Annotations != null)
		{
			AnnotationHelpers.ApplyAnnotations(calculationGroup, def.Annotations, (CalculationGroup cg) => cg.Annotations);
		}
		if (def.CalculationItems != null && def.CalculationItems.Count > 0)
		{
			List<CalculationItemDefinition> list2 = def.CalculationItems.ToList();
			if (list2.All((CalculationItemDefinition item) => !item.Ordinal.HasValue))
			{
				for (int num3 = 0; num3 < list2.Count; num3++)
				{
					list2[num3].Ordinal = num3;
				}
			}
			foreach (CalculationItemDefinition item in list2.OrderBy((CalculationItemDefinition item) => item.Ordinal))
			{
				CalculationItem calculationItem = new CalculationItem
				{
					Name = item.Name,
					Expression = item.Expression,
					Ordinal = item.Ordinal.Value
				};
				if (!string.IsNullOrWhiteSpace(item.Description))
				{
					calculationItem.Description = item.Description;
				}
				if (!string.IsNullOrWhiteSpace(item.FormatStringExpression))
				{
					calculationItem.FormatStringDefinition = new FormatStringDefinition
					{
						Expression = item.FormatStringExpression
					};
				}
				calculationGroup.CalculationItems.Add(calculationItem);
			}
		}
		Table table = new Table
		{
			Name = def.Name
		};
		if (def.IsHidden.HasValue)
		{
			table.IsHidden = def.IsHidden.Value;
		}
		DataColumn metadataObject = new DataColumn
		{
			Name = def.Name,
			DataType = DataType.String
		};
		table.Columns.Add(metadataObject);
		table.CalculationGroup = calculationGroup;
		Partition partition = new Partition
		{
			Name = "Partition_" + def.Name
		};
		partition.Source = new CalculationGroupSource();
		table.Partitions.Add(partition);
		database.Model.Tables.Add(table);
		int value = def.CalculationItems?.Count ?? 0;
		TransactionOperations.RecordOperation(info, $"Created calculation group '{def.Name}' with {value} calculation items");
		ConnectionOperations.SaveChangesWithRollback(info, "create calculation group", OperationType.Create);
		CalculationGroupOperationResult calculationGroupOperationResult = new CalculationGroupOperationResult
		{
			CalculationGroupName = def.Name,
			CalculationItemCount = calculationGroup.CalculationItems.Count
		};
		foreach (CalculationItem item2 in calculationGroup.CalculationItems.OrderBy((CalculationItem i) => i.Ordinal))
		{
			calculationGroupOperationResult.CalculationItems.Add(new CalculationItemOperationResult
			{
				State = item2.State.ToString(),
				ErrorMessage = item2.ErrorMessage,
				CalculationItemName = item2.Name,
				CalculationGroupName = def.Name,
				Ordinal = item2.Ordinal
			});
		}
		return calculationGroupOperationResult;
	}

	internal static CalculationGroupOperationResult UpdateCalculationGroupInternal(IConnectionInfo info, CalculationGroupDefinition update)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo is null", ErrorSource.User);
		}
		ValidateCalculationGroupBase(update, isCreate: false);
		Database database = info.Database;
		CalculationGroup calculationGroup = FindCalculationGroup(database.Model, update.Name);
		Table calculationGroupTable = GetCalculationGroupTable(database.Model, update.Name);
		bool flag = false;
		if (update.Description != null && calculationGroup.Description != update.Description)
		{
			calculationGroup.Description = update.Description;
			flag = true;
		}
		if (update.IsHidden.HasValue && calculationGroupTable.IsHidden != update.IsHidden.Value)
		{
			calculationGroupTable.IsHidden = update.IsHidden.Value;
			flag = true;
		}
		if (update.Precedence.HasValue && calculationGroup.Precedence != update.Precedence.Value)
		{
			calculationGroup.Precedence = update.Precedence.Value;
			flag = true;
		}
		if (update.MultipleOrEmptySelectionExpression != null)
		{
			if (calculationGroup.MultipleOrEmptySelectionExpression == null)
			{
				calculationGroup.MultipleOrEmptySelectionExpression = CreateCalculationGroupExpression(update.MultipleOrEmptySelectionExpression);
				flag = true;
			}
			else
			{
				string obj = calculationGroup.MultipleOrEmptySelectionExpression.Expression ?? "";
				string text = calculationGroup.MultipleOrEmptySelectionExpression.Description ?? "";
				string text2 = calculationGroup.MultipleOrEmptySelectionExpression.FormatStringDefinition?.Expression ?? "";
				string text3 = update.MultipleOrEmptySelectionExpression.Expression ?? "";
				string text4 = update.MultipleOrEmptySelectionExpression.Description ?? "";
				string text5 = update.MultipleOrEmptySelectionExpression.FormatStringExpression ?? "";
				if (obj != text3 || text != text4 || text2 != text5)
				{
					UpdateCalculationGroupExpression(calculationGroup.MultipleOrEmptySelectionExpression, update.MultipleOrEmptySelectionExpression);
					flag = true;
				}
			}
		}
		if (update.NoSelectionExpression != null)
		{
			if (calculationGroup.NoSelectionExpression == null)
			{
				calculationGroup.NoSelectionExpression = CreateCalculationGroupExpression(update.NoSelectionExpression);
				flag = true;
			}
			else
			{
				string obj2 = calculationGroup.NoSelectionExpression.Expression ?? "";
				string text6 = calculationGroup.NoSelectionExpression.Description ?? "";
				string text7 = calculationGroup.NoSelectionExpression.FormatStringDefinition?.Expression ?? "";
				string text8 = update.NoSelectionExpression.Expression ?? "";
				string text9 = update.NoSelectionExpression.Description ?? "";
				string text10 = update.NoSelectionExpression.FormatStringExpression ?? "";
				if (obj2 != text8 || text6 != text9 || text7 != text10)
				{
					UpdateCalculationGroupExpression(calculationGroup.NoSelectionExpression, update.NoSelectionExpression);
					flag = true;
				}
			}
		}
		if (update.Annotations != null && AnnotationHelpers.ReplaceAnnotations(calculationGroup, update.Annotations, (CalculationGroup cg) => cg.Annotations))
		{
			flag = true;
		}
		if (!flag)
		{
			CalculationGroupOperationResult calculationGroupOperationResult = new CalculationGroupOperationResult
			{
				CalculationGroupName = update.Name,
				CalculationItemCount = calculationGroup.CalculationItems.Count,
				HasChanges = false
			};
			{
				foreach (CalculationItem item in calculationGroup.CalculationItems.OrderBy((CalculationItem i) => i.Ordinal))
				{
					calculationGroupOperationResult.CalculationItems.Add(new CalculationItemOperationResult
					{
						State = item.State.ToString(),
						ErrorMessage = item.ErrorMessage,
						CalculationItemName = item.Name,
						CalculationGroupName = update.Name,
						Ordinal = item.Ordinal,
						HasChanges = false
					});
				}
				return calculationGroupOperationResult;
			}
		}
		TransactionOperations.RecordOperation(info, "Updated calculation group '" + update.Name + "'");
		ConnectionOperations.SaveChangesWithRollback(info, "update calculation group", OperationType.Update);
		CalculationGroupOperationResult calculationGroupOperationResult2 = new CalculationGroupOperationResult
		{
			CalculationGroupName = update.Name,
			CalculationItemCount = calculationGroup.CalculationItems.Count,
			HasChanges = true
		};
		foreach (CalculationItem item2 in calculationGroup.CalculationItems.OrderBy((CalculationItem i) => i.Ordinal))
		{
			calculationGroupOperationResult2.CalculationItems.Add(new CalculationItemOperationResult
			{
				State = item2.State.ToString(),
				ErrorMessage = item2.ErrorMessage,
				CalculationItemName = item2.Name,
				CalculationGroupName = update.Name,
				Ordinal = item2.Ordinal,
				HasChanges = true
			});
		}
		return calculationGroupOperationResult2;
	}

	internal static void RenameCalculationGroupInternal(IConnectionInfo info, string oldName, string newName)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo is null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(oldName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("oldName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(newName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("newName is required", ErrorSource.User);
		}
		Database database = info.Database;
		Table calculationGroupTable = GetCalculationGroupTable(database.Model, oldName);
		foreach (Table table in database.Model.Tables)
		{
			if (table.CalculationGroup != null && table.Name == newName && !string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Calculation group '" + newName + "' already exists", ErrorSource.User);
			}
		}
		calculationGroupTable.RequestRename(newName);
		TransactionOperations.RecordOperation(info, $"Renamed calculation group '{oldName}' to '{newName}'");
		ConnectionOperations.SaveChangesWithRollback(info, "rename calculation group", OperationType.Update, CheckpointMode.AfterRequestRename);
	}

	internal static void DeleteCalculationGroupInternal(IConnectionInfo info, string calculationGroupName)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo is null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(calculationGroupName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("calculationGroupName is required", ErrorSource.User);
		}
		Database database = info.Database;
		Table calculationGroupTable = GetCalculationGroupTable(database.Model, calculationGroupName);
		List<string> list = new List<string>();
		foreach (Table table in database.Model.Tables)
		{
			foreach (Measure measure in table.Measures)
			{
				if (!string.IsNullOrWhiteSpace(measure.Expression) && measure.Expression.Contains("'" + calculationGroupName + "'"))
				{
					list.Add("[" + measure.Name + "]");
				}
			}
		}
		if (list.Count > 0)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Cannot delete calculation group '" + calculationGroupName + "' because it is referenced by: " + string.Join(", ", list), ErrorSource.User);
		}
		database.Model.Tables.Remove(calculationGroupTable);
		TransactionOperations.RecordOperation(info, "Deleted calculation group '" + calculationGroupName + "'");
		ConnectionOperations.SaveChangesWithRollback(info, "delete calculation group", OperationType.Delete);
	}

	public static async Task<List<CalculationItemList>> ListCalculationItems(string? connectionName, string calculationGroupName)
	{
		if (string.IsNullOrWhiteSpace(calculationGroupName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("calculationGroupName is required", ErrorSource.User);
		}
		List<CalculationItemList> result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				List<CalculationItemList> list = ListCalculationItemsInternal(connectionInfo.Database, calculationGroupName);
				AuditEvent.Default.Emit("list calculation items", success: true, OperationType.Read, connectionInfo);
				result = list;
			}
			catch
			{
				AuditEvent.Default.Emit("list calculation items", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	internal static List<CalculationItemList> ListCalculationItemsInternal(Database db, string calculationGroupName)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database is null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(calculationGroupName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("calculationGroupName is required", ErrorSource.User);
		}
		return (from item in FindCalculationGroup(db.Model, calculationGroupName).CalculationItems
			orderby item.Ordinal
			select new CalculationItemList
			{
				Name = item.Name,
				Description = ((!string.IsNullOrEmpty(item.Description)) ? item.Description : null),
				Ordinal = item.Ordinal
			}).ToList();
	}

	internal static CalculationItemGet GetCalculationItemInternal(Database db, string calculationGroupName, string calculationItemName)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database is null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(calculationGroupName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("calculationGroupName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(calculationItemName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("calculationItemName is required", ErrorSource.User);
		}
		CalculationItem calculationItem = FindCalculationGroup(db.Model, calculationGroupName).CalculationItems.Find(calculationItemName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Calculation item '{calculationItemName}' not found in calculation group '{calculationGroupName}'", ErrorSource.User);
		return new CalculationItemGet
		{
			Name = calculationItem.Name,
			Description = calculationItem.Description,
			Expression = calculationItem.Expression,
			Ordinal = calculationItem.Ordinal,
			FormatStringExpression = calculationItem.FormatStringDefinition?.Expression,
			FormatStringExpressionState = calculationItem.FormatStringDefinition?.State.ToString(),
			FormatStringExpressionErrorMessage = calculationItem.FormatStringDefinition?.ErrorMessage,
			State = calculationItem.State.ToString(),
			ErrorMessage = calculationItem.ErrorMessage,
			ModifiedTime = calculationItem.ModifiedTime
		};
	}

	internal static CalculationItemOperationResult CreateCalculationItemInternal(IConnectionInfo info, string calculationGroupName, CalculationItemDefinition def)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo is null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(calculationGroupName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("calculationGroupName is required", ErrorSource.User);
		}
		ValidateCalculationItemBase(def, isCreate: true);
		CalculationGroup calculationGroup = FindCalculationGroup(info.Database.Model, calculationGroupName);
		if (calculationGroup.CalculationItems.Contains(def.Name))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Calculation item '{def.Name}' already exists in calculation group '{calculationGroupName}'", ErrorSource.User);
		}
		int ordinal;
		if (def.Ordinal.HasValue)
		{
			ordinal = def.Ordinal.Value;
			if (calculationGroup.CalculationItems.Any((CalculationItem item) => item.Ordinal == ordinal))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage($"Calculation item with ordinal {ordinal} already exists in calculation group '{calculationGroupName}'", ErrorSource.User);
			}
		}
		else
		{
			ordinal = ((calculationGroup.CalculationItems.Count > 0) ? (calculationGroup.CalculationItems.Max((CalculationItem item) => item.Ordinal) + 1) : 0);
		}
		CalculationItem calculationItem = new CalculationItem
		{
			Name = def.Name,
			Expression = def.Expression,
			Ordinal = ordinal
		};
		if (!string.IsNullOrWhiteSpace(def.Description))
		{
			calculationItem.Description = def.Description;
		}
		if (!string.IsNullOrWhiteSpace(def.FormatStringExpression))
		{
			calculationItem.FormatStringDefinition = new FormatStringDefinition
			{
				Expression = def.FormatStringExpression
			};
		}
		calculationGroup.CalculationItems.Add(calculationItem);
		TransactionOperations.RecordOperation(info, $"Created calculation item '{def.Name}' in calculation group '{calculationGroupName}'");
		ConnectionOperations.SaveChangesWithRollback(info, "create calculation item", OperationType.Create);
		return new CalculationItemOperationResult
		{
			State = calculationItem.State.ToString(),
			ErrorMessage = calculationItem.ErrorMessage,
			CalculationItemName = calculationItem.Name,
			CalculationGroupName = calculationGroupName,
			Ordinal = calculationItem.Ordinal
		};
	}

	internal static CalculationItemOperationResult UpdateCalculationItemInternal(IConnectionInfo info, string calculationGroupName, string calculationItemName, CalculationItemDefinition update)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo is null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(calculationGroupName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("calculationGroupName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(calculationItemName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("calculationItemName is required", ErrorSource.User);
		}
		ValidateCalculationItemBase(update, isCreate: false);
		CalculationGroup calculationGroup = FindCalculationGroup(info.Database.Model, calculationGroupName);
		CalculationItem calculationItem = calculationGroup.CalculationItems.Find(calculationItemName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Calculation item '{calculationItemName}' not found in calculation group '{calculationGroupName}'", ErrorSource.User);
		bool flag = false;
		if (update.Name != null && calculationItem.Name != update.Name)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Cannot change calculation item name from '{calculationItem.Name}' to '{update.Name}'. Use the rename operation to change calculation item names.", ErrorSource.User);
		}
		if (update.Description != null)
		{
			string text = (string.IsNullOrEmpty(update.Description) ? null : update.Description);
			if (calculationItem.Description != text)
			{
				calculationItem.Description = text;
				flag = true;
			}
		}
		if (update.Expression != null)
		{
			if (string.IsNullOrEmpty(update.Expression))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Expression cannot be empty", ErrorSource.User);
			}
			if (calculationItem.Expression != update.Expression)
			{
				calculationItem.Expression = update.Expression;
				flag = true;
			}
		}
		if (update.Ordinal.HasValue && calculationItem.Ordinal != update.Ordinal.Value)
		{
			if (calculationGroup.CalculationItems.Any((CalculationItem item) => item != calculationItem && item.Ordinal == update.Ordinal.Value))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage($"Calculation item with ordinal {update.Ordinal.Value} already exists in calculation group '{calculationGroupName}'", ErrorSource.User);
			}
			calculationItem.Ordinal = update.Ordinal.Value;
			flag = true;
		}
		if (update.FormatStringExpression != null)
		{
			if (string.IsNullOrEmpty(update.FormatStringExpression))
			{
				if (calculationItem.FormatStringDefinition != null)
				{
					calculationItem.FormatStringDefinition = null;
					flag = true;
				}
			}
			else
			{
				if (calculationItem.FormatStringDefinition == null)
				{
					calculationItem.FormatStringDefinition = new FormatStringDefinition();
				}
				if (calculationItem.FormatStringDefinition.Expression != update.FormatStringExpression)
				{
					calculationItem.FormatStringDefinition.Expression = update.FormatStringExpression;
					flag = true;
				}
			}
		}
		if (!flag)
		{
			return new CalculationItemOperationResult
			{
				State = calculationItem.State.ToString(),
				ErrorMessage = calculationItem.ErrorMessage,
				CalculationItemName = calculationItem.Name,
				CalculationGroupName = calculationGroupName,
				Ordinal = calculationItem.Ordinal,
				HasChanges = false
			};
		}
		TransactionOperations.RecordOperation(info, $"Updated calculation item '{calculationItemName}' in calculation group '{calculationGroupName}'");
		ConnectionOperations.SaveChangesWithRollback(info, "update calculation item", OperationType.Update);
		return new CalculationItemOperationResult
		{
			State = calculationItem.State.ToString(),
			ErrorMessage = calculationItem.ErrorMessage,
			CalculationItemName = calculationItem.Name,
			CalculationGroupName = calculationGroupName,
			Ordinal = calculationItem.Ordinal,
			HasChanges = true
		};
	}

	internal static void RenameCalculationItemInternal(IConnectionInfo info, string calculationGroupName, string oldName, string newName)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo is null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(calculationGroupName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("calculationGroupName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(oldName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("oldName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(newName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("newName is required", ErrorSource.User);
		}
		CalculationGroup calculationGroup = FindCalculationGroup(info.Database.Model, calculationGroupName);
		CalculationItem calculationItem = calculationGroup.CalculationItems.Find(oldName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Calculation item '{oldName}' not found in calculation group '{calculationGroupName}'", ErrorSource.User);
		if (calculationGroup.CalculationItems.Contains(newName) && !string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Calculation item '{newName}' already exists in calculation group '{calculationGroupName}'", ErrorSource.User);
		}
		calculationItem.RequestRename(newName);
		TransactionOperations.RecordOperation(info, $"Renamed calculation item '{oldName}' to '{newName}' in calculation group '{calculationGroupName}'");
		ConnectionOperations.SaveChangesWithRollback(info, "rename calculation item", OperationType.Update, CheckpointMode.AfterRequestRename);
	}

	internal static void DeleteCalculationItemInternal(IConnectionInfo info, string calculationGroupName, string calculationItemName)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo is null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(calculationGroupName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("calculationGroupName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(calculationItemName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("calculationItemName is required", ErrorSource.User);
		}
		Database database = info.Database;
		CalculationGroup calculationGroup = FindCalculationGroup(database.Model, calculationGroupName);
		CalculationItem metadataObject = calculationGroup.CalculationItems.Find(calculationItemName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Calculation item '{calculationItemName}' not found in calculation group '{calculationGroupName}'", ErrorSource.User);
		if (calculationGroup.CalculationItems.Count == 1)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Cannot delete the last calculation item from a calculation group. Delete the calculation group instead.", ErrorSource.User);
		}
		List<string> list = new List<string>();
		foreach (Table table in database.Model.Tables)
		{
			foreach (Measure measure in table.Measures)
			{
				if (!string.IsNullOrWhiteSpace(measure.Expression) && measure.Expression.Contains($"'{calculationGroupName}'[{calculationItemName}]"))
				{
					list.Add("[" + measure.Name + "]");
				}
			}
		}
		if (list.Count > 0)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Cannot delete calculation item '" + calculationItemName + "' because it is referenced by: " + string.Join(", ", list), ErrorSource.User);
		}
		calculationGroup.CalculationItems.Remove(metadataObject);
		TransactionOperations.RecordOperation(info, $"Deleted calculation item '{calculationItemName}' from calculation group '{calculationGroupName}'");
		ConnectionOperations.SaveChangesWithRollback(info, "delete calculation item", OperationType.Delete);
	}

	public static async Task ReorderCalculationItems(string? connectionName, string calculationGroupName, List<string> calculationItemNamesInOrder)
	{
		if (string.IsNullOrWhiteSpace(calculationGroupName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("calculationGroupName is required", ErrorSource.User);
		}
		if (calculationItemNamesInOrder == null || calculationItemNamesInOrder.Count == 0)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("calculationItemNamesInOrder cannot be null or empty", ErrorSource.User);
		}
		await using IConnectionInfo info = await ConnectionOperations.GetAsync(connectionName);
		ReorderCalculationItemsInternal(info, calculationGroupName, calculationItemNamesInOrder);
	}

	internal static void ReorderCalculationItemsInternal(IConnectionInfo info, string calculationGroupName, List<string> calculationItemNamesInOrder)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo is null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(calculationGroupName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("calculationGroupName is required", ErrorSource.User);
		}
		if (calculationItemNamesInOrder == null || calculationItemNamesInOrder.Count == 0)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("calculationItemNamesInOrder cannot be null or empty", ErrorSource.User);
		}
		CalculationGroup calculationGroup = FindCalculationGroup(info.Database.Model, calculationGroupName);
		if (calculationItemNamesInOrder.Count != calculationItemNamesInOrder.Distinct().Count())
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Duplicate calculation item names found in calculationItemNamesInOrder", ErrorSource.User);
		}
		List<string> list = calculationGroup.CalculationItems.Select((CalculationItem item) => item.Name).ToList();
		if (calculationItemNamesInOrder.Count != list.Count)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Number of calculation items provided ({calculationItemNamesInOrder.Count}) does not match the number of calculation items in the group ({list.Count})");
		}
		foreach (string item in calculationItemNamesInOrder)
		{
			if (!list.Contains(item))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage($"Calculation item '{item}' not found in calculation group '{calculationGroupName}'", ErrorSource.User);
			}
		}
		int i;
		for (i = 0; i < calculationItemNamesInOrder.Count; i++)
		{
			CalculationItem calculationItem = calculationGroup.CalculationItems.FirstOrDefault((CalculationItem item) => item.Name == calculationItemNamesInOrder[i]);
			if (calculationItem != null)
			{
				calculationItem.Ordinal = i;
			}
		}
		TransactionOperations.RecordOperation(info, "Reordered calculation items in calculation group '" + calculationGroupName + "'");
		ConnectionOperations.SaveChangesWithRollback(info, "reorder calculation items", OperationType.Update);
	}

	public static async Task<BatchOperationResponse> CreateCalculationGroups(string? connectionName, List<CalculationGroupDefinition> groups, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "CreateGroup",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (groups == null || !groups.Any())
		{
			response.Success = false;
			response.Message = "No calculation groups provided for creation";
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
		IConnectionInfo conn = await ConnectionOperations.GetAsync(connectionName);
		BatchOperationResponse result;
		try
		{
			TransactionSetupResult transactionSetupResult = BatchTransactionHelper.HandleTransactionSetup(conn, options.UseTransaction, connectionName, warnings);
			string transactionId = transactionSetupResult.TransactionId;
			bool ownsTransaction = transactionSetupResult.OwnsTransaction;
			try
			{
				for (int i = 0; i < groups.Count; i++)
				{
					CalculationGroupDefinition calculationGroupDefinition = groups[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = calculationGroupDefinition.Name
					};
					try
					{
						CalculationGroupOperationResult calculationGroupOperationResult = CreateCalculationGroupInternal(conn, calculationGroupDefinition);
						itemResult.Success = true;
						itemResult.Message = $"Successfully created calculation group '{calculationGroupDefinition.Name}' with {calculationGroupOperationResult.CalculationItemCount} calculation items";
						itemResult.Data = calculationGroupOperationResult;
						successCount++;
						if (transactionId != null)
						{
							TransactionOperations.RecordOperation(conn, "Created calculation group '" + calculationGroupDefinition.Name + "'");
						}
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error creating calculation group '" + calculationGroupDefinition.Name + "': " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				bool transactionFailed = BatchTransactionHelper.FinalizeBatchTransaction(conn, response, transactionId, ownsTransaction, groups.Count, ref successCount, ref failureCount, "Created", "calculation group(s)");
				PostCommitDaxValidator.Append(conn, warnings, response.Results, groups, transactionId, ownsTransaction, transactionFailed, failureCount, "created", (CalculationGroupDefinition def) => ResolveCalculationGroupForValidation(conn, def));
			}
			catch (Exception ex2)
			{
				failureCount = BatchTransactionHelper_HandleException(transactionId, ownsTransaction, conn, ex2, groups.Count, ref successCount, response);
			}
			finally
			{
				stopwatch.Stop();
			}
			response.Summary = new BatchSummary
			{
				TotalItems = groups.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		finally
		{
			if (conn != null)
			{
				await conn.DisposeAsync();
			}
		}
		return result;
	}

	public static async Task<BatchOperationResponse> UpdateCalculationGroups(string? connectionName, List<CalculationGroupDefinition> groups, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "UpdateGroup",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (groups == null || !groups.Any())
		{
			response.Success = false;
			response.Message = "No calculation groups provided for update";
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
		IConnectionInfo conn = await ConnectionOperations.GetAsync(connectionName);
		BatchOperationResponse result;
		try
		{
			TransactionSetupResult transactionSetupResult = BatchTransactionHelper.HandleTransactionSetup(conn, options.UseTransaction, connectionName, warnings);
			string transactionId = transactionSetupResult.TransactionId;
			bool ownsTransaction = transactionSetupResult.OwnsTransaction;
			try
			{
				for (int i = 0; i < groups.Count; i++)
				{
					CalculationGroupDefinition calculationGroupDefinition = groups[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = calculationGroupDefinition.Name
					};
					try
					{
						CalculationGroupOperationResult calculationGroupOperationResult = UpdateCalculationGroupInternal(conn, calculationGroupDefinition);
						itemResult.Success = true;
						itemResult.Message = (calculationGroupOperationResult.HasChanges ? ("Successfully updated calculation group '" + calculationGroupDefinition.Name + "'") : ("No changes detected for calculation group '" + calculationGroupDefinition.Name + "'"));
						itemResult.Data = calculationGroupOperationResult;
						successCount++;
						if (transactionId != null && calculationGroupOperationResult.HasChanges)
						{
							TransactionOperations.RecordOperation(conn, "Updated calculation group '" + calculationGroupDefinition.Name + "'");
						}
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error updating calculation group '" + calculationGroupDefinition.Name + "': " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				bool transactionFailed = BatchTransactionHelper.FinalizeBatchTransaction(conn, response, transactionId, ownsTransaction, groups.Count, ref successCount, ref failureCount, "Updated", "calculation group(s)");
				PostCommitDaxValidator.Append(conn, warnings, response.Results, groups, transactionId, ownsTransaction, transactionFailed, failureCount, "updated", (CalculationGroupDefinition def) => ResolveCalculationGroupForValidation(conn, def));
			}
			catch (Exception ex2)
			{
				failureCount = BatchTransactionHelper_HandleException(transactionId, ownsTransaction, conn, ex2, groups.Count, ref successCount, response);
			}
			finally
			{
				stopwatch.Stop();
			}
			response.Summary = new BatchSummary
			{
				TotalItems = groups.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		finally
		{
			if (conn != null)
			{
				await conn.DisposeAsync();
			}
		}
		return result;
	}

	public static async Task<BatchOperationResponse> DeleteCalculationGroups(string? connectionName, List<CalculationGroupReference> groups, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "DeleteGroup",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (groups == null || !groups.Any())
		{
			response.Success = false;
			response.Message = "No calculation groups provided for deletion";
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
		BatchOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			TransactionSetupResult transactionSetupResult = BatchTransactionHelper.HandleTransactionSetup(connectionInfo, options.UseTransaction, connectionName, warnings);
			string transactionId = transactionSetupResult.TransactionId;
			bool ownsTransaction = transactionSetupResult.OwnsTransaction;
			try
			{
				for (int i = 0; i < groups.Count; i++)
				{
					CalculationGroupReference calculationGroupReference = groups[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = calculationGroupReference.Name
					};
					try
					{
						DeleteCalculationGroupInternal(connectionInfo, calculationGroupReference.Name);
						itemResult.Success = true;
						itemResult.Message = "Successfully deleted calculation group '" + calculationGroupReference.Name + "'";
						successCount++;
						if (transactionId != null)
						{
							TransactionOperations.RecordOperation(connectionInfo, "Deleted calculation group '" + calculationGroupReference.Name + "'");
						}
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error deleting calculation group '" + calculationGroupReference.Name + "': " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				BatchTransactionHelper.FinalizeBatchTransaction(connectionInfo, response, transactionId, ownsTransaction, groups.Count, ref successCount, ref failureCount, "Deleted", "calculation group(s)");
			}
			catch (Exception ex2)
			{
				failureCount = BatchTransactionHelper_HandleException(transactionId, ownsTransaction, connectionInfo, ex2, groups.Count, ref successCount, response);
			}
			finally
			{
				stopwatch.Stop();
			}
			response.Summary = new BatchSummary
			{
				TotalItems = groups.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> GetCalculationGroups(string? connectionName, List<CalculationGroupReference> groups, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "GetGroup",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (groups == null || !groups.Any())
		{
			response.Success = false;
			response.Message = "No calculation groups provided for retrieval";
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
		BatchOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				for (int i = 0; i < groups.Count; i++)
				{
					CalculationGroupReference calculationGroupReference = groups[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = calculationGroupReference.Name
					};
					try
					{
						CalculationGroupGet calculationGroupInternal = GetCalculationGroupInternal(connectionInfo.Database, calculationGroupReference.Name);
						itemResult.Success = true;
						itemResult.Message = "Successfully retrieved calculation group '" + calculationGroupReference.Name + "'";
						itemResult.Data = calculationGroupInternal;
						successCount++;
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error retrieving calculation group '" + calculationGroupReference.Name + "': " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				response.Success = failureCount == 0;
				response.Message = $"Retrieved {successCount} of {groups.Count} calculation group(s)";
			}
			catch (Exception ex2)
			{
				response.Success = false;
				response.Exceptions.Add(ex2);
				response.Message = "Get operation failed: " + ex2.Message;
				failureCount = groups.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
				AuditEvent.Default.Emit("get calculation group", response.Success, OperationType.Read, connectionInfo);
			}
			response.Summary = new BatchSummary
			{
				TotalItems = groups.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> RenameCalculationGroups(string? connectionName, List<CalculationGroupRename> groups, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "RenameGroup",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (groups == null || !groups.Any())
		{
			response.Success = false;
			response.Message = "No calculation groups provided for rename";
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
		BatchOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			TransactionSetupResult transactionSetupResult = BatchTransactionHelper.HandleTransactionSetup(connectionInfo, options.UseTransaction, connectionName, warnings);
			string transactionId = transactionSetupResult.TransactionId;
			bool ownsTransaction = transactionSetupResult.OwnsTransaction;
			try
			{
				for (int i = 0; i < groups.Count; i++)
				{
					CalculationGroupRename calculationGroupRename = groups[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = calculationGroupRename.CurrentName + " -> " + calculationGroupRename.NewName
					};
					try
					{
						RenameCalculationGroupInternal(connectionInfo, calculationGroupRename.CurrentName, calculationGroupRename.NewName);
						itemResult.Success = true;
						itemResult.Message = $"Successfully renamed calculation group '{calculationGroupRename.CurrentName}' to '{calculationGroupRename.NewName}'";
						successCount++;
						if (transactionId != null)
						{
							TransactionOperations.RecordOperation(connectionInfo, $"Renamed calculation group '{calculationGroupRename.CurrentName}' to '{calculationGroupRename.NewName}'");
						}
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error renaming calculation group '" + calculationGroupRename.CurrentName + "': " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				BatchTransactionHelper.FinalizeBatchTransaction(connectionInfo, response, transactionId, ownsTransaction, groups.Count, ref successCount, ref failureCount, "Renamed", "calculation group(s)");
			}
			catch (Exception ex2)
			{
				failureCount = BatchTransactionHelper_HandleException(transactionId, ownsTransaction, connectionInfo, ex2, groups.Count, ref successCount, response);
			}
			finally
			{
				stopwatch.Stop();
			}
			response.Summary = new BatchSummary
			{
				TotalItems = groups.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> CreateCalculationItems(string? connectionName, List<CalculationItemDefinition> items, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "CreateItem",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (items == null || !items.Any())
		{
			response.Success = false;
			response.Message = "No calculation items provided for creation";
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
		IConnectionInfo conn = await ConnectionOperations.GetAsync(connectionName);
		BatchOperationResponse result;
		try
		{
			TransactionSetupResult transactionSetupResult = BatchTransactionHelper.HandleTransactionSetup(conn, options.UseTransaction, connectionName, warnings);
			string transactionId = transactionSetupResult.TransactionId;
			bool ownsTransaction = transactionSetupResult.OwnsTransaction;
			try
			{
				for (int i = 0; i < items.Count; i++)
				{
					CalculationItemDefinition calculationItemDefinition = items[i];
					string value = calculationItemDefinition.CalculationGroupName ?? "";
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = $"[{value}].[{calculationItemDefinition.Name}]"
					};
					try
					{
						if (string.IsNullOrWhiteSpace(calculationItemDefinition.CalculationGroupName))
						{
							throw McpExceptionWithSource.FromTelemetrySafeMessage("CalculationGroupName is required for each calculation item", ErrorSource.User);
						}
						CalculationItemOperationResult data = CreateCalculationItemInternal(conn, calculationItemDefinition.CalculationGroupName, calculationItemDefinition);
						itemResult.Success = true;
						itemResult.Message = $"Successfully created calculation item '{calculationItemDefinition.Name}' in group '{calculationItemDefinition.CalculationGroupName}'";
						itemResult.Data = data;
						successCount++;
						if (transactionId != null)
						{
							TransactionOperations.RecordOperation(conn, $"Created calculation item '{calculationItemDefinition.Name}' in group '{calculationItemDefinition.CalculationGroupName}'");
						}
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error creating calculation item '" + calculationItemDefinition.Name + "': " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				bool transactionFailed = BatchTransactionHelper.FinalizeBatchTransaction(conn, response, transactionId, ownsTransaction, items.Count, ref successCount, ref failureCount, "Created", "calculation item(s)");
				PostCommitDaxValidator.Append(conn, warnings, response.Results, items, transactionId, ownsTransaction, transactionFailed, failureCount, "created", (CalculationItemDefinition def) => ResolveCalculationItemForValidation(conn, def));
			}
			catch (Exception ex2)
			{
				failureCount = BatchTransactionHelper_HandleException(transactionId, ownsTransaction, conn, ex2, items.Count, ref successCount, response);
			}
			finally
			{
				stopwatch.Stop();
			}
			response.Summary = new BatchSummary
			{
				TotalItems = items.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		finally
		{
			if (conn != null)
			{
				await conn.DisposeAsync();
			}
		}
		return result;
	}

	public static async Task<BatchOperationResponse> UpdateCalculationItems(string? connectionName, List<CalculationItemDefinition> items, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "UpdateItem",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (items == null || !items.Any())
		{
			response.Success = false;
			response.Message = "No calculation items provided for update";
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
		IConnectionInfo conn = await ConnectionOperations.GetAsync(connectionName);
		BatchOperationResponse result;
		try
		{
			TransactionSetupResult transactionSetupResult = BatchTransactionHelper.HandleTransactionSetup(conn, options.UseTransaction, connectionName, warnings);
			string transactionId = transactionSetupResult.TransactionId;
			bool ownsTransaction = transactionSetupResult.OwnsTransaction;
			try
			{
				for (int i = 0; i < items.Count; i++)
				{
					CalculationItemDefinition calculationItemDefinition = items[i];
					string value = calculationItemDefinition.CalculationGroupName ?? "";
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = $"[{value}].[{calculationItemDefinition.Name}]"
					};
					try
					{
						if (string.IsNullOrWhiteSpace(calculationItemDefinition.CalculationGroupName))
						{
							throw McpExceptionWithSource.FromTelemetrySafeMessage("CalculationGroupName is required for each calculation item", ErrorSource.User);
						}
						CalculationItemOperationResult calculationItemOperationResult = UpdateCalculationItemInternal(conn, calculationItemDefinition.CalculationGroupName, calculationItemDefinition.Name, calculationItemDefinition);
						itemResult.Success = true;
						itemResult.Message = (calculationItemOperationResult.HasChanges ? $"Successfully updated calculation item '{calculationItemDefinition.Name}' in group '{calculationItemDefinition.CalculationGroupName}'" : $"No changes detected for calculation item '{calculationItemDefinition.Name}' in group '{calculationItemDefinition.CalculationGroupName}'");
						itemResult.Data = calculationItemOperationResult;
						successCount++;
						if (transactionId != null && calculationItemOperationResult.HasChanges)
						{
							TransactionOperations.RecordOperation(conn, $"Updated calculation item '{calculationItemDefinition.Name}' in group '{calculationItemDefinition.CalculationGroupName}'");
						}
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error updating calculation item '" + calculationItemDefinition.Name + "': " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				bool transactionFailed = BatchTransactionHelper.FinalizeBatchTransaction(conn, response, transactionId, ownsTransaction, items.Count, ref successCount, ref failureCount, "Updated", "calculation item(s)");
				PostCommitDaxValidator.Append(conn, warnings, response.Results, items, transactionId, ownsTransaction, transactionFailed, failureCount, "updated", (CalculationItemDefinition def) => ResolveCalculationItemForValidation(conn, def));
			}
			catch (Exception ex2)
			{
				failureCount = BatchTransactionHelper_HandleException(transactionId, ownsTransaction, conn, ex2, items.Count, ref successCount, response);
			}
			finally
			{
				stopwatch.Stop();
			}
			response.Summary = new BatchSummary
			{
				TotalItems = items.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		finally
		{
			if (conn != null)
			{
				await conn.DisposeAsync();
			}
		}
		return result;
	}

	public static async Task<BatchOperationResponse> DeleteCalculationItems(string? connectionName, List<CalculationItemReference> items, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "DeleteItem",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (items == null || !items.Any())
		{
			response.Success = false;
			response.Message = "No calculation items provided for deletion";
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
					CalculationItemReference calculationItemReference = items[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = $"[{calculationItemReference.CalculationGroupName}].[{calculationItemReference.Name}]"
					};
					try
					{
						DeleteCalculationItemInternal(connectionInfo, calculationItemReference.CalculationGroupName, calculationItemReference.Name);
						itemResult.Success = true;
						itemResult.Message = $"Successfully deleted calculation item '{calculationItemReference.Name}' from group '{calculationItemReference.CalculationGroupName}'";
						successCount++;
						if (transactionId != null)
						{
							TransactionOperations.RecordOperation(connectionInfo, $"Deleted calculation item '{calculationItemReference.Name}' from group '{calculationItemReference.CalculationGroupName}'");
						}
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error deleting calculation item '" + calculationItemReference.Name + "': " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				BatchTransactionHelper.FinalizeBatchTransaction(connectionInfo, response, transactionId, ownsTransaction, items.Count, ref successCount, ref failureCount, "Deleted", "calculation item(s)");
			}
			catch (Exception ex2)
			{
				failureCount = BatchTransactionHelper_HandleException(transactionId, ownsTransaction, connectionInfo, ex2, items.Count, ref successCount, response);
			}
			finally
			{
				stopwatch.Stop();
			}
			response.Summary = new BatchSummary
			{
				TotalItems = items.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> GetCalculationItems(string? connectionName, List<CalculationItemReference> items, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "GetItem",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (items == null || !items.Any())
		{
			response.Success = false;
			response.Message = "No calculation items provided for retrieval";
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
		BatchOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				for (int i = 0; i < items.Count; i++)
				{
					CalculationItemReference calculationItemReference = items[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = $"[{calculationItemReference.CalculationGroupName}].[{calculationItemReference.Name}]"
					};
					try
					{
						CalculationItemGet calculationItemInternal = GetCalculationItemInternal(connectionInfo.Database, calculationItemReference.CalculationGroupName, calculationItemReference.Name);
						itemResult.Success = true;
						itemResult.Message = $"Successfully retrieved calculation item '{calculationItemReference.Name}' from group '{calculationItemReference.CalculationGroupName}'";
						itemResult.Data = calculationItemInternal;
						successCount++;
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error retrieving calculation item '" + calculationItemReference.Name + "': " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				response.Success = failureCount == 0;
				response.Message = $"Retrieved {successCount} of {items.Count} calculation item(s)";
			}
			catch (Exception ex2)
			{
				response.Success = false;
				response.Exceptions.Add(ex2);
				response.Message = "Get operation failed: " + ex2.Message;
				failureCount = items.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
				AuditEvent.Default.Emit("get calculation items", response.Success, OperationType.Read, connectionInfo);
			}
			response.Summary = new BatchSummary
			{
				TotalItems = items.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> RenameCalculationItems(string? connectionName, List<CalculationItemRename> items, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "RenameItem",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (items == null || !items.Any())
		{
			response.Success = false;
			response.Message = "No calculation items provided for rename";
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
					CalculationItemRename calculationItemRename = items[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = $"[{calculationItemRename.CalculationGroupName}].[{calculationItemRename.CurrentName}] -> [{calculationItemRename.NewName}]"
					};
					try
					{
						RenameCalculationItemInternal(connectionInfo, calculationItemRename.CalculationGroupName, calculationItemRename.CurrentName, calculationItemRename.NewName);
						itemResult.Success = true;
						itemResult.Message = $"Successfully renamed calculation item '{calculationItemRename.CurrentName}' to '{calculationItemRename.NewName}' in group '{calculationItemRename.CalculationGroupName}'";
						successCount++;
						if (transactionId != null)
						{
							TransactionOperations.RecordOperation(connectionInfo, $"Renamed calculation item '{calculationItemRename.CurrentName}' to '{calculationItemRename.NewName}' in group '{calculationItemRename.CalculationGroupName}'");
						}
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error renaming calculation item '" + calculationItemRename.CurrentName + "': " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				BatchTransactionHelper.FinalizeBatchTransaction(connectionInfo, response, transactionId, ownsTransaction, items.Count, ref successCount, ref failureCount, "Renamed", "calculation item rename(s)");
			}
			catch (Exception ex2)
			{
				failureCount = BatchTransactionHelper_HandleException(transactionId, ownsTransaction, connectionInfo, ex2, items.Count, ref successCount, response);
			}
			finally
			{
				stopwatch.Stop();
			}
			response.Summary = new BatchSummary
			{
				TotalItems = items.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> ReorderCalculationItemsBatch(string? connectionName, List<CalculationItemReorder> reorders, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "ReorderItems",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (reorders == null || !reorders.Any())
		{
			response.Success = false;
			response.Message = "No reorder definitions provided";
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
		BatchOperationResponse result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			TransactionSetupResult transactionSetupResult = BatchTransactionHelper.HandleTransactionSetup(connectionInfo, options.UseTransaction, connectionName, warnings);
			string transactionId = transactionSetupResult.TransactionId;
			bool ownsTransaction = transactionSetupResult.OwnsTransaction;
			try
			{
				for (int i = 0; i < reorders.Count; i++)
				{
					CalculationItemReorder calculationItemReorder = reorders[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = calculationItemReorder.CalculationGroupName
					};
					try
					{
						ReorderCalculationItemsInternal(connectionInfo, calculationItemReorder.CalculationGroupName, calculationItemReorder.ItemNamesInOrder);
						itemResult.Success = true;
						itemResult.Message = "Successfully reordered calculation items in group '" + calculationItemReorder.CalculationGroupName + "'";
						successCount++;
						if (transactionId != null)
						{
							TransactionOperations.RecordOperation(connectionInfo, "Reordered calculation items in group '" + calculationItemReorder.CalculationGroupName + "'");
						}
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error reordering calculation items in group '" + calculationItemReorder.CalculationGroupName + "': " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				BatchTransactionHelper.FinalizeBatchTransaction(connectionInfo, response, transactionId, ownsTransaction, reorders.Count, ref successCount, ref failureCount, "Completed", "reorder operation(s)");
			}
			catch (Exception ex2)
			{
				failureCount = BatchTransactionHelper_HandleException(transactionId, ownsTransaction, connectionInfo, ex2, reorders.Count, ref successCount, response);
			}
			finally
			{
				stopwatch.Stop();
			}
			response.Summary = new BatchSummary
			{
				TotalItems = reorders.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	private static int BatchTransactionHelper_HandleException(string? transactionId, bool ownsTransaction, IConnectionInfo info, Exception ex, int totalCount, ref int successCount, BatchOperationResponse response)
	{
		if (transactionId != null && ownsTransaction)
		{
			try
			{
				TransactionOperations.RollbackTransactionInternal(info);
			}
			catch
			{
			}
			int failureCount = totalCount - successCount;
			BatchTransactionHelper.ApplyTransactionFailureFixup(response.Results, ref successCount, ref failureCount);
		}
		response.Success = false;
		response.Message = "Operation failed: " + ex.Message;
		response.Exceptions.Add(ex);
		return totalCount - successCount;
	}
}
