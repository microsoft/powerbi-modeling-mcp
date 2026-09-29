using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace PowerBIModelingMCP.Library.Common.DataStructures;

public class DaxQueryImpersonationOptions
{
	[Description("Security role names to apply via the Roles connection string property.")]
	public List<string>? Roles { get; set; }

	[Description("User principal name to impersonate via the EffectiveUserName connection string property.")]
	public string? UserPrincipalName { get; set; }

	public bool HasAny()
	{
		if (!HasUserPrincipal())
		{
			return GetNormalizedRoles().Count > 0;
		}
		return true;
	}

	public bool HasUserPrincipal()
	{
		return !string.IsNullOrWhiteSpace(UserPrincipalName);
	}

	public List<string> GetNormalizedRoles()
	{
		if (Roles == null)
		{
			return new List<string>();
		}
		return (from role in Roles
			where !string.IsNullOrWhiteSpace(role)
			select role.Trim()).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToList();
	}

	public string ToDisplayString()
	{
		List<string> list = new List<string>();
		if (HasUserPrincipal())
		{
			list.Add("user principal '" + UserPrincipalName.Trim() + "'");
		}
		List<string> normalizedRoles = GetNormalizedRoles();
		if (normalizedRoles.Count > 0)
		{
			list.Add("roles '" + string.Join(", ", normalizedRoles) + "'");
		}
		if (list.Count != 0)
		{
			return string.Join("; ", list);
		}
		return "none";
	}
}
