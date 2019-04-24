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

using System.Collections.Generic;
using System.IO;
using MatterHackers.Agg.Platform;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.SlicerConfiguration;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl
{
	public class PartWorkspace
	{
		public PartWorkspace()
		{
		}

		[JsonIgnore]
		public ILibraryContext LibraryView { get; set; }

		public PartWorkspace(PrinterConfig printer)
			: this (printer.Bed)
		{
			this.Printer = printer;
			this.PrinterID = printer.Settings.ID;

			if (this.LibraryView.ActiveContainer is WrappedLibraryContainer wrappedLibrary)
			{
				wrappedLibrary.ExtraContainers.Add(
					new DynamicContainerLink(
						() => printer.Settings.GetValue(SettingsKey.printer_name),
						AggContext.StaticData.LoadIcon(Path.Combine("Library", "sd_20x20.png")),
						AggContext.StaticData.LoadIcon(Path.Combine("Library", "sd_folder.png")),
						() => new PrinterContainer(printer))
					{
						IsReadOnly = true
					});
			}
		}

		public PartWorkspace(ISceneContext bedConfig)
		{
			// Create a new library context for the SaveAs view
			this.LibraryView = new LibraryConfig()
			{
				ActiveContainer = new WrappedLibraryContainer(ApplicationController.Instance.Library.RootLibaryContainer)
			};

			this.SceneContext = bedConfig;
			Name = bedConfig.EditContext?.SourceItem?.Name ?? "Unknown";
		}

		public string Name { get; set; }

		[JsonIgnore]
		public ISceneContext SceneContext { get; }

		public EditContext EditContext { get; set; }

		public string PrinterID { get; set; }

		[JsonIgnore]
		public PrinterConfig Printer { get; }

		public string ContentPath { get; set; }
	}
}