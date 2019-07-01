using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Globalization;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;


namespace gs
{
	/// <summary>
	/// Store/restore additive settings data structures using JSON serialization
	/// </summary>
	public class SettingsSerializer
	{

		public SettingsSerializer()
		{

		}

		/// <summary>
		/// Read all .txt files in Path and try to parse each one as a Settings object
		/// </summary>
		public bool RestoreFromFolder(string sPath, out List<PlanarAdditiveSettings> SettingsList, out List<string> SourceFilePaths)
		{
			SettingsList = new List<PlanarAdditiveSettings>();
			SourceFilePaths = new List<string>();

			string[] files = Directory.GetFiles(sPath);
			foreach (string filePath in files)
			{
				if (!filePath.EndsWith(".txt"))
					continue;

				var save_culture = Thread.CurrentThread.CurrentCulture;
				Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
				string data = File.ReadAllText(filePath);
				Thread.CurrentThread.CurrentCulture = save_culture;

				SingleMaterialFFFSettings settings = JsonToSettings(data);

				if (settings == null)
				{
					//DebugUtil.Log("SettingsSerializer.RestoreFromFolder: error reading " + filePath);
				}
				else
				{
					SettingsList.Add(settings);
					SourceFilePaths.Add(filePath);
				}
			}

			return SettingsList.Count > 0;
		}


		static Assembly GSGCODE_ASM = null;


		/// <summary>
		/// Parse the json in data string into the right type of Settings object
		/// </summary>
		public SingleMaterialFFFSettings JsonToSettings(string data)
		{
			if (!is_settings_json(data))
				return null;

			// [RMS] because we split gsGCode into a separate dll, Type.GetType() will not find it.
			// We have to use a handle to the relevant assembly.
			if (GSGCODE_ASM == null)
				GSGCODE_ASM = Assembly.Load("gsGCode");

			int typeIdx = data.IndexOf("\"ClassTypeName\"");
			SingleMaterialFFFSettings settings = null;
			if (typeIdx > 0)
			{
				try
				{
					int commaIdx = typeIdx + 1;
					while (data[commaIdx] != ',')
						commaIdx++;
					int startIdx = typeIdx + 18;
					string className = data.Substring(startIdx, commaIdx - startIdx - 1);
					//var type = Type.GetType(className);
					var type = GSGCODE_ASM.GetType(className);
					object o = JsonConvert.DeserializeObject(data, type);
					settings = o as SingleMaterialFFFSettings;
				}
				catch
				{
					// ignore disasters
				}
			}

			// either no typename, or we failed to construct it
			if (settings == null)
			{
				try
				{
					settings = JsonConvert.DeserializeObject<SingleMaterialFFFSettings>(data);
				}
				catch
				{
				}
			}

			return settings;
		}



		bool is_settings_json(string s)
		{
			return s.Contains("ManufacturerUUID") && s.Contains("ModelUUID");
		}




		//public void RefreshSettingsFromDisk(MachineDatabase db, MachinePreset preset)
		//{
		//    if (File.Exists(preset.SourcePath) == false)
		//        throw new Exception("SettingsSerializer.RefreshSettingsFromDisk: path " + preset.SourcePath + " does not exist!");

		//    var save_culture = Thread.CurrentThread.CurrentCulture;
		//    Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
		//    string data = File.ReadAllText(preset.SourcePath);
		//    Thread.CurrentThread.CurrentCulture = save_culture;

		//    SingleMaterialFFFSettings settings = JsonToSettings(data);
		//    if ( settings == null )
		//        throw new Exception("SettingsSerializer.RefreshSettingsFromDisk: json data could not be parsed!");

		//    preset.Settings = settings;
		//}



		//public void StoreSettings(MachineDatabase db, MachinePreset preset, bool bCreate = false)
		//{
		//    JsonSerializerSettings jsonSettings = makeWriteSerializer();
		//    if (bCreate == false && File.Exists(preset.SourcePath) == false)
		//        throw new Exception("SettingsSerializer.StoreSettings: path " + preset.SourcePath + " does not exist!");

