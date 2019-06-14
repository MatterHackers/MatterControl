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

namespace MatterHackers.MatterControl
{
	using MatterHackers.Agg;
	using MatterHackers.DataConverters3D;
	using MatterHackers.MatterControl.DataStorage;
	using MatterHackers.MatterControl.Library;

	public class EditContext
	{
		private ILibraryItem _sourceItem;

		/// <summary>
		/// The object responsible for item persistence 
		/// </summary>
		public IContentStore ContentStore { get; set; }

		public string SourceFilePath { get; private set; }

		public bool FreezeGCode { get; set; }

		/// <summary>
		/// The library item to load and persist
		/// </summary>
		public ILibraryItem SourceItem
		{
			get => _sourceItem;
			set
			{
				if (_sourceItem != value)
				{
					_sourceItem = value;

					if (_sourceItem is FileSystemFileItem fileItem)
					{
						this.SourceFilePath = fileItem.Path;
					}
				}
			}
		}

		public bool IsGGCodeSource => (this.SourceItem as ILibraryAsset)?.ContentType == "gcode";

		// Override or natural path
		public string GCodeFilePath(PrinterConfig printer)
		{
			if (File.Exists(this.GCodeOverridePath(printer)))
			{
				return this.GCodeOverridePath(printer);
			}

			return GCodePath(printer);
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

		internal void Save(IObject3D scene)
		{
			if (!this.FreezeGCode)
			{
				ApplicationController.Instance.Thumbnails.DeleteCache(this.SourceItem);

				// Call save on the provider
				this.ContentStore?.Save(this.SourceItem, scene);
			}
		}

		// Natural path
		private string GCodePath(PrinterConfig printer)
		{
			if (File.Exists(this.SourceFilePath))
			{
				return this.GetGCodePath(printer, this.SourceFilePath);
			}

			return null;
		}

		/// <summary>
		/// Returns the computed GCode path given a content file path and considering current settings
		/// </summary>
		/// <param name="printer">The associated printer</param>
		/// <param name="fileLocation">The source file</param>
		/// <returns>The target GCode path</returns>
		private string GetGCodePath(PrinterConfig printer, string fileLocation)
		{
			if (fileLocation.Trim() != "")
			{
				if (Path.GetExtension(fileLocation).ToUpper() == ".GCODE")
				{
					return fileLocation;
				}

				string fileHashCode = HashGenerator.ComputeFileSHA1(fileLocation);
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
		private string GCodeOverridePath(PrinterConfig printer)
		{
			return Path.ChangeExtension(GCodePath(printer), GCodeFile.PostProcessedExtension);
		}
	}
}