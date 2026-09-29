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

public static class UserHierarchyOperations
{
	public static async Task<List<HierarchyList>> ListHierarchies(string? connectionName, string tableName)
	{
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		List<HierarchyList> result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				List<HierarchyList> list = ListHierarchiesInternal(connectionInfo.Database, tableName);
				AuditEvent.Default.Emit("list hierarchies", success: true, OperationType.Read, connectionInfo);
				result = list;
			}
			catch
			{
				AuditEvent.Default.Emit("list hierarchies", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	internal static List<HierarchyList> ListHierarchiesInternal(Database db, string tableName)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		return (db.Model.Tables.Find(tableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + tableName + "' not found", ErrorSource.User)).Hierarchies.Select((Hierarchy h) => new HierarchyList
		{
			Name = h.Name,
			Description = ((!string.IsNullOrEmpty(h.Description)) ? h.Description : null),
			Levels = (from l in h.Levels
				orderby l.Ordinal
				select new LevelList
				{
					Name = l.Name,
					Description = ((!string.IsNullOrEmpty(l.Description)) ? l.Description : null)
				}).ToList(),
			DisplayFolder = ((!string.IsNullOrEmpty(h.DisplayFolder)) ? h.DisplayFolder : null),
			IsHidden = (h.IsHidden ? new bool?(true) : ((bool?)null))
		}).ToList();
	}

	public static async Task<HierarchyGet> GetHierarchy(string? connectionName, string tableName, string hierarchyName)
	{
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(hierarchyName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("hierarchyName is required", ErrorSource.User);
		}
		HierarchyGet hierarchyInternal;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			hierarchyInternal = GetHierarchyInternal(connectionInfo.Database, tableName, hierarchyName);
		}
		return hierarchyInternal;
	}

	internal static HierarchyGet GetHierarchyInternal(Database db, string tableName, string hierarchyName)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(hierarchyName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("hierarchyName is required", ErrorSource.User);
		}
		Hierarchy hierarchy = (db.Model.Tables.Find(tableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + tableName + "' not found", ErrorSource.User)).Hierarchies.Find(hierarchyName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Hierarchy '{hierarchyName}' not found in table '{tableName}'", ErrorSource.User);
		HierarchyGet hierarchyGet = new HierarchyGet
		{
			TableName = tableName,
			Name = hierarchy.Name,
			Description = hierarchy.Description,
			IsHidden = hierarchy.IsHidden,
			DisplayFolder = hierarchy.DisplayFolder,
			HideMembers = hierarchy.HideMembers.ToString(),
			LineageTag = hierarchy.LineageTag,
			SourceLineageTag = hierarchy.SourceLineageTag,
			State = hierarchy.State,
			ModifiedTime = hierarchy.ModifiedTime,
			StructureModifiedTime = hierarchy.StructureModifiedTime,
			RefreshedTime = hierarchy.RefreshedTime
		};
		foreach (Level item in hierarchy.Levels.OrderBy((Level l) => l.Ordinal))
		{
			LevelGet levelGet = new LevelGet
			{
				Name = item.Name,
				Description = item.Description,
				Ordinal = item.Ordinal,
				ColumnName = item.Column?.Name,
				LineageTag = item.LineageTag,
				SourceLineageTag = item.SourceLineageTag,
				ModifiedTime = item.ModifiedTime
			};
			if (levelGet.Annotations == null)
			{
				levelGet.Annotations = new List<KeyValuePair<string, string>>();
			}
			foreach (Annotation annotation in item.Annotations)
			{
				levelGet.Annotations.Add(new KeyValuePair<string, string>(annotation.Name ?? string.Empty, annotation.Value ?? string.Empty));
			}
			levelGet.ExtendedProperties = ExtendedPropertyHelpers.ExtractFromLevel(item);
			hierarchyGet.Levels.Add(levelGet);
		}
		if (hierarchyGet.Annotations == null)
		{
			hierarchyGet.Annotations = new List<KeyValuePair<string, string>>();
		}
		foreach (Annotation annotation2 in hierarchy.Annotations)
		{
			hierarchyGet.Annotations.Add(new KeyValuePair<string, string>(annotation2.Name ?? string.Empty, annotation2.Value ?? string.Empty));
		}
		hierarchyGet.ExtendedProperties = ExtendedPropertyHelpers.ExtractFromHierarchy(hierarchy);
		return hierarchyGet;
	}

	public static async Task<HierarchyOperationResult> CreateHierarchy(string? connectionName, HierarchyDefinition def)
	{
		ValidateHierarchyDefinition(def, isCreate: true);
		HierarchyOperationResult result;
		await using (IConnectionInfo info = await ConnectionOperations.GetAsync(connectionName))
		{
			result = CreateHierarchyInternal(info, def);
		}
		return result;
	}

