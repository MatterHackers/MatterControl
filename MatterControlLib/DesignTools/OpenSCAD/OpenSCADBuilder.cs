/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.PolygonMesh.Processors;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.Library
{
	public class OpenSCADBuilder : IObject3DEditor
	{
		private OpenScadObject3D item;

		public string Name => "Builder";

		public bool Unlocked { get; } = true;

		private string compilerPath = UserSettings.Instance.get(UserSettingsKey.OpenScadPath) ?? "/usr/bin/openscad";

		public IEnumerable<Type> SupportedTypes() => new Type[]
		{
			typeof(OpenScadObject3D)
		};

		public static Dictionary<string, string> LoadVariablesFromAsset(string assetName)
		{
			string assetPath = Path.Combine(ApplicationDataStorage.Instance.ApplicationLibraryDataPath, assetName + ".scad");
			string assetInfoPath = Path.ChangeExtension(assetPath, ".json");

			string script = File.ReadAllText(assetPath);
			string json = File.ReadAllText(assetInfoPath);

			var info = JsonConvert.DeserializeObject<MetaInfo>(json);

			var dictionary = new Dictionary<string, string>();
			foreach (var field in info.Fields)
			{
				var match = Regex.Match(script, $"({field.Key}\\s*=\\s*)(\\d+)");
				if (match.Success)
				{
					dictionary[field.Key] = match.Groups[2].Value;
				}
			}

			return dictionary;
		}

		public GuiWidget Create(IObject3D object3D, UndoBuffer undoBuffer, ThemeConfig theme)
		{
			this.item = object3D as OpenScadObject3D;

			var mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Absolute,
				Width = 225
			};

			FlowLayoutWidget actionButtons = new FlowLayoutWidget();
			mainContainer.AddChild(actionButtons);

			// TODO: This should use the same renaming and asset system as stl/amf assets
			string assetInfoPath = Path.ChangeExtension(item.ScriptPath, ".json");

			bool isGenerator = File.Exists(assetInfoPath);
			if (isGenerator)
			{
				BuildGeneratorUI(theme, mainContainer, item.ScriptPath, assetInfoPath);
			}
			else
			{
				var editButton = new TextButton("Edit".Localize(), theme)
				{
					Margin = 5,
					ToolTipText = "Edit OpenSCAD script".Localize()
				};
				editButton.Click += (s, e) =>
				{
					Process.Start(item.ScriptPath);
				};
				actionButtons.AddChild(editButton);

				var updateButton = new TextButton("Update".Localize(), theme)
				{
					Margin = 5,
					ToolTipText = "Compile model".Localize()
				};
				if (!File.Exists(compilerPath))
				{
					updateButton.Enabled = false;
					updateButton.ToolTipText = "OpenSCAD not installed".Localize();
				}
				updateButton.Click += async (s, e) =>
				{
					using (var meshStream = AggContext.StaticData.OpenStream(Path.Combine("Stls", "openscad_logo.stl")))
					{
						this.item.Mesh = Object3D.Load(meshStream, ".stl", CancellationToken.None).Mesh;
					}

					// TODO: Use assets system
					string outputPath = Path.ChangeExtension(item.ScriptPath, ".stl");
					int err = await Task.Run(() => ExecuteScript(item.ScriptPath, outputPath));

					if (err == 0)
					{
						// Reload the mesh
						this.item.Mesh = StlProcessing.Load(outputPath, CancellationToken.None);
					}
				};
				actionButtons.AddChild(updateButton);
			}

			return mainContainer;
		}

		private void BuildGeneratorUI(ThemeConfig theme, FlowLayoutWidget mainContainer, string assetPath, string assetInfoPath)
		{
			// TODO: When an OpenSCAD script is added to the scene, we need to load the script and extract
			// the hard-coded variable values into the .Variables property for their default values or the
			// meta info file would need to include this hard-coded info

			// Load the OpenSCAD script

			// Load the script meta info

			// Walk the UI elements exposed by the meta info, constructing widgets bound to replacement data

			// When the build button is clicked, perform the replacements defined by the meta info and widgets.
			//  - Walk the meta info
			//  - Grab the values from the linked widgets
			//  - Load the script text
			//  - Perform the regex replaces against the script source
			//  - Execute the script, writing the results to a target file
			//  - Use the mesh loader to load the mesh
			//  - Replace the mesh in the scene with the newly generated content

			string scriptText = File.ReadAllText(assetPath);
			MetaInfo info = null;

			string json = File.ReadAllText(assetInfoPath);
			info = JsonConvert.DeserializeObject<MetaInfo>(json);

			if (info != null)
			{
				FlowLayoutWidget rowContainer;
				foreach (var field in info.Fields)
				{
					string key = field.Key;

					string latest;
					this.item.Variables.TryGetValue(field.Key, out latest);

					rowContainer = CreateSettingsRow(field.Title, theme);

					if (field.Type == "text")
					{
						double val;
						double.TryParse(latest, out val);

						var editor = new MHNumberEdit(val, theme, pixelWidth: 50 * GuiWidget.DeviceScale, allowDecimals: true, increment: .05)
						{
							SelectAllOnFocus = true,
							VAnchor = VAnchor.Center
						};
						editor.ActuallNumberEdit.KeyPressed += (s, e) =>
						{
							var editWidget = s as NumberEdit;
							this.item.Variables[key] = editWidget.Text;
						};
						rowContainer.AddChild(editor);
					}
					else if (field.Type == "bool")
					{
						var checkbox = new CheckBox("")
						{
							Checked = latest == "true"
						};
						checkbox.Click += (sender, e) =>
						{
							this.item.Variables[key] = ((CheckBox)sender).Checked.ToString().ToLower();
						};
						rowContainer.AddChild(checkbox);
					}

					mainContainer.AddChild(rowContainer);
				}
			}
			else
			{
				// TODO: some fallback ui?
			}
		}

		private int ExecuteScript(string sourcePath, string outputPath)
		{
			var process = new Process()
			{
				StartInfo = new ProcessStartInfo(
					compilerPath,
					$"-o \"{outputPath}\" \"{sourcePath}\"")
			};

			Console.Write(process.StartInfo.FileName);
			Console.WriteLine(process.StartInfo.Arguments);

			process.StartInfo.UseShellExecute = false;
			process.StartInfo.RedirectStandardError = true;

			process.Start();

			process.WaitForExit();

			string error = process.StandardError.ReadToEnd();

			if (process.ExitCode != 0)
			{
				UiThread.RunOnIdle(() =>
				{
					StyledMessageBox.ShowMessageBox(error, "Error compiling OpenSCAD script".Localize());
				});
			}

			return process.ExitCode;
		}

		private static FlowLayoutWidget CreateSettingsRow(string labelText, ThemeConfig theme)
		{
			var rowContainer = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				HAnchor = HAnchor.Stretch,
				Padding = new BorderDouble(5)
			};

			var label = new TextWidget(labelText + ":", textColor: theme.TextColor, pointSize: theme.DefaultFontSize)
			{
				Margin = new BorderDouble(0, 0, 3, 0),
				VAnchor = VAnchor.Center
			};
			rowContainer.AddChild(label);

			rowContainer.AddChild(new HorizontalSpacer());

			return rowContainer;
		}
	}
}