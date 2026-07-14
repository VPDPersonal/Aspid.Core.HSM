using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM.Editor
{
	/// <summary>
	/// Discovers the controllers a state dispatches to, without running user code:
	/// inner controllers registered through a generated [ControllerGroup]
	/// (private "__controllerN" fields), nested <see cref="IController"/> classes,
	/// and the state itself when it implements controller interfaces directly.
	/// Shared by the graph builder (counts on node cards) and the inspector (detailed rows).
	/// </summary>
	public static class StateTreeControllerScanner
	{
		public readonly struct Entry
		{
			public readonly string title;
			public readonly Type type;

			public Entry(string title, Type type)
			{
				this.title = title;
				this.type = type;
			}
		}

		public static List<Entry> Scan(Type stateType)
		{
			var entries = new List<Entry>();
			var isControllerGroup = false;

			// [ControllerGroup] emits explicit interface implementations that dispatch to private
			// "__controllerN" fields holding the actual inner controllers (see ControllerGroupBody).
			// List those instead of the group class itself, otherwise controllers registered via
			// AddControllers(...) never show up.
			foreach (FieldInfo field in stateType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
			{
				if (!IsGeneratedControllerGroupField(field) || !typeof(IController).IsAssignableFrom(field.FieldType))
				{
					continue;
				}

				isControllerGroup = true;
				entries.Add(new Entry(field.FieldType.Name, field.FieldType));
			}

			foreach (Type nested in stateType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
			{
				if (nested.IsAbstract || !typeof(IController).IsAssignableFrom(nested))
				{
					continue;
				}

				entries.Add(new Entry(nested.Name, nested));
			}

			if (!isControllerGroup && typeof(IController).IsAssignableFrom(stateType))
			{
				entries.Add(new Entry("(the state itself)", stateType));
			}

			return entries;
		}

		private static bool IsGeneratedControllerGroupField(FieldInfo field)
		{
			if (!Regex.IsMatch(field.Name, @"^__controller\d+$"))
			{
				return false;
			}

			foreach (var attribute in field.GetCustomAttributes(typeof(System.CodeDom.Compiler.GeneratedCodeAttribute), false))
			{
				if (attribute is System.CodeDom.Compiler.GeneratedCodeAttribute generated
					&& generated.Tool == "Aspid.Core.HSM.Generators.ControllerGroupGenerator")
				{
					return true;
				}
			}

			return false;
		}
	}
}
