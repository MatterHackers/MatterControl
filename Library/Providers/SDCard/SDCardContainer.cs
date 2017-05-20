/*
Copyright (c) 2017, John Lewin
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
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;

namespace MatterHackers.MatterControl.Library
{
	public class SDCardContainer : LibraryContainer
	{
		private bool gotBeginFileList;

		private EventHandler unregisterEvents;

		public SDCardContainer()
		{
			this.ChildContainers = new List<ILibraryContainerLink>();
			this.Items = new List<ILibraryItem>();
			this.Name = "SD Card".Localize();
			LoadFilesFromSD();
		}

		public void LoadFilesFromSD()
		{
			var printer = PrinterConnectionAndCommunication.Instance;

			if (printer.PrinterIsConnected
				&& !(printer.PrinterIsPrinting || printer.PrinterIsPaused))
			{

				printer.ReadLine.RegisterEvent(Printer_LineRead, ref unregisterEvents);

				gotBeginFileList = false;
				printer.SendLineToPrinterNow("M21\r\nM20");
			}
		}

		// Container override of child thumbnails
		public override Task<ImageBuffer> GetThumbnail(ILibraryItem item, int width, int height)
		{
			bool largeIcon = width > 50 || height > 50;
			var thumbnail = LibraryProviderHelpers.LoadInvertIcon(largeIcon ? "icon_sd_card_115x115.png" : "icon_sd_card_50x50.png");

			return Task.FromResult(thumbnail);
		}

		private void Printer_LineRead(object sender, EventArgs e)
		{
			var currentEvent = e as StringEventArgs;
			if (currentEvent != null)
			{
				if (!currentEvent.Data.StartsWith("echo:"))
				{
					switch (currentEvent.Data)
					{
						case "Begin file list":
							gotBeginFileList = true;
							this.Items.Clear();
							break;

						default:
							if (gotBeginFileList)
							{
								string sdCardFileExtension = currentEvent.Data.ToUpper();

								bool validSdCardItem = sdCardFileExtension.Contains(".GCO") || sdCardFileExtension.Contains(".GCODE");
								if (validSdCardItem)
								{
									this.Items.Add(new SDCardFileItem()
									{
										Name = currentEvent.Data
									});
								}
							}
							break;

						case "End file list":
							PrinterConnectionAndCommunication.Instance.ReadLine.UnregisterEvent(Printer_LineRead, ref unregisterEvents);
							this.OnReloaded();
							break;
					}
				}
			}
		}

		public override void Dispose()
		{
			// In case "End file list" is never received
			PrinterConnectionAndCommunication.Instance.ReadLine.UnregisterEvent(Printer_LineRead, ref unregisterEvents);
		}
	}
}
