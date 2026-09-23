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

public static class RelationshipOperations
{
	public static void ValidateRelationshipDefinition(RelationshipBase def, bool isCreate)
	{
		if (def == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Relationship definition cannot be null", ErrorSource.User);
		}
		if (isCreate)
		{
			if (string.IsNullOrWhiteSpace(def.FromTable))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("FromTable is required", ErrorSource.User);
			}
			if (string.IsNullOrWhiteSpace(def.FromColumn))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("FromColumn is required", ErrorSource.User);
			}
			if (string.IsNullOrWhiteSpace(def.ToTable))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("ToTable is required", ErrorSource.User);
			}
			if (string.IsNullOrWhiteSpace(def.ToColumn))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("ToColumn is required", ErrorSource.User);
			}
		}
		if (!string.IsNullOrWhiteSpace(def.Type) && !Enum.IsDefined(typeof(RelationshipType), def.Type))
		{
			string[] names = Enum.GetNames(typeof(RelationshipType));
			throw new McpExceptionWithSource("Invalid Type '" + def.Type + "'. Valid values are: " + string.Join(", ", names), ErrorSource.User, "Invalid Type supplied. Valid values are: " + string.Join(", ", names) + ".");
		}
		if (!string.IsNullOrWhiteSpace(def.CrossFilteringBehavior) && !Enum.IsDefined(typeof(CrossFilteringBehavior), def.CrossFilteringBehavior))
		{
			string[] names2 = Enum.GetNames(typeof(CrossFilteringBehavior));
			throw new McpExceptionWithSource("Invalid CrossFilteringBehavior '" + def.CrossFilteringBehavior + "'. Valid values are: " + string.Join(", ", names2), ErrorSource.User, "Invalid CrossFilteringBehavior supplied. Valid values are: " + string.Join(", ", names2) + ".");
		}
		if (!string.IsNullOrWhiteSpace(def.SecurityFilteringBehavior) && !Enum.IsDefined(typeof(SecurityFilteringBehavior), def.SecurityFilteringBehavior))
		{
			string[] names3 = Enum.GetNames(typeof(SecurityFilteringBehavior));
			throw new McpExceptionWithSource("Invalid SecurityFilteringBehavior '" + def.SecurityFilteringBehavior + "'. Valid values are: " + string.Join(", ", names3), ErrorSource.User, "Invalid SecurityFilteringBehavior supplied. Valid values are: " + string.Join(", ", names3) + ".");
		}
		if (!string.IsNullOrWhiteSpace(def.JoinOnDateBehavior) && !Enum.IsDefined(typeof(DateTimeRelationshipBehavior), def.JoinOnDateBehavior))
		{
			string[] names4 = Enum.GetNames(typeof(DateTimeRelationshipBehavior));
			throw new McpExceptionWithSource("Invalid JoinOnDateBehavior '" + def.JoinOnDateBehavior + "'. Valid values are: " + string.Join(", ", names4), ErrorSource.User, "Invalid JoinOnDateBehavior supplied. Valid values are: " + string.Join(", ", names4) + ".");
		}
		if (def.ExtendedProperties != null)
		{
			List<string> list = ExtendedPropertyHelpers.Validate(def.ExtendedProperties);
			if (list.Count > 0)
			{
				throw new McpExceptionWithSource("ExtendedProperties validation failed: " + string.Join(", ", list), ErrorSource.User, "ExtendedProperties validation failed.");
			}
		}
		AnnotationHelpers.ValidateAnnotations(def.Annotations);
	}

	public static async Task<List<RelationshipList>> ListRelationships(string? connectionName)
	{
		List<RelationshipList> result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				List<RelationshipList> list = ListRelationshipsInternal(connectionInfo.Database);
				AuditEvent.Default.Emit("list relationships", success: true, OperationType.Read, connectionInfo);
				result = list;
			}
			catch
			{
				AuditEvent.Default.Emit("list relationships", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	internal static List<RelationshipList> ListRelationshipsInternal(Database db)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		List<RelationshipList> list = new List<RelationshipList>();
		foreach (Relationship relationship in db.Model.Relationships)
		{
			if (relationship is SingleColumnRelationship singleColumnRelationship)
			{
				list.Add(new RelationshipList
				{
					Name = singleColumnRelationship.Name,
					FromTable = singleColumnRelationship.FromTable?.Name,
					FromColumn = singleColumnRelationship.FromColumn?.Name,
					ToTable = singleColumnRelationship.ToTable?.Name,
					ToColumn = singleColumnRelationship.ToColumn?.Name,
					IsActive = singleColumnRelationship.IsActive,
					CrossFilteringBehavior = singleColumnRelationship.CrossFilteringBehavior.ToString(),
					FromCardinality = singleColumnRelationship.FromCardinality.ToString(),
					ToCardinality = singleColumnRelationship.ToCardinality.ToString()
				});
			}
			else
			{
				list.Add(new RelationshipList
				{
					Name = relationship.Name,
					FromTable = relationship.FromTable?.Name,
					FromColumn = "[Multiple Columns]",
					ToTable = relationship.ToTable?.Name,
					ToColumn = "[Multiple Columns]",
					IsActive = relationship.IsActive,
					CrossFilteringBehavior = relationship.CrossFilteringBehavior.ToString()
				});
			}
		}
		return list;
	}

	public static async Task<RelationshipGet> GetRelationship(string? connectionName, string relationshipName)
	{
		if (string.IsNullOrWhiteSpace(relationshipName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("relationshipName is required", ErrorSource.User);
		}
		RelationshipGet relationshipInternal;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			relationshipInternal = GetRelationshipInternal(connectionInfo.Database, relationshipName);
		}
		return relationshipInternal;
	}

	internal static RelationshipGet GetRelationshipInternal(Database db, string relationshipName)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(relationshipName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("relationshipName is required", ErrorSource.User);
		}
		Relationship relationship = db.Model.Relationships.Find(relationshipName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Relationship '" + relationshipName + "' not found", ErrorSource.User);
		RelationshipGet relationshipGet = new RelationshipGet
		{
			Name = relationship.Name,
			IsActive = relationship.IsActive,
			Type = relationship.Type.ToString(),
			CrossFilteringBehavior = relationship.CrossFilteringBehavior.ToString(),
			JoinOnDateBehavior = relationship.JoinOnDateBehavior.ToString(),
			RelyOnReferentialIntegrity = relationship.RelyOnReferentialIntegrity,
			FromTable = relationship.FromTable?.Name,
			ToTable = relationship.ToTable?.Name,
			SecurityFilteringBehavior = relationship.SecurityFilteringBehavior.ToString(),
			State = relationship.State.ToString(),
			Annotations = new List<KeyValuePair<string, string>>(),
			ExtendedProperties = new List<PowerBIModelingMCP.Library.Common.DataStructures.ExtendedProperty>()
		};
		if (relationship is SingleColumnRelationship singleColumnRelationship)
		{
			relationshipGet.FromColumn = singleColumnRelationship.FromColumn?.Name;
			relationshipGet.ToColumn = singleColumnRelationship.ToColumn?.Name;
			relationshipGet.FromCardinality = singleColumnRelationship.FromCardinality.ToString();
			relationshipGet.ToCardinality = singleColumnRelationship.ToCardinality.ToString();
		}
		else
		{
			relationshipGet.FromColumn = "[Multiple Columns]";
			relationshipGet.ToColumn = "[Multiple Columns]";
		}
		foreach (Annotation annotation in relationship.Annotations)
		{
			relationshipGet.Annotations.Add(new KeyValuePair<string, string>(annotation.Name, annotation.Value));
		}
		relationshipGet.ExtendedProperties = ExtendedPropertyHelpers.ExtractFromRelationship(relationship);
		return relationshipGet;
	}

	public static async Task<string> ExportTMDL(string? connectionName, string relationshipName, ExportTmdl options)
	{
		if (string.IsNullOrWhiteSpace(relationshipName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("relationshipName is required", ErrorSource.User);
		}
		string result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				string item = ExportContentProcessor.ProcessExportContent(TmdlSerializer.SerializeObject(connectionInfo.Database.Model.Relationships.Find(relationshipName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Relationship '" + relationshipName + "' not found", ErrorSource.User), options.SerializationOptions.ToMetadataSerializationOptions()), options).Content;
				AuditEvent.Default.Emit("export relationship to TMDL", success: true, OperationType.Read, connectionInfo);
				result = item;
			}
			catch
			{
				AuditEvent.Default.Emit("export relationship to TMDL", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	public static async Task<RelationshipOperationResult> CreateRelationship(string? connectionName, RelationshipDefinition def)
	{
		ValidateRelationshipDefinition(def, isCreate: true);
		RelationshipOperationResult result;
		await using (IConnectionInfo info = await ConnectionOperations.GetAsync(connectionName))
		{
			result = CreateRelationshipInternal(info, def);
		}
		return result;
	}

	internal static RelationshipOperationResult CreateRelationshipInternal(IConnectionInfo info, RelationshipDefinition def)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (def == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("RelationshipDefinition cannot be null", ErrorSource.User);
		}
		Database database = info.Database;
		Table table = database.Model.Tables.Find(def.FromTable) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("From table '" + def.FromTable + "' not found", ErrorSource.User);
		Column fromColumn = table.Columns.Find(def.FromColumn) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"From column '{def.FromColumn}' not found in table '{def.FromTable}'", ErrorSource.User);
		Table table2 = database.Model.Tables.Find(def.ToTable) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("To table '" + def.ToTable + "' not found", ErrorSource.User);
		Column toColumn = table2.Columns.Find(def.ToColumn) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"To column '{def.ToColumn}' not found in table '{def.ToTable}'", ErrorSource.User);
		List<string> list = new List<string>();
		var (finalFromTable, finalFromColumn, finalToTable, finalToColumn) = ValidateAndFixCardinality(def, table, fromColumn, table2, toColumn, list);
		if (database.Model.Relationships.OfType<SingleColumnRelationship>().FirstOrDefault((SingleColumnRelationship r) => (r.FromTable == finalFromTable && r.FromColumn == finalFromColumn && r.ToTable == finalToTable && r.ToColumn == finalToColumn) || (r.FromTable == finalToTable && r.FromColumn == finalToColumn && r.ToTable == finalFromTable && r.ToColumn == finalFromColumn)) != null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"A relationship already exists between {finalFromTable.Name}[{finalFromColumn.Name}] and {finalToTable.Name}[{finalToColumn.Name}]", ErrorSource.User);
		}
		bool num = string.IsNullOrEmpty(def.Name);
		SingleColumnRelationship singleColumnRelationship = new SingleColumnRelationship
		{
			Name = (def.Name ?? $"{finalFromTable.Name}_{finalFromColumn.Name}_{finalToTable.Name}_{finalToColumn.Name}"),
			FromColumn = finalFromColumn,
			ToColumn = finalToColumn
		};
		if (num)
		{
			list.Insert(0, "Relationship name was auto-generated as '" + singleColumnRelationship.Name + "' based on table and column names");
		}
		ApplyRelationshipProperties(singleColumnRelationship, def, database);
		if (singleColumnRelationship.IsActive && database.Model.Relationships.Count((Relationship r) => r.IsActive && ((r.FromTable == finalFromTable && r.ToTable == finalToTable) || (r.FromTable == finalToTable && r.ToTable == finalFromTable))) > 0)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"An active relationship already exists between tables '{finalFromTable.Name}' and '{finalToTable.Name}'. " + "Only one active relationship is allowed between two tables. Set IsActive to false or deactivate the existing relationship first.", ErrorSource.User);
		}
		database.Model.Relationships.Add(singleColumnRelationship);
		TransactionOperations.RecordOperation(info, $"Created relationship '{singleColumnRelationship.Name}' from {finalFromTable.Name}[{finalFromColumn.Name}] to {finalToTable.Name}[{finalToColumn.Name}]");
		ConnectionOperations.SaveChangesWithRollback(info, "create relationship", OperationType.Create);
		return new RelationshipOperationResult
		{
			State = singleColumnRelationship.State.ToString(),
			RelationshipName = singleColumnRelationship.Name,
			FromTable = finalFromTable.Name,
			FromColumn = finalFromColumn.Name,
			ToTable = finalToTable.Name,
			ToColumn = finalToColumn.Name,
			Warnings = ((list.Count > 0) ? list : null)
		};
	}

	public static async Task<RelationshipOperationResult> UpdateRelationship(string? connectionName, RelationshipDefinition update)
	{
		ValidateRelationshipDefinition(update, isCreate: false);
		if (string.IsNullOrWhiteSpace(update.Name))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Name is required to identify the relationship to update", ErrorSource.User);
		}
		RelationshipOperationResult result;
		await using (IConnectionInfo info = await ConnectionOperations.GetAsync(connectionName))
		{
			result = UpdateRelationshipInternal(info, update);
		}
		return result;
	}

	internal static RelationshipOperationResult UpdateRelationshipInternal(IConnectionInfo info, RelationshipDefinition update)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (update == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("RelationshipDefinition cannot be null", ErrorSource.User);
		}
		Database database = info.Database;
		if (!((database.Model.Relationships.Find(update.Name) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Relationship '" + update.Name + "' not found", ErrorSource.User)) is SingleColumnRelationship singleColumnRelationship))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Relationship '" + update.Name + "' is not a SingleColumnRelationship", ErrorSource.User);
		}
		if (!ApplyRelationshipUpdates(singleColumnRelationship, update, database))
		{
			return new RelationshipOperationResult
			{
				State = singleColumnRelationship.State.ToString(),
				RelationshipName = singleColumnRelationship.Name,
				FromTable = (singleColumnRelationship.FromTable?.Name ?? ""),
				FromColumn = (singleColumnRelationship.FromColumn?.Name ?? ""),
				ToTable = (singleColumnRelationship.ToTable?.Name ?? ""),
				ToColumn = (singleColumnRelationship.ToColumn?.Name ?? ""),
				HasChanges = false
			};
		}
		TransactionOperations.RecordOperation(info, "Updated relationship '" + update.Name + "'");
		ConnectionOperations.SaveChangesWithRollback(info, "update relationship", OperationType.Update);
		return new RelationshipOperationResult
		{
			State = singleColumnRelationship.State.ToString(),
			RelationshipName = singleColumnRelationship.Name,
			FromTable = (singleColumnRelationship.FromTable?.Name ?? ""),
			FromColumn = (singleColumnRelationship.FromColumn?.Name ?? ""),
			ToTable = (singleColumnRelationship.ToTable?.Name ?? ""),
			ToColumn = (singleColumnRelationship.ToColumn?.Name ?? ""),
			HasChanges = true
		};
	}

	public static async Task DeleteRelationship(string? connectionName, string relationshipName)
	{
		if (string.IsNullOrWhiteSpace(relationshipName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("relationshipName is required", ErrorSource.User);
		}
		await using IConnectionInfo info = await ConnectionOperations.GetAsync(connectionName);
		DeleteRelationshipInternal(info, relationshipName);
	}

	internal static void DeleteRelationshipInternal(IConnectionInfo info, string relationshipName)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(relationshipName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("relationshipName is required", ErrorSource.User);
		}
		Database database = info.Database;
		Relationship metadataObject = database.Model.Relationships.Find(relationshipName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Relationship '" + relationshipName + "' not found", ErrorSource.User);
		database.Model.Relationships.Remove(metadataObject);
		TransactionOperations.RecordOperation(info, "Deleted relationship '" + relationshipName + "'");
		ConnectionOperations.SaveChangesWithRollback(info, "delete relationship", OperationType.Delete);
	}

	public static async Task RenameRelationship(string? connectionName, string oldName, string newName)
	{
		if (string.IsNullOrWhiteSpace(oldName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("oldName is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(newName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("newName is required", ErrorSource.User);
		}
		await using IConnectionInfo info = await ConnectionOperations.GetAsync(connectionName);
		RenameRelationshipInternal(info, oldName, newName);
	}

	internal static void RenameRelationshipInternal(IConnectionInfo info, string oldName, string newName)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
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
		Relationship relationship = database.Model.Relationships.Find(oldName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Relationship '" + oldName + "' not found", ErrorSource.User);
		if (database.Model.Relationships.Contains(newName) && !string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Relationship '" + newName + "' already exists", ErrorSource.User);
		}
		relationship.RequestRename(newName);
		TransactionOperations.RecordOperation(info, $"Renamed relationship '{oldName}' to '{newName}'");
		ConnectionOperations.SaveChangesWithRollback(info, "rename relationship", OperationType.Update, CheckpointMode.AfterRequestRename);
	}

	public static async Task<RelationshipOperationResult> ActivateRelationship(string? connectionName, string relationshipName)
	{
		if (string.IsNullOrWhiteSpace(relationshipName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("relationshipName is required", ErrorSource.User);
		}
		RelationshipOperationResult result;
		await using (IConnectionInfo info = await ConnectionOperations.GetAsync(connectionName))
		{
			result = ActivateRelationshipInternal(info, relationshipName);
		}
		return result;
	}

	internal static RelationshipOperationResult ActivateRelationshipInternal(IConnectionInfo info, string relationshipName)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(relationshipName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("relationshipName is required", ErrorSource.User);
		}
		Database database = info.Database;
		Relationship rel = database.Model.Relationships.Find(relationshipName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Relationship '" + relationshipName + "' not found", ErrorSource.User);
		List<string> list = new List<string>();
		if (rel.IsActive)
		{
			string item = "Relationship '" + relationshipName + "' is already active";
			list.Add(item);
			return new RelationshipOperationResult
			{
				State = rel.State.ToString(),
				RelationshipName = relationshipName,
				FromTable = (rel.FromTable?.Name ?? ""),
				FromColumn = ((!(rel is SingleColumnRelationship singleColumnRelationship)) ? "" : (singleColumnRelationship.FromColumn?.Name ?? "")),
				ToTable = (rel.ToTable?.Name ?? ""),
				ToColumn = ((!(rel is SingleColumnRelationship singleColumnRelationship2)) ? "" : (singleColumnRelationship2.ToColumn?.Name ?? "")),
				HasChanges = false,
				Warnings = list
			};
		}
		if (database.Model.Relationships.Count((Relationship r) => r != rel && r.IsActive && ((r.FromTable == rel.FromTable && r.ToTable == rel.ToTable) || (r.FromTable == rel.ToTable && r.ToTable == rel.FromTable))) > 0)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Cannot activate relationship '" + relationshipName + "'. An active relationship already exists between these tables. Deactivate the existing relationship first.", ErrorSource.User);
		}
		rel.IsActive = true;
		TransactionOperations.RecordOperation(info, "Activated relationship '" + relationshipName + "'");
		ConnectionOperations.SaveChangesWithRollback(info, "activate relationship", OperationType.Update);
		return new RelationshipOperationResult
		{
			State = rel.State.ToString(),
			RelationshipName = relationshipName,
			FromTable = (rel.FromTable?.Name ?? ""),
			FromColumn = ((!(rel is SingleColumnRelationship singleColumnRelationship3)) ? "" : (singleColumnRelationship3.FromColumn?.Name ?? "")),
			ToTable = (rel.ToTable?.Name ?? ""),
			ToColumn = ((!(rel is SingleColumnRelationship singleColumnRelationship4)) ? "" : (singleColumnRelationship4.ToColumn?.Name ?? "")),
			HasChanges = true,
			Warnings = ((list.Count > 0) ? list : null)
		};
	}

	public static async Task<RelationshipOperationResult> DeactivateRelationship(string? connectionName, string relationshipName)
	{
		if (string.IsNullOrWhiteSpace(relationshipName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("relationshipName is required", ErrorSource.User);
		}
		RelationshipOperationResult result;
		await using (IConnectionInfo info = await ConnectionOperations.GetAsync(connectionName))
		{
			result = DeactivateRelationshipInternal(info, relationshipName);
		}
		return result;
	}

	internal static RelationshipOperationResult DeactivateRelationshipInternal(IConnectionInfo info, string relationshipName)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo cannot be null");
		}
		if (string.IsNullOrWhiteSpace(relationshipName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("relationshipName is required", ErrorSource.User);
		}
		Relationship relationship = info.Database.Model.Relationships.Find(relationshipName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Relationship '" + relationshipName + "' not found", ErrorSource.User);
		List<string> list = new List<string>();
		if (!relationship.IsActive)
		{
			string item = "Relationship '" + relationshipName + "' is already inactive";
			list.Add(item);
			return new RelationshipOperationResult
			{
				State = relationship.State.ToString(),
				RelationshipName = relationshipName,
				FromTable = (relationship.FromTable?.Name ?? ""),
				FromColumn = ((!(relationship is SingleColumnRelationship singleColumnRelationship)) ? "" : (singleColumnRelationship.FromColumn?.Name ?? "")),
				ToTable = (relationship.ToTable?.Name ?? ""),
				ToColumn = ((!(relationship is SingleColumnRelationship singleColumnRelationship2)) ? "" : (singleColumnRelationship2.ToColumn?.Name ?? "")),
				HasChanges = false,
				Warnings = list
			};
		}
		relationship.IsActive = false;
		TransactionOperations.RecordOperation(info, "Deactivated relationship '" + relationshipName + "'");
		ConnectionOperations.SaveChangesWithRollback(info, "deactivate relationship", OperationType.Update);
		return new RelationshipOperationResult
		{
			State = relationship.State.ToString(),
			RelationshipName = relationshipName,
			FromTable = (relationship.FromTable?.Name ?? ""),
			FromColumn = ((!(relationship is SingleColumnRelationship singleColumnRelationship3)) ? "" : (singleColumnRelationship3.FromColumn?.Name ?? "")),
			ToTable = (relationship.ToTable?.Name ?? ""),
			ToColumn = ((!(relationship is SingleColumnRelationship singleColumnRelationship4)) ? "" : (singleColumnRelationship4.ToColumn?.Name ?? "")),
			HasChanges = true,
			Warnings = ((list.Count > 0) ? list : null)
		};
	}

	public static async Task<List<string>> FindRelationshipsForTable(string? connectionName, string tableName)
	{
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		List<string> result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				List<string> list = FindRelationshipsForTableInternal(connectionInfo.Database, tableName);
				AuditEvent.Default.Emit("find relationships", success: true, OperationType.Read, connectionInfo);
				result = list;
			}
			catch
			{
				AuditEvent.Default.Emit("find relationships", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	internal static List<string> FindRelationshipsForTableInternal(Database db, string tableName)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("tableName is required", ErrorSource.User);
		}
		Table table = db.Model.Tables.Find(tableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + tableName + "' not found", ErrorSource.User);
		List<string> list = new List<string>();
		foreach (Relationship relationship in db.Model.Relationships)
		{
			if (relationship.FromTable == table || relationship.ToTable == table)
			{
				list.Add(relationship.Name);
			}
		}
		return list;
	}

	public static async Task<BatchOperationResponse> CreateRelationships(string? connectionName, List<RelationshipDefinition> relationships, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, relationships, options, "Create", "Created", "relationships", (RelationshipDefinition item) => item.Name ?? $"{item.FromTable}.{item.FromColumn} -> {item.ToTable}.{item.ToColumn}", delegate(BatchItemContext<RelationshipDefinition> ctx)
		{
			RelationshipOperationResult relationshipOperationResult = CreateRelationshipInternal(ctx.Connection, ctx.Item);
			ctx.Result.Success = true;
			ctx.Result.Message = "Successfully created relationship '" + relationshipOperationResult.RelationshipName + "'";
			ctx.Result.ItemIdentifier = relationshipOperationResult.RelationshipName;
			if (relationshipOperationResult.Warnings != null && relationshipOperationResult.Warnings.Any())
			{
				ctx.Result.Warnings = relationshipOperationResult.Warnings;
			}
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, "Created relationship '" + relationshipOperationResult.RelationshipName + "'");
			}
		});
	}

	public static async Task<BatchOperationResponse> UpdateRelationships(string? connectionName, List<RelationshipDefinition> relationships, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, relationships, options, "Update", "Updated", "relationships", (RelationshipDefinition item) => item.Name ?? "[unnamed]", delegate(BatchItemContext<RelationshipDefinition> ctx)
		{
			RelationshipOperationResult relationshipOperationResult = UpdateRelationshipInternal(ctx.Connection, ctx.Item);
			ctx.Result.Success = true;
			ctx.Result.Message = (relationshipOperationResult.HasChanges ? ("Successfully updated relationship '" + ctx.Item.Name + "'") : ("No changes made to relationship '" + ctx.Item.Name + "'"));
			ctx.Result.ItemIdentifier = ctx.Item.Name;
			if (ctx.TransactionId != null && relationshipOperationResult.HasChanges)
			{
				TransactionOperations.RecordOperation(ctx.Connection, "Updated relationship '" + ctx.Item.Name + "'");
			}
		});
	}

	public static async Task<BatchOperationResponse> DeleteRelationships(string? connectionName, List<RelationshipReference> relationships, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, relationships, options, "Delete", "Deleted", "relationships", (RelationshipReference item) => item.Name, delegate(BatchItemContext<RelationshipReference> ctx)
		{
			DeleteRelationshipInternal(ctx.Connection, ctx.Item.Name);
			ctx.Result.Success = true;
			ctx.Result.Message = "Successfully deleted relationship '" + ctx.Item.Name + "'";
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, "Deleted relationship '" + ctx.Item.Name + "'");
			}
		});
	}

	public static async Task<BatchOperationResponse> GetRelationships(string? connectionName, List<RelationshipReference> relationships, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "Get",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (relationships == null || !relationships.Any())
		{
			response.Success = false;
			response.Message = "No relationships provided for retrieval";
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
				for (int i = 0; i < relationships.Count; i++)
				{
					RelationshipReference relationshipReference = relationships[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = relationshipReference.Name
					};
					try
					{
						RelationshipGet relationshipInternal = GetRelationshipInternal(connectionInfo.Database, relationshipReference.Name);
						itemResult.Success = true;
						itemResult.Message = "Successfully retrieved relationship '" + relationshipReference.Name + "'";
						itemResult.Data = relationshipInternal;
						successCount++;
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error retrieving relationship '" + relationshipReference.Name + "': " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				stopwatch.Stop();
				response.Success = failureCount == 0;
				response.Message = ((failureCount == 0) ? $"Successfully retrieved {successCount} relationships" : $"Retrieved {successCount} of {relationships.Count} relationships. {failureCount} failed.");
				response.Summary = new BatchSummary
				{
					TotalItems = relationships.Count,
					SuccessCount = successCount,
					FailureCount = failureCount,
					ExecutionTime = stopwatch.Elapsed
				};
			}
			catch (Exception ex2)
			{
				response.Success = false;
				response.Exceptions.Add(ex2);
				response.Message = "Batch operation failed: " + ex2.Message;
				response.Summary = new BatchSummary
				{
					TotalItems = relationships.Count,
					SuccessCount = successCount,
					FailureCount = failureCount,
					ExecutionTime = stopwatch.Elapsed
				};
			}
			finally
			{
				AuditEvent.Default.Emit("get relationships", response.Success, OperationType.Read, connectionInfo);
			}
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> RenameRelationships(string? connectionName, List<RelationshipRename> relationships, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, relationships, options, "Rename", "Renamed", "relationships", (RelationshipRename item) => item.CurrentName ?? "[unnamed]", delegate(BatchItemContext<RelationshipRename> ctx)
		{
			RenameRelationshipInternal(ctx.Connection, ctx.Item.CurrentName, ctx.Item.NewName);
			ctx.Result.Success = true;
			ctx.Result.Message = $"Successfully renamed relationship '{ctx.Item.CurrentName}' to '{ctx.Item.NewName}'";
			ctx.Result.ItemIdentifier = ctx.Item.NewName;
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, $"Renamed relationship '{ctx.Item.CurrentName}' to '{ctx.Item.NewName}'");
			}
		});
	}

	private static void ApplyRelationshipProperties(SingleColumnRelationship relationship, RelationshipBase def, Database db)
	{
		relationship.IsActive = def.IsActive ?? true;
		if (!string.IsNullOrWhiteSpace(def.CrossFilteringBehavior))
		{
			if (Enum.TryParse<CrossFilteringBehavior>(def.CrossFilteringBehavior, ignoreCase: true, out var result))
			{
				relationship.CrossFilteringBehavior = result;
			}
		}
		else
		{
			relationship.CrossFilteringBehavior = CrossFilteringBehavior.OneDirection;
		}
		if (!string.IsNullOrWhiteSpace(def.JoinOnDateBehavior) && Enum.TryParse<DateTimeRelationshipBehavior>(def.JoinOnDateBehavior, ignoreCase: true, out var result2))
		{
			relationship.JoinOnDateBehavior = result2;
		}
		relationship.RelyOnReferentialIntegrity = def.RelyOnReferentialIntegrity == true;
		if (!string.IsNullOrWhiteSpace(def.FromCardinality) && Enum.TryParse<RelationshipEndCardinality>(def.FromCardinality, ignoreCase: true, out var result3))
		{
			relationship.FromCardinality = result3;
		}
		if (!string.IsNullOrWhiteSpace(def.ToCardinality) && Enum.TryParse<RelationshipEndCardinality>(def.ToCardinality, ignoreCase: true, out var result4))
		{
			relationship.ToCardinality = result4;
		}
		if (!string.IsNullOrWhiteSpace(def.SecurityFilteringBehavior))
		{
			if (Enum.TryParse<SecurityFilteringBehavior>(def.SecurityFilteringBehavior, ignoreCase: true, out var result5))
			{
				relationship.SecurityFilteringBehavior = result5;
			}
		}
		else
		{
			relationship.SecurityFilteringBehavior = SecurityFilteringBehavior.OneDirection;
		}
		if (def.Annotations != null)
		{
			foreach (KeyValuePair<string, string> annotation in def.Annotations)
			{
				relationship.Annotations.Add(new Annotation
				{
					Name = annotation.Key,
					Value = annotation.Value
				});
			}
		}
		if (def.ExtendedProperties != null)
		{
			ExtendedPropertyHelpers.ApplyToRelationship(relationship, def.ExtendedProperties);
		}
	}

	private static bool ApplyRelationshipUpdates(SingleColumnRelationship relationship, RelationshipDefinition update, Database db)
	{
		bool result = false;
		if (update.IsActive.HasValue && relationship.IsActive != update.IsActive.Value)
		{
			if (update.IsActive.Value && db.Model.Relationships.Count((Relationship r) => r != relationship && r.IsActive && ((r.FromTable == relationship.FromTable && r.ToTable == relationship.ToTable) || (r.FromTable == relationship.ToTable && r.ToTable == relationship.FromTable))) > 0)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Cannot activate relationship '" + update.Name + "'. An active relationship already exists between these tables. Deactivate the existing relationship first.", ErrorSource.User);
			}
			relationship.IsActive = update.IsActive.Value;
			result = true;
		}
		if (!string.IsNullOrWhiteSpace(update.Type))
		{
			if (!Enum.TryParse<RelationshipType>(update.Type, ignoreCase: true, out var result2))
			{
				string[] names = Enum.GetNames(typeof(RelationshipType));
				throw new McpExceptionWithSource("Invalid Type '" + update.Type + "'. Valid values are: " + string.Join(", ", names), ErrorSource.User, "Invalid Type supplied. Valid values are: " + string.Join(", ", names) + ".");
			}
			if (relationship.Type != result2)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage($"Cannot change the Type of an existing relationship from '{relationship.Type}' to '{result2}'. Delete and recreate the relationship instead.", ErrorSource.User);
			}
		}
		if (!string.IsNullOrWhiteSpace(update.CrossFilteringBehavior))
		{
			if (!Enum.TryParse<CrossFilteringBehavior>(update.CrossFilteringBehavior, ignoreCase: true, out var result3))
			{
				string[] names2 = Enum.GetNames(typeof(CrossFilteringBehavior));
				throw new McpExceptionWithSource("Invalid CrossFilteringBehavior '" + update.CrossFilteringBehavior + "'. Valid values are: " + string.Join(", ", names2), ErrorSource.User, "Invalid CrossFilteringBehavior supplied. Valid values are: " + string.Join(", ", names2) + ".");
			}
			if (relationship.CrossFilteringBehavior != result3)
			{
				relationship.CrossFilteringBehavior = result3;
				result = true;
			}
		}
		if (!string.IsNullOrWhiteSpace(update.JoinOnDateBehavior))
		{
			if (!Enum.TryParse<DateTimeRelationshipBehavior>(update.JoinOnDateBehavior, ignoreCase: true, out var result4))
			{
				string[] names3 = Enum.GetNames(typeof(DateTimeRelationshipBehavior));
				throw new McpExceptionWithSource("Invalid JoinOnDateBehavior '" + update.JoinOnDateBehavior + "'. Valid values are: " + string.Join(", ", names3), ErrorSource.User, "Invalid JoinOnDateBehavior supplied. Valid values are: " + string.Join(", ", names3) + ".");
			}
			if (relationship.JoinOnDateBehavior != result4)
			{
				relationship.JoinOnDateBehavior = result4;
				result = true;
			}
		}
		if (update.RelyOnReferentialIntegrity.HasValue && relationship.RelyOnReferentialIntegrity != update.RelyOnReferentialIntegrity.Value)
		{
			relationship.RelyOnReferentialIntegrity = update.RelyOnReferentialIntegrity.Value;
			result = true;
		}
		if (!string.IsNullOrWhiteSpace(update.FromCardinality))
		{
			if (!Enum.TryParse<RelationshipEndCardinality>(update.FromCardinality, ignoreCase: true, out var result5))
			{
				string[] names4 = Enum.GetNames(typeof(RelationshipEndCardinality));
				throw new McpExceptionWithSource("Invalid FromCardinality '" + update.FromCardinality + "'. Valid values are: " + string.Join(", ", names4), ErrorSource.User, "Invalid FromCardinality supplied. Valid values are: " + string.Join(", ", names4) + ".");
			}
			if (relationship.FromCardinality != result5)
			{
				relationship.FromCardinality = result5;
				result = true;
			}
		}
		if (!string.IsNullOrWhiteSpace(update.ToCardinality))
		{
			if (!Enum.TryParse<RelationshipEndCardinality>(update.ToCardinality, ignoreCase: true, out var result6))
			{
				string[] names5 = Enum.GetNames(typeof(RelationshipEndCardinality));
				throw new McpExceptionWithSource("Invalid ToCardinality '" + update.ToCardinality + "'. Valid values are: " + string.Join(", ", names5), ErrorSource.User, "Invalid ToCardinality supplied. Valid values are: " + string.Join(", ", names5) + ".");
			}
			if (relationship.ToCardinality != result6)
			{
				relationship.ToCardinality = result6;
				result = true;
			}
		}
		if (!string.IsNullOrWhiteSpace(update.SecurityFilteringBehavior))
		{
			if (!Enum.TryParse<SecurityFilteringBehavior>(update.SecurityFilteringBehavior, ignoreCase: true, out var result7))
			{
				string[] names6 = Enum.GetNames(typeof(SecurityFilteringBehavior));
				throw new McpExceptionWithSource("Invalid SecurityFilteringBehavior '" + update.SecurityFilteringBehavior + "'. Valid values are: " + string.Join(", ", names6), ErrorSource.User, "Invalid SecurityFilteringBehavior supplied. Valid values are: " + string.Join(", ", names6) + ".");
			}
			if (relationship.SecurityFilteringBehavior != result7)
			{
				relationship.SecurityFilteringBehavior = result7;
				result = true;
			}
		}
		if (!string.IsNullOrWhiteSpace(update.FromTable) || !string.IsNullOrWhiteSpace(update.FromColumn) || !string.IsNullOrWhiteSpace(update.ToTable) || !string.IsNullOrWhiteSpace(update.ToColumn))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Cannot change the tables or columns of an existing relationship. Delete and recreate the relationship instead.", ErrorSource.User);
		}
		if (update.Annotations != null)
		{
			relationship.Annotations.Clear();
			foreach (KeyValuePair<string, string> annotation in update.Annotations)
			{
				relationship.Annotations.Add(new Annotation
				{
					Name = annotation.Key,
					Value = annotation.Value
				});
			}
			result = true;
		}
		if (update.ExtendedProperties != null)
		{
			ExtendedPropertyHelpers.ReplaceRelationshipProperties(relationship, update.ExtendedProperties);
			result = true;
		}
		return result;
	}

	private static (Table fromTable, Column fromColumn, Table toTable, Column toColumn) ValidateAndFixCardinality(RelationshipDefinition def, Table fromTable, Column fromColumn, Table toTable, Column toColumn, List<string> warnings)
	{
		RelationshipEndCardinality result = RelationshipEndCardinality.Many;
		RelationshipEndCardinality result2 = RelationshipEndCardinality.One;
		if (!string.IsNullOrWhiteSpace(def.FromCardinality))
		{
			Enum.TryParse<RelationshipEndCardinality>(def.FromCardinality, ignoreCase: true, out result);
		}
		if (!string.IsNullOrWhiteSpace(def.ToCardinality))
		{
			Enum.TryParse<RelationshipEndCardinality>(def.ToCardinality, ignoreCase: true, out result2);
		}
		if (result == RelationshipEndCardinality.One && result2 == RelationshipEndCardinality.Many)
		{
			Table table = fromTable;
			Column column = fromColumn;
			fromTable = toTable;
			fromColumn = toColumn;
			toTable = table;
			toColumn = column;
			string fromTable2 = def.FromTable;
			string fromColumn2 = def.FromColumn;
			def.FromTable = def.ToTable;
			def.FromColumn = def.ToColumn;
			def.ToTable = fromTable2;
			def.ToColumn = fromColumn2;
			def.FromCardinality = "Many";
			def.ToCardinality = "One";
			warnings.Add($"Relationship direction was corrected: The 'from' side (many side) is now {def.FromTable}[{def.FromColumn}] and the 'to' side (one side) is now {def.ToTable}[{def.ToColumn}]. In Power BI relationships, the 'from' end cardinality should typically be 'Many' unless creating a one-to-one relationship.");
		}
		return (fromTable: fromTable, fromColumn: fromColumn, toTable: toTable, toColumn: toColumn);
	}
}
