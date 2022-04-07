/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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

using System.IO;
using MatterControl.Printing;
using MatterHackers.Agg;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.Library;
using Newtonsoft.Json;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl
{
	public class EditContext
	{
		/// <summary>
		/// The object responsible for item persistence 
		/// </summary>
		public IContentStore ContentStore { get; set; }

		public string SourceFilePath
		{
			get
			{
				if (SourceItem is ILibraryAsset fileItem)
				{
					return fileItem.AssetPath;
				}

				return null;
			}
		}

		public bool FreezeGCode { get; set; }

		public event EventHandler SourceItemChanged;

		private ILibraryItem _sourceItem;
		/// <summary>
		/// The library item to load and persist
		/// </summary>
		public ILibraryItem SourceItem
		{
			get => _sourceItem;
			
			set
			{
				if (value != _sourceItem)
                {
					_sourceItem = value;
					SourceItemChanged?.Invoke(this, EventArgs.Empty);
                }
			}
		}

		public bool IsGGCodeSource => (this.SourceItem as ILibraryAsset)?.ContentType == "gcode";

		// Override or natural path
		public async Task<string> GCodeFilePath(PrinterConfig printer)
		{
			var path = await this.GCodeOverridePath(printer);
			if (File.Exists(path))
			{
				return path;
			}

			return await GCodePath(printer);
		}

		public static string GCodeFilePath(PrinterConfig printer, IObject3D object3D)
		{
			using (var memoryStream = new MemoryStream())
			{
				// Write JSON
				object3D.SaveTo(memoryStream);

				// Reposition
				memoryStream.Position = 0;

				// Calculate
				string fileHashCode = HashGenerator.ComputeSHA1(memoryStream);

				ulong settingsHashCode = printer.Settings.GetGCodeCacheKey();

				return Path.Combine(
					ApplicationDataStorage.Instance.GCodeOutputPath,
					$"{fileHashCode}_{ settingsHashCode}.gcode");
			}
		}

		public async Task Save(IObject3D scene)
		{
			if (!this.FreezeGCode)
			{
				if (this.SourceItem != null)
				{
					ApplicationController.Instance.Thumbnails.DeleteCache(this.SourceItem);
				}

				if (scene is InteractiveScene interactiveScene)
				{
					using (new SelectionMaintainer(interactiveScene))
					{
						// Call save on the provider
						await this.ContentStore?.Save(this.SourceItem, scene);
					}

					interactiveScene.MarkSavePoint();
				}
				else
                {
					// Call save on the provider
					await this.ContentStore?.Save(this.SourceItem, scene);
				}
			}
		}
        
        // Natural path
        private async Task<string> GCodePath(PrinterConfig printer)
		{
			if (File.Exists(this.SourceFilePath))
			{
				return await this.GetGCodePath(printer, this.SourceFilePath);
			}

			return null;
		}

		public static string[] GetFileNamesFromMcx(string mcxFileName)
		{
			// add in the cache path
			mcxFileName = Path.Combine(ApplicationDataStorage.Instance.PlatingDirectory, mcxFileName);
			if (File.Exists(mcxFileName))
			{
				var document = JsonConvert.DeserializeObject<McxDocument.McxNode>(File.ReadAllText(mcxFileName));
				var names = document.AllVisibleMeshFileNames();
				return names.GroupBy(n => n)
					.Select(g => g.Key)
					.OrderBy(n => n)
					.ToArray();
			}

			return null;
		}


		/// <summary>
		/// Returns the computed GCode path given a content file path and considering current settings
		/// </summary>
		/// <param name="printer">The associated printer</param>
		/// <param name="fileLocation">The source file</param>
		/// <returns>The target GCode path</returns>
		private async Task<string> GetGCodePath(PrinterConfig printer, string fileLocation)
		{
			if (fileLocation.Trim() != "")
			{
				if (Path.GetExtension(fileLocation).ToUpper() == ".GCODE")
				{
					return fileLocation;
				}

				string fileHashCode = HashGenerator.ComputeFileSHA1(fileLocation);

				var fileExtension = Path.GetExtension(fileLocation).ToUpper();
				if (fileExtension == ".MCX")
				{
					var hashCode = fileHashCode.GetLongHashCode();
					var fileNames = GetFileNamesFromMcx(fileLocation);
					foreach (var file in fileNames)
					{
						var fullPath = Object3DExtensions.ResolveFilePath(file, null, CancellationToken.None);
						if (File.Exists(fullPath))
						{
							hashCode = File.GetLastWriteTime(fullPath).ToString().GetLongHashCode(hashCode);
						}
					}

					fileHashCode = hashCode.ToString();
				}

				ulong settingsHashCode = printer.Settings.GetGCodeCacheKey();

				return Path.Combine(
					ApplicationDataStorage.Instance.GCodeOutputPath,
					$"{fileHashCode}_{ settingsHashCode}.gcode");
			}
			else
			{
				return null;
			}
		}

		// Override path
		private async Task<string> GCodeOverridePath(PrinterConfig printer)
		{
			return Path.ChangeExtension(await GCodePath(printer), GCodeFile.PostProcessedExtension);
		}
	}
}