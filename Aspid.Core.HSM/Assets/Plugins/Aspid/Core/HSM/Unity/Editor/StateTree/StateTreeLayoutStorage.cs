using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace Aspid.Core.HSM.Editor
{
	/// <summary>
	/// Storage for the HSM graph's user layout: positions of dragged nodes
	/// are saved as JSON under <c>UserSettings/</c> (machine-local, outside VCS)
	/// separately for each <see cref="StateTreeLayoutDirection"/> mode.
	/// </summary>
	public static class StateTreeLayoutStorage
	{
		private const string FilePath = "UserSettings/AspidHsmStateTreeLayout.json";

		public static Dictionary<string, Vector2> Load(StateTreeLayoutDirection mode)
		{
			var positions = new Dictionary<string, Vector2>();
			string modeName = mode.ToString();

			foreach (Entry entry in LoadData().entries)
			{
				if (entry.mode == modeName)
				{
					positions[entry.type] = new Vector2(entry.x, entry.y);
				}
			}

			return positions;
		}

		public static void Save(StateTreeLayoutDirection mode, Type stateType, Vector2 position)
		{
			Data data = LoadData();
			string modeName = mode.ToString();
			string typeName = stateType.FullName;

			Entry entry = data.entries.Find(candidate => candidate.mode == modeName && candidate.type == typeName);
			if (entry == null)
			{
				entry = new Entry { mode = modeName, type = typeName };
				data.entries.Add(entry);
			}

			entry.x = position.x;
			entry.y = position.y;
			SaveData(data);
		}

		public static void Clear(StateTreeLayoutDirection mode)
		{
			Data data = LoadData();
			string modeName = mode.ToString();
			data.entries.RemoveAll(entry => entry.mode == modeName);
			SaveData(data);
		}

		private static Data LoadData()
		{
			if (!File.Exists(FilePath))
			{
				return new Data();
			}

			try
			{
				return JsonUtility.FromJson<Data>(File.ReadAllText(FilePath)) ?? new Data();
			}
			catch (Exception)
			{
				return new Data();
			}
		}

		private static void SaveData(Data data) =>
			File.WriteAllText(FilePath, JsonUtility.ToJson(data, prettyPrint: true));

		[Serializable]
		private sealed class Data
		{
			public List<Entry> entries = new();
		}

		[Serializable]
		private sealed class Entry
		{
			public string mode;
			public string type;
			public float x;
			public float y;
		}
	}
}