		//    var save_culture = Thread.CurrentThread.CurrentCulture;
		//    Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

		//    string json = JsonConvert.SerializeObject(preset.Settings, jsonSettings);
		//    System.IO.File.WriteAllText(preset.SourcePath, json);

		//    Thread.CurrentThread.CurrentCulture = save_culture;
		//}



		//public void UpdateSettingsFromJson(MachineDatabase db, MachinePreset preset, string newJson, bool bCreate)
		//{
		//    JsonSerializerSettings jsonSettings = makeWriteSerializer();
		//    if (bCreate == false && File.Exists(preset.SourcePath) == false)
		//        throw new Exception("SettingsSerializer.StoreSettings: path " + preset.SourcePath + " does not exist!");

		//    var save_culture = Thread.CurrentThread.CurrentCulture;
		//    Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
		//    System.IO.File.WriteAllText(preset.SourcePath, newJson);
		//    Thread.CurrentThread.CurrentCulture = save_culture;

		//    RefreshSettingsFromDisk(db, preset);
		//}




		public bool ValidateSettingsJson(string json)
		{
			// [RMS] this just checks if string is valid json...
			if (!is_valid_json(json))
				return false;

			var settings = JsonToSettings(json);
			if (settings == null)
				return false;

			return true;
		}



		/// <summary>
		/// check if input string is valid json
		/// details: https://stackoverflow.com/questions/14977848/how-to-make-sure-that-string-is-valid-json-using-json-net
		/// </summary>
		protected bool is_valid_json(string json)
		{
			string strInput = json.Trim();
			if ((strInput.StartsWith("{") && strInput.EndsWith("}")) || //For object
				(strInput.StartsWith("[") && strInput.EndsWith("]"))) //For array
			{
				try
				{
					var obj = JToken.Parse(strInput);
					return true;
				}
				catch (JsonReaderException jex)
				{
					//Exception in parsing json
					Console.WriteLine(jex.Message);
					return false;
				}
				catch (Exception ex) //some other exception
				{
					Console.WriteLine(ex.ToString());
					return false;
				}
			}
			else
			{
				return false;
			}
		}





		//public void GenerateSettingsFolder(MachineDatabase db, string rootPath)
		//{
		//    JsonSerializerSettings jsonSettings = makeWriteSerializer();

		//    foreach ( Manufacturer mfg in db.Manufacturers ) {
		//        string mfgPath = Path.Combine(rootPath, mfg.Name);
		//        if (!Directory.Exists(mfgPath)) {
		//            Directory.CreateDirectory(mfgPath);
		//            if (!Directory.Exists(mfgPath))
		//                throw new Exception("SettingsSerializer: cannot create directory for manufacturer " + mfg.Name);
		//        }

		//        IReadOnlyList<MachineModel> machines = db.ModelsForManufacturer(mfg);
		//        foreach ( MachineModel machine in machines) {
		//            string machinePath = Path.Combine(mfgPath, machine.Name);
		//            if (! Directory.Exists(machinePath) ) {
		//                Directory.CreateDirectory(machinePath);
		//                if (!Directory.Exists(machinePath))
		//                    throw new Exception("SettingsSerializer: cannot create directory for machine " + mfg.Name + "::" + machine.Name);
		//            }


		//            PlanarAdditiveSettings settings = machine.DefaultPreset.Settings;
		//            string settingsPath = Path.Combine(machinePath, settings.Identifier + ".txt");

		//            var save_culture = Thread.CurrentThread.CurrentCulture;
		//            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
		//            string json = JsonConvert.SerializeObject(settings, jsonSettings);
		//            System.IO.File.WriteAllText(settingsPath, json);
		//            Thread.CurrentThread.CurrentCulture = save_culture;
		//        }
		//    }
		//}



