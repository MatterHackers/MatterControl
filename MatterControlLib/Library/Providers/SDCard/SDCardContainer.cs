﻿/*
Copyright (c) 2018, John Lewin
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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;

namespace MatterHackers.MatterControl.Library
{
	public class SDCardContainer : LibraryContainer
	{
		private bool gotBeginFileList;

		private PrinterConfig printer;

		private AutoResetEvent autoResetEvent;

		public SDCardContainer(PrinterConfig printer)
		{
			this.ChildContainers = new List<ILibraryContainerLink>();
			this.Items = new List<ILibraryItem>();
			this.Name = "SD Card".Localize();
			this.printer = printer;
			void CommunicationStateChanged(object s, EventArgs e)
			{
				switch (printer.Connection.CommunicationState)
				{
					case CommunicationStates.Connected:
						Task.Run(() =>
						{
							this.Load();

							UiThread.RunOnIdle(this.OnContentChanged);
						});
						break;

					case CommunicationStates.Disconnected:
						this.Items.Clear();
						this.OnContentChanged();
						break;
				}
			}
			printer.Connection.CommunicationStateChanged += CommunicationStateChanged;
			printer.Disposed += (s, e) => printer.Connection.CommunicationStateChanged -= CommunicationStateChanged;
		}

		public override void Load()
		{
			this.Items.Clear();

			if (printer.Connection.IsConnected
				&& !(printer.Connection.PrinterIsPrinting || printer.Connection.PrinterIsPaused))
			{
				autoResetEvent = new AutoResetEvent(false);

				// Ask for files and listen for response
				gotBeginFileList = false;
				printer.Connection.LineReceived += Printer_LineRead;
				printer.Connection.QueueLine("M21\r\nM20");

				// Block and wait up to timeout for response
				autoResetEvent.WaitOne(40 * 1000);
			}
		}

		// Container override of child thumbnails
		public override Task<ImageBuffer> GetThumbnail(ILibraryItem item, int width, int height)
		{
			return Task.FromResult(
				AggContext.StaticData.LoadIcon(
					Path.Combine((width > 50 || height > 50) ? "icon_sd_card_115x115.png" : "icon_sd_card_50x50.png"),
					ApplicationController.Instance.Theme.InvertIcons));
		}

		private void Printer_LineRead(object sender, string line)
		{
			if (line != null)
			{
				if (!line.StartsWith("echo:"))
				{
					switch (line)
					{
						case "Begin file list":
							gotBeginFileList = true;
							this.Items.Clear();
							break;

						case "End file list":
							printer.Connection.LineReceived -= Printer_LineRead;

							// Release the Load WaitOne
							autoResetEvent.Set();
							break;

						default:
							if (gotBeginFileList)
							{
								string sdCardFileExtension = line.ToUpper();

								bool validSdCardItem = sdCardFileExtension.Contains(".GCO") || sdCardFileExtension.Contains(".GCODE");
								if (validSdCardItem)
								{
									this.Items.Add(new SDCardFileItem()
									{
										Name = line
									});
								}
							}
							break;
					}
				}
			}
		}

		public override void Dispose()
		{
			// Ensure released
			autoResetEvent?.Set();
		}
	}
}