	internal static HierarchyOperationResult CreateHierarchyInternal(IConnectionInfo info, HierarchyDefinition def)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (def == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("HierarchyDefinition cannot be null", ErrorSource.User);
		}
		Table table = info.Database.Model.Tables.Find(def.TableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + def.TableName + "' not found", ErrorSource.User);
		if (table.Hierarchies.Contains(def.Name))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Hierarchy '{def.Name}' already exists in table '{def.TableName}'", ErrorSource.User);
		}
		foreach (LevelDefinition level2 in def.Levels)
		{
			if (table.Columns.Find(level2.ColumnName) == null)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage($"Column '{level2.ColumnName}' not found in table '{def.TableName}' for level '{level2.Name}'", ErrorSource.User);
			}
		}
		Hierarchy hierarchy = new Hierarchy
		{
			Name = def.Name
		};
		if (!string.IsNullOrWhiteSpace(def.Description))
		{
			hierarchy.Description = def.Description;
		}
		if (def.IsHidden.HasValue)
		{
			hierarchy.IsHidden = def.IsHidden.Value;
		}
		if (!string.IsNullOrWhiteSpace(def.DisplayFolder))
		{
			hierarchy.DisplayFolder = def.DisplayFolder;
		}
		if (!string.IsNullOrWhiteSpace(def.HideMembers))
		{
			if (!Enum.TryParse<HierarchyHideMembersType>(def.HideMembers, ignoreCase: true, out var result))
			{
				string[] names = Enum.GetNames(typeof(HierarchyHideMembersType));
				throw new McpExceptionWithSource("Invalid HideMembers '" + def.HideMembers + "'. Valid values are: " + string.Join(", ", names), ErrorSource.User, "Invalid HideMembers supplied. Valid values are: " + string.Join(", ", names) + ".");
			}
			hierarchy.HideMembers = result;
		}
		hierarchy.LineageTag = (string.IsNullOrWhiteSpace(def.LineageTag) ? Guid.NewGuid().ToString() : def.LineageTag);
		if (!string.IsNullOrWhiteSpace(def.SourceLineageTag))
		{
			hierarchy.SourceLineageTag = def.SourceLineageTag;
		}
		if (def.Annotations != null)
		{
			foreach (KeyValuePair<string, string> annotation in def.Annotations)
			{
				if (!string.IsNullOrWhiteSpace(annotation.Key))
				{
					hierarchy.Annotations.Add(new Annotation
					{
						Name = annotation.Key,
						Value = annotation.Value
					});
				}
			}
		}
		if (def.ExtendedProperties != null)
		{
			ExtendedPropertyHelpers.ApplyToHierarchy(hierarchy, def.ExtendedProperties);
		}
		List<LevelDefinition> list = def.Levels.ToList();
		if (list.All((LevelDefinition l) => !l.Ordinal.HasValue))
		{
			for (int num = 0; num < list.Count; num++)
			{
				list[num].Ordinal = num;
			}
		}
		foreach (LevelDefinition item in list.OrderBy((LevelDefinition l) => l.Ordinal))
		{
			Column column = table.Columns.Find(item.ColumnName);
			Level level = new Level
			{
				Name = (item.Name ?? item.ColumnName),
				Ordinal = item.Ordinal.Value,
				Column = column
			};
			if (!string.IsNullOrWhiteSpace(item.Description))
			{
				level.Description = item.Description;
			}
			level.LineageTag = (string.IsNullOrWhiteSpace(item.LineageTag) ? Guid.NewGuid().ToString() : item.LineageTag);
			if (!string.IsNullOrWhiteSpace(item.SourceLineageTag))
			{
				level.SourceLineageTag = item.SourceLineageTag;
			}
			if (item.Annotations != null)
			{
				foreach (KeyValuePair<string, string> annotation2 in item.Annotations)
				{
					if (!string.IsNullOrWhiteSpace(annotation2.Key))
					{
						level.Annotations.Add(new Annotation
						{
							Name = annotation2.Key,
							Value = annotation2.Value
						});
					}
				}
			}
			if (item.ExtendedProperties != null)
			{
				ExtendedPropertyHelpers.ApplyToLevel(level, item.ExtendedProperties);
			}
			hierarchy.Levels.Add(level);
		}
		table.Hierarchies.Add(hierarchy);
		TransactionOperations.RecordOperation(info, $"Created hierarchy '{def.Name}' with {def.Levels.Count} levels in table '{def.TableName}'");
		ConnectionOperations.SaveChangesWithRollback(info, "create hierarchy", OperationType.Create);
		return new HierarchyOperationResult
		{
			State = hierarchy.State.ToString(),
			HierarchyName = (hierarchy.Name ?? string.Empty),
			TableName = table.Name,
			LevelCount = hierarchy.Levels.Count,
			LevelNames = (from l in hierarchy.Levels
				orderby l.Ordinal
				select l.Name).ToList()
		};
	}

	public static async Task<HierarchyOperationResult> UpdateHierarchy(string? connectionName, HierarchyDefinition update)
	{
		ValidateHierarchyDefinition(update, isCreate: false);
		HierarchyOperationResult result;
		await using (IConnectionInfo info = await ConnectionOperations.GetAsync(connectionName))
		{
			result = UpdateHierarchyInternal(info, update);
		}
		return result;
	}

	internal static HierarchyOperationResult UpdateHierarchyInternal(IConnectionInfo info, HierarchyDefinition update)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (update == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("HierarchyDefinition cannot be null", ErrorSource.User);
		}
		Table table = info.Database.Model.Tables.Find(update.TableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + update.TableName + "' not found", ErrorSource.User);
		Hierarchy hierarchy = table.Hierarchies.Find(update.Name) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Hierarchy '{update.Name}' not found in table '{update.TableName}'", ErrorSource.User);
		bool flag = false;
		if (update.Description != null)
		{
			string text = (string.IsNullOrEmpty(update.Description) ? null : update.Description);
			if (text != hierarchy.Description)
			{
				hierarchy.Description = text;
				flag = true;
			}
		}
		if (update.DisplayFolder != null)
		{
			string text2 = (string.IsNullOrEmpty(update.DisplayFolder) ? null : update.DisplayFolder);
			if (text2 != hierarchy.DisplayFolder)
			{
				hierarchy.DisplayFolder = text2;
				flag = true;
			}
		}
		if (update.LineageTag != null)
		{
			string text3 = (string.IsNullOrEmpty(update.LineageTag) ? null : update.LineageTag);
			if (text3 != hierarchy.LineageTag)
			{
				hierarchy.LineageTag = text3;
				flag = true;
			}
		}
		if (update.SourceLineageTag != null)
		{
			string text4 = (string.IsNullOrEmpty(update.SourceLineageTag) ? null : update.SourceLineageTag);
			if (text4 != hierarchy.SourceLineageTag)
			{
				hierarchy.SourceLineageTag = text4;
				flag = true;
			}
		}
		if (update.IsHidden.HasValue && hierarchy.IsHidden != update.IsHidden.Value)
		{
			hierarchy.IsHidden = update.IsHidden.Value;
			flag = true;
		}
		if (!string.IsNullOrWhiteSpace(update.HideMembers))
		{
			if (!Enum.TryParse<HierarchyHideMembersType>(update.HideMembers, ignoreCase: true, out var result))
			{
				string[] names = Enum.GetNames(typeof(HierarchyHideMembersType));
				throw new McpExceptionWithSource("Invalid HideMembers '" + update.HideMembers + "'. Valid values are: " + string.Join(", ", names), ErrorSource.User, "Invalid HideMembers supplied. Valid values are: " + string.Join(", ", names) + ".");
			}
			if (hierarchy.HideMembers != result)
			{
				hierarchy.HideMembers = result;
				flag = true;
			}
		}
		if (update.Annotations != null && AnnotationHelpers.ReplaceAnnotations(hierarchy, update.Annotations, (Hierarchy obj) => obj.Annotations))
		{
			flag = true;
		}
		if (update.ExtendedProperties != null)
		{
			bool num = hierarchy.ExtendedProperties.Count > 0;
			ExtendedPropertyHelpers.ReplaceExtendedProperties(hierarchy, update.ExtendedProperties, (Hierarchy obj) => obj.ExtendedProperties);
			if (num || update.ExtendedProperties.Count > 0)
			{
				flag = true;
			}
		}
		if (update.Levels != null && update.Levels.Count > 0 && ApplyInlineLevelUpdates(hierarchy, table, update.Levels))
		{
			flag = true;
		}
		if (!flag)
		{
			return new HierarchyOperationResult
			{
				State = hierarchy.State.ToString(),
				HierarchyName = (hierarchy.Name ?? string.Empty),
				TableName = table.Name,
				LevelCount = hierarchy.Levels.Count,
				LevelNames = (from l in hierarchy.Levels
					orderby l.Ordinal
					select l.Name).ToList(),
				HasChanges = false
			};
		}
		TransactionOperations.RecordOperation(info, $"Updated hierarchy '{update.Name}' in table '{update.TableName}'");
		ConnectionOperations.SaveChangesWithRollback(info, "update hierarchy", OperationType.Update);
		return new HierarchyOperationResult
		{
			State = hierarchy.State.ToString(),
			HierarchyName = (hierarchy.Name ?? string.Empty),
			TableName = table.Name,
			LevelCount = hierarchy.Levels.Count,
			LevelNames = (from l in hierarchy.Levels
				orderby l.Ordinal
				select l.Name).ToList(),
			HasChanges = true
		};
	}

	private static bool ApplyInlineLevelUpdates(Hierarchy hierarchy, Table table, List<LevelDefinition> levelUpdates)
	{
		bool result = false;
		foreach (LevelDefinition levelDef in levelUpdates)
		{
			if (string.IsNullOrWhiteSpace(levelDef.Name))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Level name is required for inline level updates", ErrorSource.User);
			}
			Level level = hierarchy.Levels.FirstOrDefault((Level l) => l.Name == levelDef.Name);
			if (level == null)
			{
				if (string.IsNullOrWhiteSpace(levelDef.ColumnName))
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage("ColumnName is required when adding new level '" + levelDef.Name + "'", ErrorSource.User);
				}
				Column column = table.Columns.Find(levelDef.ColumnName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Column '{levelDef.ColumnName}' not found in table '{table.Name}' for new level '{levelDef.Name}'", ErrorSource.User);
				Level level2 = new Level
				{
					Name = levelDef.Name,
					Column = column
				};
				if (levelDef.Ordinal.HasValue)
				{
					level2.Ordinal = levelDef.Ordinal.Value;
					foreach (Level item in hierarchy.Levels.Where((Level l) => l.Ordinal >= levelDef.Ordinal.Value))
					{
						item.Ordinal++;
					}
				}
				else
				{
					level2.Ordinal = ((hierarchy.Levels.Count > 0) ? (hierarchy.Levels.Max((Level l) => l.Ordinal) + 1) : 0);
				}
				if (!string.IsNullOrWhiteSpace(levelDef.Description))
				{
					level2.Description = levelDef.Description;
				}
				level2.LineageTag = (string.IsNullOrWhiteSpace(levelDef.LineageTag) ? Guid.NewGuid().ToString() : levelDef.LineageTag);
				if (!string.IsNullOrWhiteSpace(levelDef.SourceLineageTag))
				{
					level2.SourceLineageTag = levelDef.SourceLineageTag;
				}
				if (levelDef.Annotations != null)
				{
					foreach (KeyValuePair<string, string> annotation in levelDef.Annotations)
					{
						if (!string.IsNullOrWhiteSpace(annotation.Key))
						{
							level2.Annotations.Add(new Annotation
							{
								Name = annotation.Key,
								Value = annotation.Value
							});
						}
					}
				}
				if (levelDef.ExtendedProperties != null)
				{
					ExtendedPropertyHelpers.ApplyToLevel(level2, levelDef.ExtendedProperties);
				}
				hierarchy.Levels.Add(level2);
				result = true;
				continue;
			}
			bool flag = false;
			if (levelDef.Description != null)
			{
				string text = (string.IsNullOrEmpty(levelDef.Description) ? null : levelDef.Description);
				if (text != level.Description)
				{
					level.Description = text;
					flag = true;
				}
			}
			if (!string.IsNullOrWhiteSpace(levelDef.ColumnName))
			{
				Column column2 = table.Columns.Find(levelDef.ColumnName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Column '{levelDef.ColumnName}' not found in table '{table.Name}'", ErrorSource.User);
				if (level.Column != column2)
				{
					level.Column = column2;
					flag = true;
				}
			}
			if (levelDef.Ordinal.HasValue && level.Ordinal != levelDef.Ordinal.Value)
			{
				level.Ordinal = levelDef.Ordinal.Value;
				flag = true;
			}
			if (levelDef.LineageTag != null)
			{
				string text2 = (string.IsNullOrEmpty(levelDef.LineageTag) ? null : levelDef.LineageTag);
				if (text2 != level.LineageTag)
				{
					level.LineageTag = text2;
					flag = true;
				}
			}
			if (levelDef.SourceLineageTag != null)
			{
				string text3 = (string.IsNullOrEmpty(levelDef.SourceLineageTag) ? null : levelDef.SourceLineageTag);
				if (text3 != level.SourceLineageTag)
				{
					level.SourceLineageTag = text3;
					flag = true;
				}
			}
			if (levelDef.Annotations != null && AnnotationHelpers.ReplaceAnnotations(level, levelDef.Annotations, (Level obj) => obj.Annotations))
			{
				flag = true;
			}
			if (levelDef.ExtendedProperties != null)
			{
				bool num = level.ExtendedProperties.Count > 0;
				ExtendedPropertyHelpers.ReplaceExtendedProperties(level, levelDef.ExtendedProperties, (Level obj) => obj.ExtendedProperties);
				if (num || levelDef.ExtendedProperties.Count > 0)
				{
					flag = true;
				}
			}
			if (flag)
			{
				result = true;
			}
		}
		return result;
	}

	public static async Task RenameHierarchy(string? connectionName, string tableName, string oldName, string newName)
	{
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(oldName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("oldName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(newName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("newName is required", ErrorSource.User);
		}
		await using IConnectionInfo info = await ConnectionOperations.GetAsync(connectionName);
		RenameHierarchyInternal(info, tableName, oldName, newName);
	}

	internal static void RenameHierarchyInternal(IConnectionInfo info, string tableName, string oldName, string newName)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(oldName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("oldName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(newName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("newName is required", ErrorSource.User);
		}
		Table obj = info.Database.Model.Tables.Find(tableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + tableName + "' not found", ErrorSource.User);
		Hierarchy hierarchy = obj.Hierarchies.Find(oldName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Hierarchy '{oldName}' not found in table '{tableName}'", ErrorSource.User);
		if (obj.Hierarchies.Contains(newName) && !string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Hierarchy '{newName}' already exists in table '{tableName}'", ErrorSource.User);
		}
		hierarchy.RequestRename(newName);
		TransactionOperations.RecordOperation(info, $"Renamed hierarchy '{oldName}' to '{newName}' in table '{tableName}'");
		ConnectionOperations.SaveChangesWithRollback(info, "rename hierarchy", OperationType.Update, CheckpointMode.AfterRequestRename);
	}

	public static async Task DeleteHierarchy(string? connectionName, string tableName, string hierarchyName, bool shouldCascadeDelete)
	{
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(hierarchyName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("hierarchyName is required", ErrorSource.User);
		}
		await using IConnectionInfo info = await ConnectionOperations.GetAsync(connectionName);
		DeleteHierarchyInternal(info, tableName, hierarchyName, shouldCascadeDelete);
	}

	internal static void DeleteHierarchyInternal(IConnectionInfo info, string tableName, string hierarchyName, bool shouldCascadeDelete)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(hierarchyName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("hierarchyName is required", ErrorSource.User);
		}
		Database database = info.Database;
		Table table = database.Model.Tables.Find(tableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + tableName + "' not found", ErrorSource.User);
		Hierarchy hierarchy = table.Hierarchies.Find(hierarchyName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Hierarchy '{hierarchyName}' not found in table '{tableName}'", ErrorSource.User);
		List<string> list = StructuralDependencyHelper.CheckAndDeleteDependenciesIfRequired(database, hierarchy, shouldCascadeDelete);
		if (!shouldCascadeDelete && list.Any())
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Cannot delete hierarchy '" + hierarchyName + "' because it has dependencies: " + string.Join(", ", list), ErrorSource.User);
		}
		table.Hierarchies.Remove(hierarchy);
		TransactionOperations.RecordOperation(info, $"Deleted hierarchy '{hierarchyName}' from table '{tableName}'");
		ConnectionOperations.SaveChangesWithRollback(info, "delete hierarchy", OperationType.Delete);
	}

	public static async Task<List<string>> GetHierarchyColumns(string? connectionName, string tableName, string hierarchyName)
	{
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(hierarchyName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("hierarchyName is required", ErrorSource.User);
		}
		List<string> result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				List<string> hierarchyColumnsInternal = GetHierarchyColumnsInternal(connectionInfo.Database, tableName, hierarchyName);
				AuditEvent.Default.Emit("get hierarchy columns", success: true, OperationType.Read, connectionInfo);
				result = hierarchyColumnsInternal;
			}
			catch
			{
				AuditEvent.Default.Emit("get hierarchy columns", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	internal static List<string> GetHierarchyColumnsInternal(Database db, string tableName, string hierarchyName)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(hierarchyName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("hierarchyName is required", ErrorSource.User);
		}
		return (from l in ((db.Model.Tables.Find(tableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + tableName + "' not found", ErrorSource.User)).Hierarchies.Find(hierarchyName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Hierarchy '{hierarchyName}' not found in table '{tableName}'", ErrorSource.User)).Levels
			orderby l.Ordinal
			select l.Column?.Name into c
			where c != null
			select c).ToList();
	}

	public static async Task AddLevel(string? connectionName, string tableName, string hierarchyName, LevelDefinition levelDef)
	{
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(hierarchyName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("hierarchyName is required", ErrorSource.User);
		}
		if (levelDef == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("levelDef is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(levelDef.Name))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Level name is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(levelDef.ColumnName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Level column is required", ErrorSource.User);
		}
		await using IConnectionInfo info = await ConnectionOperations.GetAsync(connectionName);
		AddLevelInternal(info, tableName, hierarchyName, levelDef);
	}

	internal static void AddLevelInternal(IConnectionInfo info, string tableName, string hierarchyName, LevelDefinition levelDef)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(hierarchyName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("hierarchyName is required", ErrorSource.User);
		}
		if (levelDef == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("LevelDefinition cannot be null", ErrorSource.User);
		}
		Table obj = info.Database.Model.Tables.Find(tableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + tableName + "' not found", ErrorSource.User);
		Hierarchy hierarchy = obj.Hierarchies.Find(hierarchyName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Hierarchy '{hierarchyName}' not found in table '{tableName}'", ErrorSource.User);
		Column column = obj.Columns.Find(levelDef.ColumnName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Column '{levelDef.ColumnName}' not found in table '{tableName}'", ErrorSource.User);
		if (hierarchy.Levels.Contains(levelDef.Name))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Level '{levelDef.Name}' already exists in hierarchy '{hierarchyName}'", ErrorSource.User);
		}
		TransactionOperations.RecordOperation(info, $"Added level '{levelDef.Name}' to hierarchy '{hierarchyName}' in table '{tableName}'");
		Level level = new Level
		{
			Name = levelDef.Name,
			Column = column
		};
		if (!string.IsNullOrWhiteSpace(levelDef.Description))
		{
			level.Description = levelDef.Description;
		}
		level.LineageTag = (string.IsNullOrWhiteSpace(levelDef.LineageTag) ? Guid.NewGuid().ToString() : levelDef.LineageTag);
		if (!string.IsNullOrWhiteSpace(levelDef.SourceLineageTag))
		{
			level.SourceLineageTag = levelDef.SourceLineageTag;
		}
		hierarchy.Levels.Add(level);
		if (levelDef.Ordinal.HasValue)
		{
			if (levelDef.Ordinal.Value < 0 || levelDef.Ordinal.Value > hierarchy.Levels.Count - 1)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage($"Invalid ordinal {levelDef.Ordinal.Value}. Must be between 0 and {hierarchy.Levels.Count - 1}", ErrorSource.User);
			}
			level.Ordinal = levelDef.Ordinal.Value;
			foreach (Level item in hierarchy.Levels.Where((Level l) => l != level && l.Ordinal >= levelDef.Ordinal.Value))
			{
				item.Ordinal++;
			}
		}
		if (levelDef.Annotations != null)
		{
			foreach (KeyValuePair<string, string> annotation in levelDef.Annotations)
			{
				if (!string.IsNullOrWhiteSpace(annotation.Key))
				{
					level.Annotations.Add(new Annotation
					{
						Name = annotation.Key,
						Value = annotation.Value
					});
				}
			}
		}
		if (levelDef.ExtendedProperties != null)
		{
			ExtendedPropertyHelpers.ApplyToLevel(level, levelDef.ExtendedProperties);
		}
		ConnectionOperations.SaveChangesWithRollback(info, "add level", OperationType.Create);
	}

	public static async Task RenameLevel(string? connectionName, string tableName, string hierarchyName, string oldLevelName, string newLevelName)
	{
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(hierarchyName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("hierarchyName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(oldLevelName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("oldLevelName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(newLevelName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("newLevelName is required", ErrorSource.User);
		}
		await using IConnectionInfo info = await ConnectionOperations.GetAsync(connectionName);
		RenameLevelInternal(info, tableName, hierarchyName, oldLevelName, newLevelName);
	}

	internal static void RenameLevelInternal(IConnectionInfo info, string tableName, string hierarchyName, string oldLevelName, string newLevelName)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(hierarchyName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("hierarchyName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(oldLevelName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("oldLevelName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(newLevelName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("newLevelName is required", ErrorSource.User);
		}
		Hierarchy obj = (info.Database.Model.Tables.Find(tableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + tableName + "' not found", ErrorSource.User)).Hierarchies.Find(hierarchyName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Hierarchy '{hierarchyName}' not found in table '{tableName}'", ErrorSource.User);
		Level level = obj.Levels.FirstOrDefault((Level l) => l.Name == oldLevelName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Level '{oldLevelName}' not found in hierarchy '{hierarchyName}'", ErrorSource.User);
		if (obj.Levels.Any((Level l) => l.Name == newLevelName) && !string.Equals(oldLevelName, newLevelName, StringComparison.OrdinalIgnoreCase))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Level '{newLevelName}' already exists in hierarchy '{hierarchyName}'", ErrorSource.User);
		}
		level.RequestRename(newLevelName);
		TransactionOperations.RecordOperation(info, $"Renamed level '{oldLevelName}' to '{newLevelName}' in hierarchy '{hierarchyName}' in table '{tableName}'");
		ConnectionOperations.SaveChangesWithRollback(info, "rename level", OperationType.Update, CheckpointMode.AfterRequestRename);
	}

	public static async Task UpdateLevel(string? connectionName, string tableName, string hierarchyName, string levelName, LevelDefinition update)
	{
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(hierarchyName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("hierarchyName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(levelName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("levelName is required", ErrorSource.User);
		}
		if (update == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Level update definition cannot be null", ErrorSource.User);
		}
		await using IConnectionInfo info = await ConnectionOperations.GetAsync(connectionName);
		UpdateLevelInternal(info, tableName, hierarchyName, levelName, update);
	}

	internal static void UpdateLevelInternal(IConnectionInfo info, string tableName, string hierarchyName, string levelName, LevelDefinition update)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(hierarchyName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("hierarchyName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(levelName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("levelName is required", ErrorSource.User);
		}
		if (update == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("LevelDefinition cannot be null", ErrorSource.User);
		}
		Table table = info.Database.Model.Tables.Find(tableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + tableName + "' not found", ErrorSource.User);
		Hierarchy hierarchy = table.Hierarchies.Find(hierarchyName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Hierarchy '{hierarchyName}' not found in table '{tableName}'", ErrorSource.User);
		Level level = hierarchy.Levels.FirstOrDefault((Level l) => l.Name == levelName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Level '{levelName}' not found in hierarchy '{hierarchyName}'", ErrorSource.User);
		bool flag = false;
		if (!string.IsNullOrWhiteSpace(update.Name) && level.Name != update.Name)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Level name cannot be changed through UpdateLevel. Use RenameLevel to rename level '{levelName}' to '{update.Name}'", ErrorSource.User);
		}
		if (update.Description != null)
		{
			string text = (string.IsNullOrEmpty(update.Description) ? null : update.Description);
			if (text != level.Description)
			{
				level.Description = text;
				flag = true;
			}
		}
		if (update.LineageTag != null)
		{
			string text2 = (string.IsNullOrEmpty(update.LineageTag) ? null : update.LineageTag);
			if (text2 != level.LineageTag)
			{
				level.LineageTag = text2;
				flag = true;
			}
		}
		if (update.SourceLineageTag != null)
		{
			string text3 = (string.IsNullOrEmpty(update.SourceLineageTag) ? null : update.SourceLineageTag);
			if (text3 != level.SourceLineageTag)
			{
				level.SourceLineageTag = text3;
				flag = true;
			}
		}
		if (update.Ordinal.HasValue && level.Ordinal != update.Ordinal.Value)
		{
			if (hierarchy.Levels.Any((Level l) => l != level && l.Ordinal == update.Ordinal.Value))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage($"Level with ordinal {update.Ordinal.Value} already exists in hierarchy '{hierarchyName}'", ErrorSource.User);
			}
			level.Ordinal = update.Ordinal.Value;
			flag = true;
		}
		if (update.ColumnName != null)
		{
			if (string.IsNullOrEmpty(update.ColumnName))
			{
				if (level.Column != null)
				{
					level.Column = null;
					flag = true;
				}
			}
			else
			{
				Column column = table.Columns.Find(update.ColumnName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Column '{update.ColumnName}' not found in table '{tableName}'", ErrorSource.User);
				if (level.Column != column)
				{
					level.Column = column;
					flag = true;
				}
			}
		}
		if (update.Annotations != null && AnnotationHelpers.ReplaceAnnotations(level, update.Annotations, (Level obj) => obj.Annotations))
		{
			flag = true;
		}
		if (update.ExtendedProperties != null)
		{
			bool num = level.ExtendedProperties.Count > 0;
			ExtendedPropertyHelpers.ReplaceExtendedProperties(level, update.ExtendedProperties, (Level obj) => obj.ExtendedProperties);
			if (num || update.ExtendedProperties.Count > 0)
			{
				flag = true;
			}
		}
		if (flag)
		{
			TransactionOperations.RecordOperation(info, $"Updated level '{levelName}' in hierarchy '{hierarchyName}' in table '{tableName}'");
			ConnectionOperations.SaveChangesWithRollback(info, "update level", OperationType.Update);
		}
	}

	public static async Task RemoveLevel(string? connectionName, string tableName, string hierarchyName, string levelName, bool shouldCascadeDelete)
	{
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(hierarchyName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("hierarchyName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(levelName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("levelName is required", ErrorSource.User);
		}
		await using IConnectionInfo info = await ConnectionOperations.GetAsync(connectionName);
		RemoveLevelInternal(info, tableName, hierarchyName, levelName, shouldCascadeDelete);
	}

	internal static void RemoveLevelInternal(IConnectionInfo info, string tableName, string hierarchyName, string levelName, bool shouldCascadeDelete)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(hierarchyName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("hierarchyName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(levelName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("levelName is required", ErrorSource.User);
		}
		Database database = info.Database;
		Hierarchy hierarchy = (database.Model.Tables.Find(tableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + tableName + "' not found", ErrorSource.User)).Hierarchies.Find(hierarchyName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Hierarchy '{hierarchyName}' not found in table '{tableName}'", ErrorSource.User);
		Level level = hierarchy.Levels.FirstOrDefault((Level l) => l.Name == levelName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Level '{levelName}' not found in hierarchy '{hierarchyName}'", ErrorSource.User);
		if (hierarchy.Levels.Count == 1)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Cannot remove the last level from a hierarchy. Delete the hierarchy instead.", ErrorSource.User);
		}
		List<string> list = StructuralDependencyHelper.CheckAndDeleteDependenciesIfRequired(database, level, shouldCascadeDelete);
		if (!shouldCascadeDelete && list.Any())
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Cannot remove level " + levelName + " because it is used by: " + string.Join(", ", list), ErrorSource.User);
		}
		hierarchy.Levels.Remove(level);
		ReorderLevelsInternal(info, tableName, hierarchyName, (from l in hierarchy.Levels
			orderby l.Ordinal
			select l.Name).ToList());
		TransactionOperations.RecordOperation(info, $"Removed level '{levelName}' from hierarchy '{hierarchyName}' in table '{tableName}'");
		ConnectionOperations.SaveChangesWithRollback(info, "remove level", OperationType.Delete);
	}

	public static async Task ReorderLevels(string? connectionName, string tableName, string hierarchyName, List<string> levelNamesInOrder)
	{
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(hierarchyName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("hierarchyName is required", ErrorSource.User);
		}
		if (levelNamesInOrder == null || levelNamesInOrder.Count == 0)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("levelNamesInOrder cannot be null or empty", ErrorSource.User);
		}
		await using IConnectionInfo info = await ConnectionOperations.GetAsync(connectionName);
		ReorderLevelsInternal(info, tableName, hierarchyName, levelNamesInOrder);
	}

	internal static void ReorderLevelsInternal(IConnectionInfo info, string tableName, string hierarchyName, List<string> levelNamesInOrder)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(hierarchyName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("hierarchyName is required", ErrorSource.User);
		}
		if (levelNamesInOrder == null || levelNamesInOrder.Count == 0)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("levelNamesInOrder cannot be null or empty", ErrorSource.User);
		}
		Hierarchy hierarchy = (info.Database.Model.Tables.Find(tableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + tableName + "' not found", ErrorSource.User)).Hierarchies.Find(hierarchyName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Hierarchy '{hierarchyName}' not found in table '{tableName}'", ErrorSource.User);
		if (levelNamesInOrder.Count != levelNamesInOrder.Distinct().Count())
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Duplicate level names found in levelNamesInOrder", ErrorSource.User);
		}
		List<string> list = hierarchy.Levels.Select((Level l) => l.Name).ToList();
		if (levelNamesInOrder.Count != list.Count)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Number of levels provided ({levelNamesInOrder.Count}) does not match the number of levels in the hierarchy ({list.Count})", ErrorSource.User);
		}
		foreach (string item in levelNamesInOrder)
		{
			if (!list.Contains(item))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage($"Level '{item}' not found in hierarchy '{hierarchyName}'", ErrorSource.User);
			}
		}
		int i;
		for (i = 0; i < levelNamesInOrder.Count; i++)
		{
			Level level = hierarchy.Levels.FirstOrDefault((Level l) => l.Name == levelNamesInOrder[i]);
			if (level != null)
			{
				level.Ordinal = i;
			}
		}
		TransactionOperations.RecordOperation(info, $"Reordered levels in hierarchy '{hierarchyName}' in table '{tableName}'");
		ConnectionOperations.SaveChangesWithRollback(info, "reorder levels", OperationType.Update);
	}

	public static async Task<string> ExportTMDL(string? connectionName, string tableName, string hierarchyName, ExportTmdl options)
	{
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(hierarchyName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("hierarchyName is required", ErrorSource.User);
		}
		string result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				string text = ExportTMDLInternal(connectionInfo.Database, tableName, hierarchyName, options);
				AuditEvent.Default.Emit("export hierarchy to TMDL", success: true, OperationType.Read, connectionInfo);
				result = text;
			}
			catch
			{
				AuditEvent.Default.Emit("export hierarchy to TMDL", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	internal static string ExportTMDLInternal(Database db, string tableName, string hierarchyName, ExportTmdl options)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(hierarchyName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("hierarchyName is required", ErrorSource.User);
		}
		return ExportContentProcessor.ProcessExportContent(TmdlSerializer.SerializeObject((db.Model.Tables.Find(tableName) ?? throw new ArgumentException("Table '" + tableName + "' not found")).Hierarchies.Find(hierarchyName) ?? throw new ArgumentException($"Hierarchy '{hierarchyName}' not found in table '{tableName}'"), options.SerializationOptions.ToMetadataSerializationOptions()), options).Content;
	}

	public static async Task<BatchOperationResponse> CreateHierarchies(string? connectionName, List<HierarchyDefinition> hierarchies, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, hierarchies, options, "Create", "Created", "hierarchies", (HierarchyDefinition item) => item.TableName + "." + item.Name, delegate(BatchItemContext<HierarchyDefinition> ctx)
		{
			HierarchyOperationResult hierarchyOperationResult = CreateHierarchyInternal(ctx.Connection, ctx.Item);
			string[] source = new string[3] { "Ready", "NoData", "CalculationNeeded" };
			ctx.Result.Success = Enumerable.Contains<string>(source, hierarchyOperationResult.State);
			ctx.Result.Message = (ctx.Result.Success ? $"Successfully created hierarchy '{ctx.Item.Name}' in table '{ctx.Item.TableName}'" : $"Failed to create hierarchy '{ctx.Item.Name}' in table '{ctx.Item.TableName}': State={hierarchyOperationResult.State}");
			if (ctx.Result.Success && ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, $"Created hierarchy '{ctx.Item.TableName}.{ctx.Item.Name}'");
			}
		});
	}

	public static async Task<BatchOperationResponse> UpdateHierarchies(string? connectionName, List<HierarchyDefinition> hierarchies, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, hierarchies, options, "Update", "Updated", "hierarchies", (HierarchyDefinition item) => item.TableName + "." + item.Name, delegate(BatchItemContext<HierarchyDefinition> ctx)
		{
			HierarchyOperationResult hierarchyOperationResult = UpdateHierarchyInternal(ctx.Connection, ctx.Item);
			ctx.Result.Success = true;
			ctx.Result.Data = hierarchyOperationResult;
			ctx.Result.Message = (hierarchyOperationResult.HasChanges ? $"Successfully updated hierarchy '{ctx.Item.Name}' in table '{ctx.Item.TableName}'" : $"Hierarchy '{ctx.Item.Name}' in table '{ctx.Item.TableName}' updated (no changes detected)");
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, $"Updated hierarchy '{ctx.Item.TableName}.{ctx.Item.Name}'");
			}
		});
	}

	public static async Task<BatchOperationResponse> DeleteHierarchies(string? connectionName, List<UserHierarchyReference> hierarchies, bool shouldCascadeDelete, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, hierarchies, options, "Delete", "Deleted", "hierarchies", (UserHierarchyReference item) => item.TableName + "." + item.HierarchyName, delegate(BatchItemContext<UserHierarchyReference> ctx)
		{
			DeleteHierarchyInternal(ctx.Connection, ctx.Item.TableName, ctx.Item.HierarchyName, shouldCascadeDelete);
			ctx.Result.Success = true;
			ctx.Result.Message = $"Successfully deleted hierarchy '{ctx.Item.HierarchyName}' from table '{ctx.Item.TableName}'";
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, $"Deleted hierarchy '{ctx.Item.TableName}.{ctx.Item.HierarchyName}'");
			}
		});
	}

	public static async Task<BatchOperationResponse> GetHierarchies(string? connectionName, List<UserHierarchyReference> hierarchies, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "Get",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (hierarchies == null || !hierarchies.Any())
		{
			response.Success = false;
			response.Message = "No hierarchies provided for retrieval";
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
				for (int i = 0; i < hierarchies.Count; i++)
				{
					UserHierarchyReference userHierarchyReference = hierarchies[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = userHierarchyReference.TableName + "." + userHierarchyReference.HierarchyName
					};
					try
					{
						HierarchyGet hierarchyInternal = GetHierarchyInternal(connectionInfo.Database, userHierarchyReference.TableName, userHierarchyReference.HierarchyName);
						itemResult.Success = true;
						itemResult.Message = $"Successfully retrieved hierarchy '{userHierarchyReference.HierarchyName}' from table '{userHierarchyReference.TableName}'";
						itemResult.Data = hierarchyInternal;
						successCount++;
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = $"Error retrieving hierarchy '{userHierarchyReference.HierarchyName}' from table '{userHierarchyReference.TableName}': {ex.Message}";
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
				response.Message = $"Retrieved {successCount} of {hierarchies.Count} hierarchies.";
			}
			catch (Exception ex2)
			{
				response.Success = false;
				response.Exceptions.Add(ex2);
				response.Message = "Get operation failed: " + ex2.Message;
				failureCount = hierarchies.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
				AuditEvent.Default.Emit("get hierarchies", response.Success, OperationType.Read, connectionInfo);
			}
			response.Summary = new BatchSummary
			{
				TotalItems = hierarchies.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> RenameHierarchies(string? connectionName, List<UserHierarchyRename> hierarchies, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, hierarchies, options, "Rename", "Renamed", "hierarchies", (UserHierarchyRename item) => item.TableName + "." + item.CurrentName, delegate(BatchItemContext<UserHierarchyRename> ctx)
		{
			RenameHierarchyInternal(ctx.Connection, ctx.Item.TableName, ctx.Item.CurrentName, ctx.Item.NewName);
			ctx.Result.Success = true;
			ctx.Result.Message = $"Successfully renamed hierarchy '{ctx.Item.CurrentName}' to '{ctx.Item.NewName}' in table '{ctx.Item.TableName}'";
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, $"Renamed hierarchy '{ctx.Item.TableName}.{ctx.Item.CurrentName}' to '{ctx.Item.NewName}'");
			}
		});
	}

	public static async Task<BatchOperationResponse> AddLevels(string? connectionName, List<HierarchyLevelDefinition> levels, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, levels, options, "AddLevel", "Added", "levels", (HierarchyLevelDefinition item) => $"{item.TableName}.{item.HierarchyName}.{item.Name}", delegate(BatchItemContext<HierarchyLevelDefinition> ctx)
		{
			LevelDefinition levelDef = new LevelDefinition
			{
				Name = ctx.Item.Name,
				ColumnName = ctx.Item.ColumnName,
				Description = ctx.Item.Description,
				Ordinal = ctx.Item.Ordinal,
				LineageTag = ctx.Item.LineageTag,
				SourceLineageTag = ctx.Item.SourceLineageTag,
				Annotations = ctx.Item.Annotations,
				ExtendedProperties = ctx.Item.ExtendedProperties
			};
			AddLevelInternal(ctx.Connection, ctx.Item.TableName, ctx.Item.HierarchyName, levelDef);
			ctx.Result.Success = true;
			ctx.Result.Message = $"Successfully added level '{ctx.Item.Name}' to hierarchy '{ctx.Item.HierarchyName}' in table '{ctx.Item.TableName}'";
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, $"Added level '{ctx.Item.Name}' to hierarchy '{ctx.Item.TableName}.{ctx.Item.HierarchyName}'");
			}
		});
	}

	public static async Task<BatchOperationResponse> RemoveLevels(string? connectionName, List<HierarchyLevelReference> levels, bool shouldCascadeDelete, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, levels, options, "RemoveLevel", "Removed", "levels", (HierarchyLevelReference item) => $"{item.TableName}.{item.HierarchyName}.{item.LevelName}", delegate(BatchItemContext<HierarchyLevelReference> ctx)
		{
			RemoveLevelInternal(ctx.Connection, ctx.Item.TableName, ctx.Item.HierarchyName, ctx.Item.LevelName, shouldCascadeDelete);
			ctx.Result.Success = true;
			ctx.Result.Message = $"Successfully removed level '{ctx.Item.LevelName}' from hierarchy '{ctx.Item.HierarchyName}' in table '{ctx.Item.TableName}'";
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, $"Removed level '{ctx.Item.LevelName}' from hierarchy '{ctx.Item.TableName}.{ctx.Item.HierarchyName}'");
			}
		});
	}

	public static async Task<BatchOperationResponse> UpdateLevels(string? connectionName, List<HierarchyLevelDefinition> levels, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, levels, options, "UpdateLevel", "Updated", "levels", (HierarchyLevelDefinition item) => $"{item.TableName}.{item.HierarchyName}.{item.Name}", delegate(BatchItemContext<HierarchyLevelDefinition> ctx)
		{
			LevelDefinition update = new LevelDefinition
			{
				Name = ctx.Item.Name,
				ColumnName = ctx.Item.ColumnName,
				Description = ctx.Item.Description,
				Ordinal = ctx.Item.Ordinal,
				LineageTag = ctx.Item.LineageTag,
				SourceLineageTag = ctx.Item.SourceLineageTag,
				Annotations = ctx.Item.Annotations,
				ExtendedProperties = ctx.Item.ExtendedProperties
			};
			UpdateLevelInternal(ctx.Connection, ctx.Item.TableName, ctx.Item.HierarchyName, ctx.Item.Name, update);
			ctx.Result.Success = true;
			ctx.Result.Message = $"Successfully updated level '{ctx.Item.Name}' in hierarchy '{ctx.Item.HierarchyName}' in table '{ctx.Item.TableName}'";
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, $"Updated level '{ctx.Item.Name}' in hierarchy '{ctx.Item.TableName}.{ctx.Item.HierarchyName}'");
			}
		});
	}

	public static async Task<BatchOperationResponse> RenameLevels(string? connectionName, List<HierarchyLevelRenameDefinition> levels, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, levels, options, "RenameLevel", "Renamed", "levels", (HierarchyLevelRenameDefinition item) => $"{item.TableName}.{item.HierarchyName}.{item.CurrentLevelName}", delegate(BatchItemContext<HierarchyLevelRenameDefinition> ctx)
		{
			RenameLevelInternal(ctx.Connection, ctx.Item.TableName, ctx.Item.HierarchyName, ctx.Item.CurrentLevelName, ctx.Item.NewLevelName);
			ctx.Result.Success = true;
			ctx.Result.Message = $"Successfully renamed level '{ctx.Item.CurrentLevelName}' to '{ctx.Item.NewLevelName}' in hierarchy '{ctx.Item.HierarchyName}' in table '{ctx.Item.TableName}'";
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, $"Renamed level '{ctx.Item.CurrentLevelName}' to '{ctx.Item.NewLevelName}' in hierarchy '{ctx.Item.TableName}.{ctx.Item.HierarchyName}'");
			}
		});
	}

	public static async Task<BatchOperationResponse> ReorderLevelsBatch(string? connectionName, List<UserHierarchyReorderLevels> reorderings, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "ReorderLevels",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (reorderings == null || !reorderings.Any())
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
				for (int i = 0; i < reorderings.Count; i++)
				{
					UserHierarchyReorderLevels userHierarchyReorderLevels = reorderings[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = userHierarchyReorderLevels.TableName + "." + userHierarchyReorderLevels.HierarchyName
					};
					try
					{
						ReorderLevelsInternal(connectionInfo, userHierarchyReorderLevels.TableName, userHierarchyReorderLevels.HierarchyName, userHierarchyReorderLevels.LevelNamesInOrder);
						itemResult.Success = true;
						itemResult.Message = $"Successfully reordered levels in hierarchy '{userHierarchyReorderLevels.HierarchyName}' in table '{userHierarchyReorderLevels.TableName}'";
						successCount++;
						if (transactionId != null)
						{
							TransactionOperations.RecordOperation(connectionInfo, $"Reordered levels in hierarchy '{userHierarchyReorderLevels.TableName}.{userHierarchyReorderLevels.HierarchyName}'");
						}
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = $"Error reordering levels in hierarchy '{userHierarchyReorderLevels.HierarchyName}' in table '{userHierarchyReorderLevels.TableName}': {ex.Message}";
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				BatchTransactionHelper.FinalizeBatchTransaction(connectionInfo, response, transactionId, ownsTransaction, reorderings.Count, ref successCount, ref failureCount, "Reordered levels in", "hierarchies");
			}
			catch (Exception ex2)
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
				response.Message = "ReorderLevels operation failed: " + ex2.Message;
				failureCount = reorderings.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
			}
			response.Summary = new BatchSummary
			{
				TotalItems = reorderings.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static void ValidateHierarchyDefinition(HierarchyBase def, bool isCreate)
	{
		if (def == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Hierarchy definition cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(def.TableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("TableName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(def.Name))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Name is required", ErrorSource.User);
		}
		if (isCreate && def is HierarchyDefinition hierarchyDefinition)
		{
			if (hierarchyDefinition.Levels == null || hierarchyDefinition.Levels.Count == 0)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("At least one level must be specified when creating a hierarchy", ErrorSource.User);
			}
			bool num = hierarchyDefinition.Levels.Any((LevelDefinition l) => l.Ordinal.HasValue);
			bool flag = hierarchyDefinition.Levels.All((LevelDefinition l) => l.Ordinal.HasValue);
			if (num && !flag)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Either all levels must have ordinals specified or none. Mixed ordinals are not allowed.", ErrorSource.User);
			}
			foreach (LevelDefinition level in hierarchyDefinition.Levels)
			{
				if (string.IsNullOrWhiteSpace(level.Name))
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage("Level name is required", ErrorSource.User);
				}
				if (string.IsNullOrWhiteSpace(level.ColumnName))
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage("ColumnName is required for level '" + level.Name + "'", ErrorSource.User);
				}
				if (level.ExtendedProperties != null)
				{
					List<string> list = ExtendedPropertyHelpers.Validate(level.ExtendedProperties);
					if (list.Count > 0)
					{
						throw McpExceptionWithSource.FromTelemetrySafeMessage("Level '" + level.Name + "' ExtendedProperties validation failed: " + string.Join(", ", list), ErrorSource.User);
					}
				}
				AnnotationHelpers.ValidateAnnotations(level.Annotations, "Level '" + level.Name + "'");
			}
			if (flag)
			{
				HashSet<int> hashSet = new HashSet<int>();
				foreach (LevelDefinition level2 in hierarchyDefinition.Levels)
				{
					if (level2.Ordinal.Value < 0)
					{
						throw McpExceptionWithSource.FromTelemetrySafeMessage("Ordinal must be non-negative for level '" + level2.Name + "'", ErrorSource.User);
					}
					if (!hashSet.Add(level2.Ordinal.Value))
					{
						throw McpExceptionWithSource.FromTelemetrySafeMessage($"Duplicate ordinal {level2.Ordinal.Value} found", ErrorSource.User);
					}
				}
				List<int> list2 = hashSet.OrderBy((int o) => o).ToList();
				int num2 = list2[0];
				if (num2 != 0 && num2 != 1)
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage("Level ordinals must start from 0 or 1", ErrorSource.User);
				}
				for (int num3 = 0; num3 < list2.Count; num3++)
				{
					if (list2[num3] != num2 + num3)
					{
						throw McpExceptionWithSource.FromTelemetrySafeMessage($"Level ordinals must be continuous. Missing ordinal {num2 + num3}", ErrorSource.User);
					}
				}
			}
		}
		if (!string.IsNullOrWhiteSpace(def.HideMembers) && !Enum.IsDefined(typeof(HierarchyHideMembersType), def.HideMembers))
		{
			string[] names = Enum.GetNames(typeof(HierarchyHideMembersType));
			throw new McpExceptionWithSource("Invalid HideMembers '" + def.HideMembers + "'. Valid values are: " + string.Join(", ", names), ErrorSource.User, "Invalid HideMembers supplied. Valid values are: " + string.Join(", ", names) + ".");
		}
		if (def.ExtendedProperties != null)
		{
			List<string> list3 = ExtendedPropertyHelpers.Validate(def.ExtendedProperties);
			if (list3.Count > 0)
			{
				throw new McpExceptionWithSource("ExtendedProperties validation failed: " + string.Join(", ", list3), ErrorSource.User, "ExtendedProperties validation failed.");
			}
		}
		AnnotationHelpers.ValidateAnnotations(def.Annotations);
	}
}
