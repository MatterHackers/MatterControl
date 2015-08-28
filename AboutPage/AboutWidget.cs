﻿/*
Copyright (c) 2014, Lars Brubaker
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

using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ContactForm;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.HtmlParsing;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.MatterControl.PrintLibrary.Provider;
using MatterHackers.MatterControl.PrintQueue;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace MatterHackers.MatterControl
{
	public class AboutWidget : GuiWidget
	{
		public AboutWidget()
		{
			this.HAnchor = HAnchor.ParentLeftRight;
			this.VAnchor = VAnchor.ParentTop;

			this.Padding = new BorderDouble(5);
			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			FlowLayoutWidget customInfoTopToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			customInfoTopToBottom.Name = "AboutPageCustomInfo";
			customInfoTopToBottom.HAnchor = HAnchor.ParentLeftRight;
			customInfoTopToBottom.VAnchor = VAnchor.Max_FitToChildren_ParentHeight;
			customInfoTopToBottom.Padding = new BorderDouble(5, 10, 5, 0);

			if (ActiveTheme.Instance.IsTouchScreen)
			{
				customInfoTopToBottom.AddChild(new UpdateControlView());
			}

			//AddMatterHackersInfo(customInfoTopToBottom);
			customInfoTopToBottom.AddChild(new GuiWidget(1, 10));

			string aboutHtmlFile = Path.Combine("OEMSettings", "AboutPage.html");
			string htmlContent = StaticData.Instance.ReadAllText(aboutHtmlFile); 

#if false // test
			{
				SystemWindow releaseNotes = new SystemWindow(640, 480);
				string releaseNotesFile = Path.Combine("OEMSettings", "ReleaseNotes.html");
				string releaseNotesContent = StaticData.Instance.ReadAllText(releaseNotesFile);
				HtmlWidget content = new HtmlWidget(releaseNotesContent, RGBA_Bytes.Black);
				content.AddChild(new GuiWidget(HAnchor.AbsolutePosition, VAnchor.ParentBottomTop));
				content.VAnchor |= VAnchor.ParentTop;
				content.BackgroundColor = RGBA_Bytes.White;
				releaseNotes.AddChild(content);
				releaseNotes.BackgroundColor = RGBA_Bytes.Cyan;
				UiThread.RunOnIdle((state) =>
				{
					releaseNotes.ShowAsSystemWindow();
				}, 1);
			}
#endif

			HtmlWidget htmlWidget = new HtmlWidget(htmlContent, ActiveTheme.Instance.PrimaryTextColor);

			customInfoTopToBottom.AddChild(htmlWidget);

			this.AddChild(customInfoTopToBottom);
		}

		public static void DeleteCacheData()
		{
			if(LibraryProviderSQLite.Instance.PreloadingCalibrationFiles)
			{
				return;
			}

			// delete everything in the GCodeOutputPath
			//   AppData\Local\MatterControl\data\gcode
			// delete everything in the temp data that is not in use
			//   AppData\Local\MatterControl\data\temp
			//     plateImages
			//     project-assembly
			//     project-extract
			//     stl
			// delete all unreference models in Library
			//   AppData\Local\MatterControl\Library
			// delete all old update downloads
			//   AppData\updates

			// start cleaning out unused data
			// MatterControl\data\gcode
			RemoveDirectory(DataStorage.ApplicationDataStorage.Instance.GCodeOutputPath);

			string userDataPath = DataStorage.ApplicationDataStorage.ApplicationUserDataPath;
			RemoveDirectory(Path.Combine(userDataPath, "updates"));

			HashSet<string> referencedPrintItemsFilePaths = new HashSet<string>();
			HashSet<string> referencedThumbnailFiles = new HashSet<string>();
			// Get a list of all the stl and amf files referenced in the queue.
			foreach (PrintItemWrapper printItem in QueueData.Instance.PrintItems)
			{
				string fileLocation = printItem.FileLocation;
				if (!referencedPrintItemsFilePaths.Contains(fileLocation))
				{
					referencedPrintItemsFilePaths.Add(fileLocation);
					referencedThumbnailFiles.Add(PartThumbnailWidget.GetImageFileName(printItem));
				}
			}

			// Add in all the stl and amf files referenced in the library.
			foreach (PrintItem printItem in LibraryProviderSQLite.GetAllPrintItemsRecursive())
			{
				PrintItemWrapper printItemWrapper = new PrintItemWrapper(printItem);
				string fileLocation = printItem.FileLocation;
				if (!referencedPrintItemsFilePaths.Contains(fileLocation))
				{
					referencedPrintItemsFilePaths.Add(fileLocation);
					referencedThumbnailFiles.Add(PartThumbnailWidget.GetImageFileName(printItemWrapper));
				}
			}

			// If the count is less than 0 then we have never run and we need to populate the library and queue still. So don't delete anything yet.
			if (referencedPrintItemsFilePaths.Count > 0)
			{
				CleanDirectory(userDataPath, referencedPrintItemsFilePaths, referencedThumbnailFiles);
			}
		}

		public string CreateCenteredButton(string content)
		{
			throw new NotImplementedException();
		}

		public string CreateLinkButton(string content)
		{
			throw new NotImplementedException();
		}

		public string DoToUpper(string content)
		{
			throw new NotImplementedException();
		}

		public string DoTranslate(string content)
		{
			throw new NotImplementedException();
		}

		public string GetBuildString(string content)
		{
			return VersionInfo.Instance.BuildVersion;
		}

		public string GetVersionString(string content)
		{
			return VersionInfo.Instance.ReleaseVersion;
		}

		private static int CleanDirectory(string path, HashSet<string> referencedPrintItemsFilePaths, HashSet<string> referencedThumbnailFiles)
		{
			int contentCount = 0;
			foreach (string directory in Directory.EnumerateDirectories(path))
			{
				int directoryContentCount = CleanDirectory(directory, referencedPrintItemsFilePaths, referencedThumbnailFiles);
				if (directoryContentCount == 0)
				{
					try
					{
						Directory.Delete(directory);
					}
					catch (Exception)
					{
					}
				}
				else
				{
					// it has a directory that has content
					contentCount++;
				}
			}

			foreach (string file in Directory.EnumerateFiles(path, "*.*"))
			{
				switch (Path.GetExtension(file).ToUpper())
				{
					case ".STL":
					case ".AMF":
					case ".GCODE":
						// 
						if (referencedPrintItemsFilePaths.Contains(file) || LibraryProviderSQLite.Instance.PreloadingCalibrationFiles && Path.GetDirectoryName(file).Contains("calibration-parts"))
						{
							contentCount++;
						}
						else
						{
							try
							{
								File.Delete(file);
							}
							catch (Exception)
							{
							}
						}
						break;

					case ".PNG":
					case ".TGA":
						if (referencedThumbnailFiles.Contains(file))
						{
							contentCount++;
						}
						else
						{
							try
							{
								File.Delete(file);
							}
							catch (Exception)
							{
							}
						}
						break;

					case ".JSON":
						// may want to clean these up eventually
						contentCount++; // if we delete these we should not incement this
						break;

					default:
						// we have something in the directory that we are not going to delete
						contentCount++;
						break;
				}
			}

			return contentCount;
		}

		private static void RemoveDirectory(string directoryToRemove)
		{
			try
			{
				if (Directory.Exists(directoryToRemove))
				{
					Directory.Delete(directoryToRemove, true);
				}
			}
			catch (Exception)
			{
			}
		}
	}
}