		//public string CreateNewSettingsFolder(MachineDatabase db, Manufacturer mfg, MachineModel machine, string rootPath)
		//{
		//    string mfgPath = Path.Combine(rootPath, mfg.Name);
		//    if (!Directory.Exists(mfgPath)) {
		//        Directory.CreateDirectory(mfgPath);
		//        if (!Directory.Exists(mfgPath))
		//            throw new Exception("SettingsSerializer: cannot create directory for manufacturer " + mfg.Name);
		//    }

		//    string machinePath = Path.Combine(mfgPath, machine.Name);
		//    if (!Directory.Exists(machinePath)) {
		//        Directory.CreateDirectory(machinePath);
		//        if (!Directory.Exists(machinePath))
		//            throw new Exception("SettingsSerializer: cannot create directory for machine " + mfg.Name + "::" + machine.Name);
		//    }

		//    return machinePath;
		//}



		JsonSerializerSettings makeWriteSerializer()
		{
			JsonSerializerSettings jsonSettings = new JsonSerializerSettings();
			jsonSettings.Converters.Add(
				new Newtonsoft.Json.Converters.StringEnumConverter());
			jsonSettings.Formatting = Formatting.Indented;
			jsonSettings.NullValueHandling = NullValueHandling.Ignore;
			jsonSettings.ContractResolver = SettingsContractResolver.MyInstance;
			return jsonSettings;
		}


		JsonSerializerSettings makeReadSerializer()
		{
			JsonSerializerSettings jsonSettings = new JsonSerializerSettings();
			jsonSettings.Converters.Add(
				new Newtonsoft.Json.Converters.StringEnumConverter());
			jsonSettings.Formatting = Formatting.Indented;
			jsonSettings.NullValueHandling = NullValueHandling.Ignore;
			return jsonSettings;
		}


	}






	/// <summary>
	/// JSON.Net settings contract - this lets you sort and filter the fields that get serialized
	/// </summary>
	public class SettingsContractResolver : DefaultContractResolver
	{
		public static readonly SettingsContractResolver MyInstance = new SettingsContractResolver();

		public HashSet<string> IgnoreFields = new HashSet<string>();

		public SettingsContractResolver()
		{
			IgnoreFields.Add("LayerRangeFilter");
			IgnoreFields.Add("BaseMachine");
		}

		// use this to filter out fields
		protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
		{
			JsonProperty property = base.CreateProperty(member, memberSerialization);
			if (property.DeclaringType == typeof(SingleMaterialFFFSettings) && IgnoreFields.Contains(property.PropertyName))
			{
				property.Ignored = true;
			}
			return property;
		}

		// sort property names alphabetically
		protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
		{
			List<JsonProperty> sorted =
				base.CreateProperties(type, memberSerialization).OrderBy(p => p.PropertyName).ToList();

			if (type == typeof(FFFMachineInfo))
				sorted = SortMachine(sorted);

			// [TODO] construct an ordering here?

			return sorted;
		}


		static readonly string[] machine_order = new string[] {
				"ManufacturerName",
				"ManufacturerUUID",
				"ModelIdentifier",
				"ModelUUID",
				"Class",
				"BedSizeXMM",
				"BedSizeYMM",
				"MaxHeightMM",
				"FilamentDiamMM",
				"NozzleDiamMM",
				"MinExtruderTempC",
				"MaxExtruderTempC",
				"HasHeatedBed",
				"MinBedTempC",
				"MaxBedTempC",
				"MinLayerHeightMM",
				"MaxLayerHeightMM"
			};

		List<JsonProperty> SortMachine(List<JsonProperty> input)
		{
			List<JsonProperty> sorted = new List<JsonProperty>(input.Count);
			bool[] used = new bool[input.Count];
			for (int i = 0; i < machine_order.Length; ++i)
			{
				int idx = input.FindIndex((v) => { return v.PropertyName.Equals(machine_order[i]); });
				if (idx >= 0)
				{
					sorted.Add(input[idx]);
				}
				used[idx] = true;
			}
			for (int i = 0; i < input.Count; ++i)
			{
				if (used[i] == false)
					sorted.Add(input[i]);
			}
			return sorted;
		}

	}

}
