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

using System;
using System.IO;
using MatterControl.Printing;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;

namespace MatterHackers.MatterControl
{
	using MatterHackers.DataConverters3D;
	using MatterHackers.MatterControl.Library;

	public class EditContext
	{
		private ILibraryItem _sourceItem;

		public IContentStore ContentStore { get; set; }

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
						printItem = new PrintItemWrapper(new PrintItem(fileItem.FileName, fileItem.Path));
					}
				}
			}
		}

		// Natural path
		private string GCodePath(PrinterConfig printer)
		{
			if (printItem != null)
			{
				return printer.GetGCodePathAndFileName(printItem.FileLocation);
			}
			return null;
		}

		// Override path
		public string GCodeOverridePath(PrinterConfig printer)
		{
			return Path.ChangeExtension(GCodePath(printer), GCodeFile.PostProcessedExtension);
		}

		// Override or natural path
		public string GCodeFilePath(PrinterConfig printer)
		{
			if (File.Exists(this.GCodeOverridePath(printer)))
			{
				return this.GCodeOverridePath(printer);
			}
			
			return GCodePath(printer);
		}

		public string SourceFilePath => printItem?.FileLocation;

		public bool FreezeGCode { get; set; }

		/// <summary>
		/// Short term stop gap that should only be used until GCode path helpers, hash code and print recovery components can be extracted
		/// </summary>
		[Obsolete]
		internal PrintItemWrapper printItem { get; set; }

		internal void Save(IObject3D scene)
		{
			if (!this.FreezeGCode)
			{
				ApplicationController.Instance.Thumbnails.DeleteCache(this.SourceItem);

				// Call save on the provider
				this.ContentStore?.Save(this.SourceItem, scene);
			}
		}
	}
}