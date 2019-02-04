/*
Copyright (c) 2019, John Lewin
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.Extensibility
{
	public class PluginManager
	{
		private string pluginStateFile = "DisabledPlugins.json";
		private string knownPluginsFile = "KnownPlugins.json";

		public PluginManager()
		{
			if (File.Exists(pluginStateFile))
			{
				try
				{
					this.Disabled = JsonConvert.DeserializeObject<HashSet<string>>(File.ReadAllText(pluginStateFile));
				}
				catch
				{
					this.Disabled = new HashSet<string>();
				}
			}
			else
			{
				this.Disabled = new HashSet<string>();
			}

			if (File.Exists(knownPluginsFile))
			{
				try
				{
					this.KnownPlugins = JsonConvert.DeserializeObject<List<PluginState>>(File.ReadAllText(knownPluginsFile));
				}
				catch
				{
				}
			}

			var plugins = new List<IApplicationPlugin>();

			foreach (var containerType in PluginFinder.FindTypes<IApplicationPlugin>().Where(type => Disabled.Contains(type.FullName) == false))
			{
				try
				{
					plugins.Add(Activator.CreateInstance(containerType) as IApplicationPlugin);
				}
				catch(Exception ex)
				{
					Console.WriteLine("Error constructing plugin: " + ex.Message);
				}
			}

			this.Plugins = plugins;

			/*
			// Uncomment to generate new KnownPlugins.json file
			KnownPlugins = plugins.Where(p => p.MetaData != null).Select(p => new PluginState { TypeName = p.GetType().FullName, Name = p.MetaData.Name }).ToList();

			File.WriteAllText(
				Path.Combine("..", "..", "knownPlugins.json"),
				JsonConvert.SerializeObject(KnownPlugins, Formatting.Indented)); */

		}

		public List<IApplicationPlugin> Plugins { get; }

		public List<PluginState> KnownPlugins { get; }

		public class PluginState
		{
			public string Name { get; set; }
			public string TypeName { get; set; }
			//public bool Enabled { get; set; }
			//public bool UpdateAvailable { get; set; }
		}

		public HashSet<string> Disabled { get; }

		public void Disable(string typeName) => Disabled.Add(typeName);

		public void Enable(string typeName) => Disabled.Remove(typeName);

		public void Save()
		{
			File.WriteAllText(
				pluginStateFile,
				JsonConvert.SerializeObject(Disabled, Formatting.Indented));
		}

		public void InitializePlugins(SystemWindow systemWindow)
		{
			foreach (var plugin in this.Plugins)
			{
				plugin.Initialize();
			}
		}

		public class MatterControlPluginItem
		{
			public string Name { get; set; }
			public string Url { get; set; }
			public string Version { get; set; }
			public DateTime ReleaseDate { get; set; }
		}
	}
}
