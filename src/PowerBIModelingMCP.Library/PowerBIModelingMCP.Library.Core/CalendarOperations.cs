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

public static class CalendarOperations
{
	public static void ValidateCalendarBase(CalendarBase def, bool isCreate)
	{
		if (def == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Calendar definition cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(def.Name))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Name is required", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(def.TableName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("TableName is required", ErrorSource.User);
		}
	}

	public static void ValidateCalendarColumnGroupDefinition(CalendarColumnGroupDefinition def)
	{
		if (def == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Calendar column group definition cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(def.GroupType))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("GroupType is required", ErrorSource.User);
		}
		if (def.GroupType != "TimeRelated" && def.GroupType != "TimeUnitAssociation")
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("GroupType must be either 'TimeRelated' or 'TimeUnitAssociation'", ErrorSource.User);
		}
		if (def.GroupType == "TimeRelated")
		{
			if (def.TimeRelatedGroup == null)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("TimeRelatedGroup is required when GroupType is 'TimeRelated'", ErrorSource.User);
			}
			if (def.TimeRelatedGroup.Columns == null || def.TimeRelatedGroup.Columns.Count == 0)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("At least one column is required for TimeRelated column groups", ErrorSource.User);
			}
		}
		else if (def.GroupType == "TimeUnitAssociation")
		{
			if (def.TimeUnitAssociation == null)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("TimeUnitAssociation is required when GroupType is 'TimeUnitAssociation'", ErrorSource.User);
			}
			if (string.IsNullOrWhiteSpace(def.TimeUnitAssociation.TimeUnit))
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("TimeUnit is required for TimeUnitAssociation column groups", ErrorSource.User);
			}
		}
	}

	private static Calendar FindCalendar(Model model, string tableName, string calendarName)
	{
		return (model.Tables.Find(tableName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + tableName + "' not found in model", ErrorSource.User)).Calendars.Find(calendarName) ?? throw McpExceptionWithSource.FromTelemetrySafeMessage($"Calendar '{calendarName}' not found in table '{tableName}'", ErrorSource.User);
	}

	private static Table GetCalendarTable(Model model, string calendarName)
	{
		foreach (Table table in model.Tables)
		{
			if (table.Calendars.Find(calendarName) != null)
			{
				return table;
			}
		}
		throw McpExceptionWithSource.FromTelemetrySafeMessage("Calendar '" + calendarName + "' not found in any table", ErrorSource.User);
	}

	public static int ResolveColumnGroupIndex(Calendar calendar, string groupType, string? timeUnit)
	{
		if (string.IsNullOrWhiteSpace(groupType))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("GroupType is required", ErrorSource.User);
		}
		if (groupType != "TimeRelated" && groupType != "TimeUnitAssociation")
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("GroupType must be either 'TimeRelated' or 'TimeUnitAssociation'", ErrorSource.User);
		}
		for (int i = 0; i < calendar.CalendarColumnGroups.Count; i++)
		{
			CalendarColumnGroup calendarColumnGroup = calendar.CalendarColumnGroups[i];
			if (groupType == "TimeRelated" && calendarColumnGroup is TimeRelatedColumnGroup)
			{
				return i;
			}
			if (groupType == "TimeUnitAssociation" && calendarColumnGroup is TimeUnitColumnAssociation timeUnitColumnAssociation)
			{
				if (string.IsNullOrWhiteSpace(timeUnit))
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage("TimeUnit is required when GroupType is 'TimeUnitAssociation'", ErrorSource.User);
				}
				if (string.Equals(timeUnitColumnAssociation.TimeUnit.ToString(), timeUnit, StringComparison.OrdinalIgnoreCase))
				{
					return i;
				}
			}
		}
		if (groupType == "TimeRelated")
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("No TimeRelated column group found in calendar '" + calendar.Name + "'", ErrorSource.User);
		}
		throw new McpExceptionWithSource($"No TimeUnitAssociation column group with TimeUnit '{timeUnit}' found in calendar '{calendar.Name}'", ErrorSource.User, "No TimeUnitAssociation column group matching the supplied TimeUnit was found in calendar '" + calendar.Name + "'.");
	}

	public static (Calendar Calendar, Table Table, int Index) ResolveColumnGroupReference(Model model, CalendarColumnGroupDefinition reference)
	{
		if (reference == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Column group reference cannot be null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(reference.CalendarName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("CalendarName is required", ErrorSource.User);
		}
		Calendar calendar;
		Table table;
		if (!string.IsNullOrWhiteSpace(reference.TableName))
		{
			calendar = FindCalendar(model, reference.TableName, reference.CalendarName);
			table = model.Tables.Find(reference.TableName);
		}
		else
		{
			table = GetCalendarTable(model, reference.CalendarName);
			calendar = table.Calendars.Find(reference.CalendarName);
		}
		int item = ResolveColumnGroupIndex(calendar, reference.GroupType, reference.TimeUnit);
		return (Calendar: calendar, Table: table, Index: item);
	}

	public static async Task<List<CalendarList>> ListCalendars(string? connectionName, string? tableName)
	{
		List<CalendarList> result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				List<CalendarList> list = ListCalendarsInternal(connectionInfo.Database, tableName);
				AuditEvent.Default.Emit("list calendars", success: true, OperationType.Read, connectionInfo);
				result = list;
			}
			catch
			{
				AuditEvent.Default.Emit("list calendars", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	internal static List<CalendarList> ListCalendarsInternal(Database db, string? tableName)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database is null", ErrorSource.User);
		}
		Model model = db.Model;
		IEnumerable<(Table, Calendar)> source;
		if (!string.IsNullOrWhiteSpace(tableName))
		{
			Table table = model.Tables.Find(tableName);
			if (table == null)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + tableName + "' not found", ErrorSource.User);
			}
			source = table.Calendars.Select((Calendar c) => (table: table, c: c));
		}
		else
		{
			source = model.Tables.SelectMany((Table t) => t.Calendars.Select((Calendar c) => (t: t, c: c)));
		}
		return source.Select<(Table, Calendar), CalendarList>(((Table table, Calendar calendar) pair) => new CalendarList
		{
			Name = pair.calendar.Name,
			Description = ((!string.IsNullOrEmpty(pair.calendar.Description)) ? pair.calendar.Description : null),
			TableName = pair.table.Name,
			ColumnGroups = pair.calendar.CalendarColumnGroups.Select((CalendarColumnGroup cg) => ConvertColumnGroupToList(cg)).ToList()
		}).ToList();
	}

	internal static CalendarGet GetCalendarInternal(Database db, string calendarName, string? tableName = null)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database is null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(calendarName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("calendarName is required", ErrorSource.User);
		}
		Model model = db.Model;
		Calendar calendar;
		Table table;
		if (!string.IsNullOrWhiteSpace(tableName))
		{
			calendar = FindCalendar(model, tableName, calendarName);
			table = model.Tables.Find(tableName);
		}
		else
		{
			table = GetCalendarTable(model, calendarName);
			calendar = table.Calendars.Find(calendarName);
		}
		CalendarGet calendarGet = new CalendarGet
		{
			Name = calendar.Name,
			Description = calendar.Description,
			TableName = table.Name,
			LineageTag = calendar.LineageTag,
			SourceLineageTag = calendar.SourceLineageTag,
			ModifiedTime = calendar.ModifiedTime,
			CalendarColumnGroups = new List<CalendarColumnGroupDefinition>()
		};
		foreach (CalendarColumnGroup calendarColumnGroup in calendar.CalendarColumnGroups)
		{
			calendarGet.CalendarColumnGroups.Add(ConvertColumnGroupToDefinition(calendarColumnGroup, calendarName));
		}
		return calendarGet;
	}

	private static CalendarColumnGroupDefinition ConvertColumnGroupToDefinition(CalendarColumnGroup columnGroup, string calendarName)
	{
		if (columnGroup is TimeRelatedColumnGroup timeRelatedColumnGroup)
		{
			return new CalendarColumnGroupDefinition
			{
				CalendarName = calendarName,
				GroupType = "TimeRelated",
				ModifiedTime = timeRelatedColumnGroup.ModifiedTime,
				TimeRelatedGroup = new TimeRelatedColumnGroupInfo
				{
					Columns = timeRelatedColumnGroup.Columns.Select((Column c) => c.Name).ToList()
				}
			};
		}
		if (columnGroup is TimeUnitColumnAssociation timeUnitColumnAssociation)
		{
			return new CalendarColumnGroupDefinition
			{
				CalendarName = calendarName,
				GroupType = "TimeUnitAssociation",
				TimeUnit = timeUnitColumnAssociation.TimeUnit.ToString(),
				ModifiedTime = timeUnitColumnAssociation.ModifiedTime,
				TimeUnitAssociation = new TimeUnitColumnAssociationInfo
				{
					TimeUnit = timeUnitColumnAssociation.TimeUnit.ToString(),
					PrimaryColumnName = timeUnitColumnAssociation.PrimaryColumn?.Name,
					AssociatedColumns = timeUnitColumnAssociation.AssociatedColumns.Select((Column c) => c.Name).ToList()
				}
			};
		}
		throw McpExceptionWithSource.FromTelemetrySafeMessage("Unknown calendar column group type: " + columnGroup.GetType().Name, ErrorSource.User);
	}

	private static ColumnGroupList ConvertColumnGroupToList(CalendarColumnGroup columnGroup)
	{
		if (columnGroup is TimeRelatedColumnGroup timeRelatedColumnGroup)
		{
			return new ColumnGroupList
			{
				Name = $"TimeRelated-{timeRelatedColumnGroup.Columns.Count}cols",
				Description = $"Time-related column group with {timeRelatedColumnGroup.Columns.Count} columns",
				GroupType = "TimeRelated",
				ColumnNames = timeRelatedColumnGroup.Columns.Select((Column c) => c.Name).ToList(),
				PrimaryColumnName = null
			};
		}
		if (columnGroup is TimeUnitColumnAssociation timeUnitColumnAssociation)
		{
			return new ColumnGroupList
			{
				Name = $"{timeUnitColumnAssociation.TimeUnit}",
				Description = $"Time unit association for {timeUnitColumnAssociation.TimeUnit}",
				GroupType = "TimeUnitAssociation",
				ColumnNames = timeUnitColumnAssociation.AssociatedColumns.Select((Column c) => c.Name).ToList(),
				PrimaryColumnName = timeUnitColumnAssociation.PrimaryColumn?.Name
			};
		}
		throw McpExceptionWithSource.FromTelemetrySafeMessage("Unknown calendar column group type: " + columnGroup.GetType().Name, ErrorSource.User);
	}

	public static async Task<string> ExportTMDL(string? connectionName, string calendarName, string? tableName, ExportTmdl options)
	{
		if (string.IsNullOrWhiteSpace(calendarName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("calendarName is required", ErrorSource.User);
		}
		string result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				string text = ExportTMDLInternal(connectionInfo.Database, calendarName, tableName, options);
				AuditEvent.Default.Emit("export calendar to TMDL", success: true, OperationType.Read, connectionInfo);
				result = text;
			}
			catch
			{
				AuditEvent.Default.Emit("export calendar to TMDL", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	internal static string ExportTMDLInternal(Database db, string calendarName, string? tableName, ExportTmdl options)
	{
		if (db == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Database is null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(calendarName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("calendarName is required", ErrorSource.User);
		}
		Model model = db.Model;
		Calendar calendar = (string.IsNullOrWhiteSpace(tableName) ? GetCalendarTable(model, calendarName).Calendars.Find(calendarName) : FindCalendar(model, tableName, calendarName));
		if (calendar == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Calendar '" + calendarName + "' not found", ErrorSource.User);
		}
		return ExportContentProcessor.ProcessExportContent(TmdlSerializer.SerializeObject(calendar, options.SerializationOptions.ToMetadataSerializationOptions()), options).Content;
	}

	internal static CalendarOperationResult CreateCalendarInternal(IConnectionInfo info, CalendarDefinition def)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo is null", ErrorSource.User);
		}
		ValidateCalendarBase(def, isCreate: true);
		Table table = info.Database.Model.Tables.Find(def.TableName);
		if (table == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Table '" + def.TableName + "' not found in model", ErrorSource.User);
		}
		if (table.Calendars.Find(def.Name) != null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Calendar '{def.Name}' already exists in table '{def.TableName}'", ErrorSource.User);
		}
		Calendar calendar = new Calendar
		{
			Name = def.Name
		};
		if (!string.IsNullOrWhiteSpace(def.Description))
		{
			calendar.Description = def.Description;
		}
		calendar.LineageTag = (string.IsNullOrWhiteSpace(def.LineageTag) ? Guid.NewGuid().ToString() : def.LineageTag);
		if (!string.IsNullOrWhiteSpace(def.SourceLineageTag))
		{
			calendar.SourceLineageTag = def.SourceLineageTag;
		}
		table.Calendars.Add(calendar);
		List<CalendarColumnGroupOperationResult> list = new List<CalendarColumnGroupOperationResult>();
		if (def.InitialColumnGroups != null && def.InitialColumnGroups.Count > 0)
		{
			foreach (CalendarColumnGroupDefinition initialColumnGroup in def.InitialColumnGroups)
			{
				try
				{
					CalendarColumnGroupOperationResult item = CreateColumnGroupInternal(info, calendar.Name, table.Name, initialColumnGroup);
					list.Add(item);
				}
				catch (Exception ex) when (!(ex is McpExceptionWithSource))
				{
					throw new McpExceptionWithSource("Failed to create column group: " + ex.Message, ex, null, "Failed to create calendar column group; see inner error details.");
				}
			}
		}
		TransactionOperations.RecordOperation(info, $"Created calendar '{def.Name}' in table '{def.TableName}'");
		ConnectionOperations.SaveChangesWithRollback(info, "create calendar", OperationType.Create);
		return new CalendarOperationResult
		{
			CalendarName = calendar.Name,
			TableName = def.TableName,
			ColumnGroupCount = calendar.CalendarColumnGroups.Count,
			ColumnGroups = list
		};
	}

	internal static CalendarOperationResult UpdateCalendarInternal(IConnectionInfo info, CalendarDefinition update, string? tableName = null)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo is null", ErrorSource.User);
		}
		ValidateCalendarBase(update, isCreate: false);
		Model model = info.Database.Model;
		Calendar calendar;
		Table table;
		if (!string.IsNullOrWhiteSpace(tableName))
		{
			calendar = FindCalendar(model, tableName, update.Name);
			table = model.Tables.Find(tableName);
		}
		else
		{
			table = GetCalendarTable(model, update.Name);
			calendar = table.Calendars.Find(update.Name);
		}
		bool flag = false;
		if (update.Description != null)
		{
			string text = (string.IsNullOrEmpty(update.Description) ? null : update.Description);
			if (calendar.Description != text)
			{
				calendar.Description = text;
				flag = true;
			}
		}
		if (update.LineageTag != null)
		{
			string text2 = (string.IsNullOrEmpty(update.LineageTag) ? null : update.LineageTag);
			if (calendar.LineageTag != text2)
			{
				calendar.LineageTag = text2;
				flag = true;
			}
		}
		if (update.SourceLineageTag != null)
		{
			string text3 = (string.IsNullOrEmpty(update.SourceLineageTag) ? null : update.SourceLineageTag);
			if (calendar.SourceLineageTag != text3)
			{
				calendar.SourceLineageTag = text3;
				flag = true;
			}
		}
		if (flag)
		{
			TransactionOperations.RecordOperation(info, $"Updated calendar '{update.Name}' in table '{table.Name}'");
			ConnectionOperations.SaveChangesWithRollback(info, "update calendar", OperationType.Update);
		}
		return new CalendarOperationResult
		{
			CalendarName = calendar.Name,
			TableName = table.Name,
			ColumnGroupCount = calendar.CalendarColumnGroups.Count,
			ColumnGroups = calendar.CalendarColumnGroups.Select((CalendarColumnGroup cg, int index) => CreateColumnGroupOperationResult(cg, calendar.Name, index)).ToList()
		};
	}

	internal static void RenameCalendarInternal(IConnectionInfo info, string oldName, string newName, string? tableName = null)
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
		Model model = info.Database.Model;
		Calendar calendar;
		Table table;
		if (!string.IsNullOrWhiteSpace(tableName))
		{
			calendar = FindCalendar(model, tableName, oldName);
			table = model.Tables.Find(tableName);
		}
		else
		{
			table = GetCalendarTable(model, oldName);
			calendar = table.Calendars.Find(oldName);
		}
		if (table.Calendars.Find(newName) != null && !string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Calendar '{newName}' already exists in table '{table.Name}'", ErrorSource.User);
		}
		calendar.RequestRename(newName);
		TransactionOperations.RecordOperation(info, $"Renamed calendar from '{oldName}' to '{newName}' in table '{table.Name}'");
		ConnectionOperations.SaveChangesWithRollback(info, "rename calendar", OperationType.Update, CheckpointMode.AfterRequestRename);
	}

	internal static void DeleteCalendarInternal(IConnectionInfo info, string calendarName, string? tableName = null)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo is null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(calendarName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("calendarName is required", ErrorSource.User);
		}
		Model model = info.Database.Model;
		Calendar metadataObject;
		Table table;
		if (!string.IsNullOrWhiteSpace(tableName))
		{
			metadataObject = FindCalendar(model, tableName, calendarName);
			table = model.Tables.Find(tableName);
		}
		else
		{
			table = GetCalendarTable(model, calendarName);
			metadataObject = table.Calendars.Find(calendarName);
		}
		table.Calendars.Remove(metadataObject);
		TransactionOperations.RecordOperation(info, $"Deleted calendar '{calendarName}' from table '{table.Name}'");
		ConnectionOperations.SaveChangesWithRollback(info, "delete calendar", OperationType.Delete);
	}

	public static async Task<List<CalendarColumnGroupDefinition>> ListColumnGroups(string? connectionName, string calendarName, string? tableName = null)
	{
		if (string.IsNullOrWhiteSpace(calendarName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("calendarName is required", ErrorSource.User);
		}
		List<CalendarColumnGroupDefinition> result;
		await using (IConnectionInfo connectionInfo = await ConnectionOperations.GetAsync(connectionName))
		{
			try
			{
				Model model = connectionInfo.Database.Model;
				Calendar calendar = (string.IsNullOrWhiteSpace(tableName) ? GetCalendarTable(model, calendarName).Calendars.Find(calendarName) : FindCalendar(model, tableName, calendarName));
				List<CalendarColumnGroupDefinition> list = calendar.CalendarColumnGroups.Select((CalendarColumnGroup cg) => ConvertColumnGroupToDefinition(cg, calendarName)).ToList();
				AuditEvent.Default.Emit("list calendar column groups", success: true, OperationType.Read, connectionInfo);
				result = list;
			}
			catch
			{
				AuditEvent.Default.Emit("list calendar column groups", success: false, OperationType.Read, connectionInfo);
				throw;
			}
		}
		return result;
	}

	public static CalendarColumnGroupDefinition GetColumnGroupByReference(IConnectionInfo info, CalendarColumnGroupDefinition reference)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo is null", ErrorSource.User);
		}
		if (reference == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Column group reference cannot be null", ErrorSource.User);
		}
		var (calendar, _, index) = ResolveColumnGroupReference(info.Database.Model, reference);
		return ConvertColumnGroupToDefinition(calendar.CalendarColumnGroups[index], reference.CalendarName);
	}

	internal static CalendarColumnGroupOperationResult CreateColumnGroupInternal(IConnectionInfo info, string calendarName, string? tableName, CalendarColumnGroupDefinition def)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo is null", ErrorSource.User);
		}
		if (string.IsNullOrWhiteSpace(calendarName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("calendarName is required", ErrorSource.User);
		}
		ValidateCalendarColumnGroupDefinition(def);
		Model model = info.Database.Model;
		Calendar calendar;
		Table table;
		if (!string.IsNullOrWhiteSpace(tableName))
		{
			calendar = FindCalendar(model, tableName, calendarName);
			table = model.Tables.Find(tableName);
		}
		else
		{
			table = GetCalendarTable(model, calendarName);
			calendar = table.Calendars.Find(calendarName);
		}
		CalendarColumnGroup calendarColumnGroup;
		if (def.GroupType == "TimeRelated")
		{
			TimeRelatedColumnGroup timeRelatedColumnGroup = new TimeRelatedColumnGroup();
			if (def.TimeRelatedGroup?.Columns != null)
			{
				foreach (string column4 in def.TimeRelatedGroup.Columns)
				{
					Column column = table.Columns.Find(column4);
					if (column == null)
					{
						throw McpExceptionWithSource.FromTelemetrySafeMessage($"Column '{column4}' not found in table '{table.Name}'", ErrorSource.User);
					}
					timeRelatedColumnGroup.Columns.Add(column);
				}
			}
			calendarColumnGroup = timeRelatedColumnGroup;
		}
		else
		{
			if (!(def.GroupType == "TimeUnitAssociation"))
			{
				throw new McpExceptionWithSource("Invalid GroupType: " + def.GroupType, ErrorSource.User, "Invalid GroupType supplied. Supported values: TimeRelated, TimeUnitAssociation.");
			}
			if (def.TimeUnitAssociation == null)
			{
				throw McpExceptionWithSource.FromTelemetrySafeMessage("TimeUnitAssociation definition is required", ErrorSource.User);
			}
			if (!Enum.TryParse<TimeUnit>(def.TimeUnitAssociation.TimeUnit, out var result))
			{
				throw new McpExceptionWithSource("Invalid TimeUnit: " + def.TimeUnitAssociation.TimeUnit, ErrorSource.User, "Invalid TimeUnit supplied. Supported values: " + string.Join(", ", Enum.GetNames(typeof(TimeUnit))) + ".");
			}
			TimeUnitColumnAssociation timeUnitColumnAssociation = new TimeUnitColumnAssociation(result);
			if (!string.IsNullOrWhiteSpace(def.TimeUnitAssociation.PrimaryColumnName))
			{
				Column column2 = table.Columns.Find(def.TimeUnitAssociation.PrimaryColumnName);
				if (column2 == null)
				{
					throw McpExceptionWithSource.FromTelemetrySafeMessage($"Primary column '{def.TimeUnitAssociation.PrimaryColumnName}' not found in table '{table.Name}'", ErrorSource.User);
				}
				timeUnitColumnAssociation.PrimaryColumn = column2;
			}
			if (def.TimeUnitAssociation.AssociatedColumns != null)
			{
				foreach (string associatedColumn in def.TimeUnitAssociation.AssociatedColumns)
				{
					Column column3 = table.Columns.Find(associatedColumn);
					if (column3 == null)
					{
						throw McpExceptionWithSource.FromTelemetrySafeMessage($"Associated column '{associatedColumn}' not found in table '{table.Name}'", ErrorSource.User);
					}
					timeUnitColumnAssociation.AssociatedColumns.Add(column3);
				}
			}
			calendarColumnGroup = timeUnitColumnAssociation;
		}
		calendar.CalendarColumnGroups.Add(calendarColumnGroup);
		TransactionOperations.RecordOperation(info, $"Created column group in calendar '{calendarName}' in table '{table.Name}'");
		ConnectionOperations.SaveChangesWithRollback(info, "create column group", OperationType.Create);
		return CreateColumnGroupOperationResult(calendarColumnGroup, calendar.Name, calendar.CalendarColumnGroups.Count - 1);
	}

	private static CalendarColumnGroupOperationResult CreateColumnGroupOperationResult(CalendarColumnGroup columnGroup, string calendarName, int groupIndex)
	{
		CalendarColumnGroupOperationResult calendarColumnGroupOperationResult = new CalendarColumnGroupOperationResult
		{
			CalendarName = calendarName,
			GroupIndex = groupIndex
		};
		if (columnGroup is TimeRelatedColumnGroup timeRelatedColumnGroup)
		{
			calendarColumnGroupOperationResult.GroupType = "TimeRelated";
			calendarColumnGroupOperationResult.ColumnCount = timeRelatedColumnGroup.Columns.Count;
		}
		else if (columnGroup is TimeUnitColumnAssociation timeUnitColumnAssociation)
		{
			calendarColumnGroupOperationResult.GroupType = "TimeUnitAssociation";
			calendarColumnGroupOperationResult.ColumnCount = timeUnitColumnAssociation.AssociatedColumns.Count;
			calendarColumnGroupOperationResult.TimeUnit = timeUnitColumnAssociation.TimeUnit.ToString();
			calendarColumnGroupOperationResult.PrimaryColumnName = timeUnitColumnAssociation.PrimaryColumn?.Name;
		}
		return calendarColumnGroupOperationResult;
	}

	private static CalendarColumnGroupOperationResult UpdateColumnGroupInternal(IConnectionInfo info, string calendarName, string? tableName, int columnGroupIndex, CalendarColumnGroupDefinition update)
	{
		if (string.IsNullOrWhiteSpace(calendarName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("calendarName is required", ErrorSource.User);
		}
		if (columnGroupIndex < 0)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("columnGroupIndex must be non-negative", ErrorSource.User);
		}
		if (update == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("update definition is required", ErrorSource.User);
		}
		Model model = info.Database.Model;
		Calendar calendar;
		Table table;
		if (!string.IsNullOrWhiteSpace(tableName))
		{
			calendar = FindCalendar(model, tableName, calendarName);
			table = model.Tables.Find(tableName);
		}
		else
		{
			table = GetCalendarTable(model, calendarName);
			calendar = table.Calendars.Find(calendarName);
		}
		if (columnGroupIndex >= calendar.CalendarColumnGroups.Count)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Column group index {columnGroupIndex} is out of range. Calendar has {calendar.CalendarColumnGroups.Count} column groups.", ErrorSource.User);
		}
		CalendarColumnGroup calendarColumnGroup = calendar.CalendarColumnGroups[columnGroupIndex];
		bool flag = calendarColumnGroup is TimeRelatedColumnGroup;
		bool flag2 = calendarColumnGroup is TimeUnitColumnAssociation;
		if ((flag && update.GroupType != "TimeRelated") || (flag2 && update.GroupType != "TimeUnitAssociation"))
		{
			throw new McpExceptionWithSource("Cannot change column group type from " + (flag ? "TimeRelated" : "TimeUnitAssociation") + " to " + update.GroupType, ErrorSource.User, "Cannot change column group type from " + (flag ? "TimeRelated" : "TimeUnitAssociation") + "; changing GroupType is not supported.");
		}
		bool flag3 = false;
		if (update.GroupType == "TimeRelated" && calendarColumnGroup is TimeRelatedColumnGroup timeRelatedColumnGroup)
		{
			if (update.TimeRelatedGroup?.Columns != null)
			{
				timeRelatedColumnGroup.Columns.Clear();
				foreach (string column4 in update.TimeRelatedGroup.Columns)
				{
					Column column = table.Columns.Find(column4);
					if (column == null)
					{
						throw McpExceptionWithSource.FromTelemetrySafeMessage($"Column '{column4}' not found in table '{table.Name}'", ErrorSource.User);
					}
					timeRelatedColumnGroup.Columns.Add(column);
				}
				flag3 = true;
			}
		}
		else if (update.GroupType == "TimeUnitAssociation" && calendarColumnGroup is TimeUnitColumnAssociation timeUnitColumnAssociation && update.TimeUnitAssociation != null)
		{
			if (!string.IsNullOrWhiteSpace(update.TimeUnitAssociation.TimeUnit))
			{
				if (!Enum.TryParse<TimeUnit>(update.TimeUnitAssociation.TimeUnit, out var result))
				{
					throw new McpExceptionWithSource("Invalid TimeUnit: " + update.TimeUnitAssociation.TimeUnit, ErrorSource.User, "Invalid TimeUnit supplied. Supported values: " + string.Join(", ", Enum.GetNames(typeof(TimeUnit))) + ".");
				}
				if (timeUnitColumnAssociation.TimeUnit != result)
				{
					timeUnitColumnAssociation.TimeUnit = result;
					flag3 = true;
				}
			}
			if (update.TimeUnitAssociation.PrimaryColumnName != null)
			{
				Column column2 = null;
				if (!string.IsNullOrWhiteSpace(update.TimeUnitAssociation.PrimaryColumnName))
				{
					column2 = table.Columns.Find(update.TimeUnitAssociation.PrimaryColumnName);
					if (column2 == null)
					{
						throw McpExceptionWithSource.FromTelemetrySafeMessage($"Primary column '{update.TimeUnitAssociation.PrimaryColumnName}' not found in table '{table.Name}'", ErrorSource.User);
					}
				}
				if (timeUnitColumnAssociation.PrimaryColumn != column2)
				{
					timeUnitColumnAssociation.PrimaryColumn = column2;
					flag3 = true;
				}
			}
			if (update.TimeUnitAssociation.AssociatedColumns != null)
			{
				timeUnitColumnAssociation.AssociatedColumns.Clear();
				foreach (string associatedColumn in update.TimeUnitAssociation.AssociatedColumns)
				{
					Column column3 = table.Columns.Find(associatedColumn);
					if (column3 == null)
					{
						throw McpExceptionWithSource.FromTelemetrySafeMessage($"Associated column '{associatedColumn}' not found in table '{table.Name}'", ErrorSource.User);
					}
					timeUnitColumnAssociation.AssociatedColumns.Add(column3);
				}
				flag3 = true;
			}
		}
		if (flag3)
		{
			TransactionOperations.RecordOperation(info, $"Updated column group {columnGroupIndex} in calendar '{calendarName}' in table '{table.Name}'");
			ConnectionOperations.SaveChangesWithRollback(info, "update column group", OperationType.Update);
		}
		return CreateColumnGroupOperationResult(calendarColumnGroup, calendarName, columnGroupIndex);
	}

	internal static CalendarColumnGroupOperationResult UpdateColumnGroupByReference(IConnectionInfo info, CalendarColumnGroupDefinition updateDef)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo is null", ErrorSource.User);
		}
		if (updateDef == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Update definition cannot be null", ErrorSource.User);
		}
		int item = ResolveColumnGroupReference(info.Database.Model, updateDef).Index;
		return UpdateColumnGroupInternal(info, updateDef.CalendarName, updateDef.TableName, item, updateDef);
	}

	private static void DeleteColumnGroupInternal(IConnectionInfo info, string calendarName, string? tableName, int columnGroupIndex)
	{
		if (string.IsNullOrWhiteSpace(calendarName))
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("calendarName is required", ErrorSource.User);
		}
		if (columnGroupIndex < 0)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("columnGroupIndex must be non-negative", ErrorSource.User);
		}
		Model model = info.Database.Model;
		Calendar calendar;
		Table table;
		if (!string.IsNullOrWhiteSpace(tableName))
		{
			calendar = FindCalendar(model, tableName, calendarName);
			table = model.Tables.Find(tableName);
		}
		else
		{
			table = GetCalendarTable(model, calendarName);
			calendar = table.Calendars.Find(calendarName);
		}
		if (columnGroupIndex >= calendar.CalendarColumnGroups.Count)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage($"Column group index {columnGroupIndex} is out of range. Calendar has {calendar.CalendarColumnGroups.Count} column groups.", ErrorSource.User);
		}
		CalendarColumnGroup metadataObject = calendar.CalendarColumnGroups[columnGroupIndex];
		calendar.CalendarColumnGroups.Remove(metadataObject);
		TransactionOperations.RecordOperation(info, $"Deleted column group {columnGroupIndex} from calendar '{calendarName}' in table '{table.Name}'");
		ConnectionOperations.SaveChangesWithRollback(info, "delete column group", OperationType.Delete);
	}

	internal static void DeleteColumnGroupByReference(IConnectionInfo info, CalendarColumnGroupDefinition deleteDef)
	{
		if (info == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("ConnectionInfo is null");
		}
		if (deleteDef == null)
		{
			throw McpExceptionWithSource.FromTelemetrySafeMessage("Delete definition cannot be null", ErrorSource.User);
		}
		int item = ResolveColumnGroupReference(info.Database.Model, deleteDef).Index;
		DeleteColumnGroupInternal(info, deleteDef.CalendarName, deleteDef.TableName, item);
	}

	public static async Task<BatchOperationResponse> CreateCalendars(string? connectionName, List<CalendarDefinition> calendars, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, calendars, options, "Create", "Created", "calendars", (CalendarDefinition item) => item.TableName + "." + item.Name, delegate(BatchItemContext<CalendarDefinition> ctx)
		{
			CalendarOperationResult data = CreateCalendarInternal(ctx.Connection, ctx.Item);
			ctx.Result.Success = true;
			ctx.Result.Message = $"Successfully created calendar '{ctx.Item.Name}' in table '{ctx.Item.TableName}'";
			ctx.Result.Data = data;
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, $"Created calendar '{ctx.Item.TableName}.{ctx.Item.Name}'");
			}
		});
	}

	public static async Task<BatchOperationResponse> UpdateCalendars(string? connectionName, List<CalendarDefinition> calendars, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, calendars, options, "Update", "Updated", "calendars", (CalendarDefinition item) => item.TableName + "." + item.Name, delegate(BatchItemContext<CalendarDefinition> ctx)
		{
			CalendarOperationResult data = UpdateCalendarInternal(ctx.Connection, ctx.Item, ctx.Item.TableName);
			ctx.Result.Success = true;
			ctx.Result.Message = $"Successfully updated calendar '{ctx.Item.Name}' in table '{ctx.Item.TableName}'";
			ctx.Result.Data = data;
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, $"Updated calendar '{ctx.Item.TableName}.{ctx.Item.Name}'");
			}
		});
	}

	public static async Task<BatchOperationResponse> DeleteCalendars(string? connectionName, List<CalendarReference> calendars, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, calendars, options, "Delete", "Deleted", "calendars", (CalendarReference item) => item.Name, delegate(BatchItemContext<CalendarReference> ctx)
		{
			DeleteCalendarInternal(ctx.Connection, ctx.Item.Name, ctx.Item.TableName);
			ctx.Result.Success = true;
			ctx.Result.Message = "Successfully deleted calendar '" + ctx.Item.Name + "'";
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, "Deleted calendar '" + ctx.Item.Name + "'");
			}
		});
	}

	public static async Task<BatchOperationResponse> GetCalendars(string? connectionName, List<CalendarReference> calendars, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "Get",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (calendars == null || !calendars.Any())
		{
			response.Success = false;
			response.Message = "No calendars provided for retrieval";
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
				for (int i = 0; i < calendars.Count; i++)
				{
					CalendarReference calendarReference = calendars[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = calendarReference.Name
					};
					try
					{
						CalendarGet calendarInternal = GetCalendarInternal(connectionInfo.Database, calendarReference.Name, calendarReference.TableName);
						itemResult.Success = true;
						itemResult.Message = "Successfully retrieved calendar '" + calendarReference.Name + "'";
						itemResult.Data = calendarInternal;
						successCount++;
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error retrieving calendar '" + calendarReference.Name + "': " + ex.Message;
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
				response.Message = $"Processed {calendars.Count} calendar(s): {successCount} succeeded, {failureCount} failed";
			}
			catch (Exception ex2)
			{
				response.Success = false;
				response.Exceptions.Add(ex2);
				response.Message = "Get operation failed: " + ex2.Message;
				failureCount = calendars.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
				AuditEvent.Default.Emit("get calendars", response.Success, OperationType.Read, connectionInfo);
			}
			response.Summary = new BatchSummary
			{
				TotalItems = calendars.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> RenameCalendars(string? connectionName, List<CalendarRename> calendars, BatchOptions options)
	{
		return await BatchExecutor.ExecuteAsync(connectionName, calendars, options, "Rename", "Renamed", "calendars", (CalendarRename item) => item.CurrentName, delegate(BatchItemContext<CalendarRename> ctx)
		{
			RenameCalendarInternal(ctx.Connection, ctx.Item.CurrentName, ctx.Item.NewName, ctx.Item.TableName);
			ctx.Result.Success = true;
			ctx.Result.Message = $"Successfully renamed calendar '{ctx.Item.CurrentName}' to '{ctx.Item.NewName}'";
			if (ctx.TransactionId != null)
			{
				TransactionOperations.RecordOperation(ctx.Connection, $"Renamed calendar '{ctx.Item.CurrentName}' to '{ctx.Item.NewName}'");
			}
		});
	}

	public static async Task<BatchOperationResponse> CreateColumnGroups(string? connectionName, List<CalendarColumnGroupDefinition> columnGroups, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "CreateColumnGroup",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (columnGroups == null || !columnGroups.Any())
		{
			response.Success = false;
			response.Message = "No column group definitions provided for creation";
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
				for (int i = 0; i < columnGroups.Count; i++)
				{
					CalendarColumnGroupDefinition calendarColumnGroupDefinition = columnGroups[i];
					ItemResult itemResult = new ItemResult
					{
						Index = i,
						ItemIdentifier = calendarColumnGroupDefinition.CalendarName + "[" + calendarColumnGroupDefinition.GroupType + "]"
					};
					try
					{
						CalendarColumnGroupOperationResult data = CreateColumnGroupInternal(connectionInfo, calendarColumnGroupDefinition.CalendarName, calendarColumnGroupDefinition.TableName, calendarColumnGroupDefinition);
						itemResult.Success = true;
						itemResult.Message = "Successfully created column group in calendar '" + calendarColumnGroupDefinition.CalendarName + "'";
						itemResult.Data = data;
						successCount++;
						if (transactionId != null)
						{
							TransactionOperations.RecordOperation(connectionInfo, "Created column group in calendar '" + calendarColumnGroupDefinition.CalendarName + "'");
						}
					}
					catch (Exception ex)
					{
						itemResult.Success = false;
						itemResult.Message = "Error creating column group in calendar '" + calendarColumnGroupDefinition.CalendarName + "': " + ex.Message;
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult);
					if (!itemResult.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				BatchTransactionHelper.FinalizeBatchTransaction(connectionInfo, response, transactionId, ownsTransaction, columnGroups.Count, ref successCount, ref failureCount, "Created", "column groups");
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
				response.Message = "CreateColumnGroup operation failed: " + ex2.Message;
				failureCount = columnGroups.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
			}
			response.Summary = new BatchSummary
			{
				TotalItems = columnGroups.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> GetColumnGroupsByReference(string? connectionName, List<CalendarColumnGroupDefinition> columnGroups, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "GetColumnGroup",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (columnGroups == null || !columnGroups.Any())
		{
			response.Success = false;
			response.Message = "No column group references provided for retrieval";
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
				for (int i = 0; i < columnGroups.Count; i++)
				{
					CalendarColumnGroupDefinition calendarColumnGroupDefinition = columnGroups[i];
					ItemResult itemResult = new ItemResult();
					itemResult.Index = i;
					itemResult.ItemIdentifier = calendarColumnGroupDefinition.CalendarName + "[" + calendarColumnGroupDefinition.GroupType + ((calendarColumnGroupDefinition.TimeUnit != null) ? (":" + calendarColumnGroupDefinition.TimeUnit) : "") + "]";
					ItemResult itemResult2 = itemResult;
					try
					{
						CalendarColumnGroupDefinition columnGroupByReference = GetColumnGroupByReference(connectionInfo, calendarColumnGroupDefinition);
						itemResult2.Success = true;
						itemResult2.Message = "Successfully retrieved " + calendarColumnGroupDefinition.GroupType + " column group" + ((calendarColumnGroupDefinition.TimeUnit != null) ? (" (" + calendarColumnGroupDefinition.TimeUnit + ")") : "") + " from calendar '" + calendarColumnGroupDefinition.CalendarName + "'";
						itemResult2.Data = columnGroupByReference;
						successCount++;
					}
					catch (Exception ex)
					{
						itemResult2.Success = false;
						itemResult2.Message = $"Error retrieving {calendarColumnGroupDefinition.GroupType} column group from calendar '{calendarColumnGroupDefinition.CalendarName}': {ex.Message}";
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult2);
					if (!itemResult2.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				response.Success = failureCount == 0;
				response.Message = $"Processed {columnGroups.Count} column group(s): {successCount} succeeded, {failureCount} failed";
			}
			catch (Exception ex2)
			{
				response.Success = false;
				response.Exceptions.Add(ex2);
				response.Message = "GetColumnGroup operation failed: " + ex2.Message;
				failureCount = columnGroups.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
				AuditEvent.Default.Emit("get calendar column groups", response.Success, OperationType.Read, connectionInfo);
			}
			response.Summary = new BatchSummary
			{
				TotalItems = columnGroups.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> UpdateColumnGroupsByReference(string? connectionName, List<CalendarColumnGroupDefinition> columnGroups, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "UpdateColumnGroup",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (columnGroups == null || !columnGroups.Any())
		{
			response.Success = false;
			response.Message = "No column group definitions provided for update";
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
				for (int i = 0; i < columnGroups.Count; i++)
				{
					CalendarColumnGroupDefinition calendarColumnGroupDefinition = columnGroups[i];
					ItemResult itemResult = new ItemResult();
					itemResult.Index = i;
					itemResult.ItemIdentifier = calendarColumnGroupDefinition.CalendarName + "[" + calendarColumnGroupDefinition.GroupType + ((calendarColumnGroupDefinition.TimeUnit != null) ? (":" + calendarColumnGroupDefinition.TimeUnit) : "") + "]";
					ItemResult itemResult2 = itemResult;
					try
					{
						CalendarColumnGroupOperationResult data = UpdateColumnGroupByReference(connectionInfo, calendarColumnGroupDefinition);
						itemResult2.Success = true;
						itemResult2.Message = "Successfully updated " + calendarColumnGroupDefinition.GroupType + " column group" + ((calendarColumnGroupDefinition.TimeUnit != null) ? (" (" + calendarColumnGroupDefinition.TimeUnit + ")") : "") + " in calendar '" + calendarColumnGroupDefinition.CalendarName + "'";
						itemResult2.Data = data;
						successCount++;
						if (transactionId != null)
						{
							TransactionOperations.RecordOperation(connectionInfo, $"Updated {calendarColumnGroupDefinition.GroupType} column group in calendar '{calendarColumnGroupDefinition.CalendarName}'");
						}
					}
					catch (Exception ex)
					{
						itemResult2.Success = false;
						itemResult2.Message = $"Error updating {calendarColumnGroupDefinition.GroupType} column group in calendar '{calendarColumnGroupDefinition.CalendarName}': {ex.Message}";
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult2);
					if (!itemResult2.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				BatchTransactionHelper.FinalizeBatchTransaction(connectionInfo, response, transactionId, ownsTransaction, columnGroups.Count, ref successCount, ref failureCount, "Updated", "column groups");
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
				response.Message = "UpdateColumnGroup operation failed: " + ex2.Message;
				failureCount = columnGroups.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
			}
			response.Summary = new BatchSummary
			{
				TotalItems = columnGroups.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}

	public static async Task<BatchOperationResponse> DeleteColumnGroupsByReference(string? connectionName, List<CalendarColumnGroupDefinition> columnGroups, BatchOptions options)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<string> warnings = new List<string>();
		BatchOperationResponse response = new BatchOperationResponse
		{
			Operation = "DeleteColumnGroup",
			Results = new List<ItemResult>(),
			Warnings = warnings
		};
		if (columnGroups == null || !columnGroups.Any())
		{
			response.Success = false;
			response.Message = "No column group definitions provided for deletion";
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
				for (int i = 0; i < columnGroups.Count; i++)
				{
					CalendarColumnGroupDefinition calendarColumnGroupDefinition = columnGroups[i];
					ItemResult itemResult = new ItemResult();
					itemResult.Index = i;
					itemResult.ItemIdentifier = calendarColumnGroupDefinition.CalendarName + "[" + calendarColumnGroupDefinition.GroupType + ((calendarColumnGroupDefinition.TimeUnit != null) ? (":" + calendarColumnGroupDefinition.TimeUnit) : "") + "]";
					ItemResult itemResult2 = itemResult;
					try
					{
						DeleteColumnGroupByReference(connectionInfo, calendarColumnGroupDefinition);
						itemResult2.Success = true;
						itemResult2.Message = "Successfully deleted " + calendarColumnGroupDefinition.GroupType + " column group" + ((calendarColumnGroupDefinition.TimeUnit != null) ? (" (" + calendarColumnGroupDefinition.TimeUnit + ")") : "") + " from calendar '" + calendarColumnGroupDefinition.CalendarName + "'";
						successCount++;
						if (transactionId != null)
						{
							TransactionOperations.RecordOperation(connectionInfo, $"Deleted {calendarColumnGroupDefinition.GroupType} column group from calendar '{calendarColumnGroupDefinition.CalendarName}'");
						}
					}
					catch (Exception ex)
					{
						itemResult2.Success = false;
						itemResult2.Message = $"Error deleting {calendarColumnGroupDefinition.GroupType} column group from calendar '{calendarColumnGroupDefinition.CalendarName}': {ex.Message}";
						failureCount++;
						response.Exceptions.Add(ex);
					}
					response.Results.Add(itemResult2);
					if (!itemResult2.Success && !options.ContinueOnError)
					{
						break;
					}
				}
				BatchTransactionHelper.FinalizeBatchTransaction(connectionInfo, response, transactionId, ownsTransaction, columnGroups.Count, ref successCount, ref failureCount, "Deleted", "column groups");
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
				response.Message = "DeleteColumnGroup operation failed: " + ex2.Message;
				failureCount = columnGroups.Count - successCount;
			}
			finally
			{
				stopwatch.Stop();
			}
			response.Summary = new BatchSummary
			{
				TotalItems = columnGroups.Count,
				SuccessCount = successCount,
				FailureCount = failureCount,
				ExecutionTime = stopwatch.Elapsed
			};
			result = response;
		}
		return result;
	}
}
