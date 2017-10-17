/*
Copyright (c) 2014, Kevin Pope
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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrintQueue
{
	public static class PrintItemWrapperExtensionMethods
	{
		private static TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;

		public static string GetFriendlyName(this PrintItemWrapper printItemWrapper)
		{
			if (printItemWrapper?.Name == null)
			{
				return "";
			}

			return textInfo?.ToTitleCase(printItemWrapper.Name.Replace('_', ' '));
		}

		public static string GetFriendlyName(string fileName)
		{
			if (fileName == null)
			{
				return "";
			}

			return textInfo?.ToTitleCase(fileName.Replace('_', ' '));
		}
	}

	public class PrintItemWrapper
	{
		public event EventHandler SlicingDone;

		private string fileNotFound = "File Not Found\n'{0}'".Localize();
		private string readyToPrint = "Ready to Print".Localize();
		private string slicingError = "Slicing Error".Localize();

		private bool doneSlicing;

		private long fileHashCode;

		private string fileType;

		private long writeTime = 0;

		public PrintItemWrapper(PrintItem printItem, ILibraryContainer sourceLibraryProviderLocator = null)
		{
			this.PrintItem = printItem;

			if (FileLocation != null)
			{
				this.fileType = Path.GetExtension(FileLocation).ToUpper();
			}

			SourceLibraryProviderLocator = sourceLibraryProviderLocator;
		}

		public PrintItemWrapper(int printItemId)
		{
			this.PrintItem = Datastore.Instance.dbSQLite.Table<PrintItem>().Where(v => v.Id == printItemId).Take(1).FirstOrDefault();
			try
			{
				this.fileType = Path.GetExtension(this.FileLocation).ToUpper();
			}
			catch(Exception e)
			{
				Debug.Print(e.Message);
				GuiWidget.BreakInDebugger();
				//file not found
			}
		}

		public bool CurrentlySlicing { get; set; }

		public bool DoneSlicing
		{
			get
			{
				return doneSlicing;
			}

			set
			{
				if (value != doneSlicing)
				{
					doneSlicing = value;
					if (doneSlicing)
					{
						string message = slicingError;
						this.SlicingHadError = true;
						if (File.Exists(FileLocation))
						{
							string gcodePathAndFileName = GetGCodePathAndFileName();
							if (gcodePathAndFileName != "" && File.Exists(gcodePathAndFileName))
							{
								FileInfo info = new FileInfo(gcodePathAndFileName);
								// This is really just to make sure it is bigger than nothing.
								if (info.Length > 10)
								{
									message = readyToPrint;
									this.SlicingHadError = false;
								}
							}
						}
						else
						{
							message = string.Format(fileNotFound, FileLocation);
						}

						OnSlicingOutputMessage(new StringEventArgs(message));

						SlicingDone?.Invoke(this, null);
					}
				}
			}
		}

		public long FileHashCode
		{
			get
			{
				bool fileExists = System.IO.File.Exists(this.FileLocation);
				if (fileExists)
				{
					long currentWriteTime = File.GetLastWriteTime(this.FileLocation).ToBinary();

					if (this.fileHashCode == 0 || writeTime != currentWriteTime)
					{
						writeTime = currentWriteTime;
						try
						{
							using (FileStream fileStream = new FileStream(this.FileLocation, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
							{
								long sizeOfFile = fileStream.Length;
								int sizeOfRead = 1 << 16;
								byte[] readData = new byte[Math.Max(64, sizeOfRead * 3)];

								// get a chuck from the begining
								fileStream.Read(readData, sizeOfRead, sizeOfRead);

								// the middle
								fileStream.Seek(sizeOfFile / 2, SeekOrigin.Begin);
								fileStream.Read(readData, sizeOfRead * 1, sizeOfRead);

								// and the end
								fileStream.Seek(Math.Max(0, sizeOfFile - sizeOfRead), SeekOrigin.Begin);
								fileStream.Read(readData, sizeOfRead * 2, sizeOfRead);

								// push the file size into the first bytes
								byte[] fileSizeAsBytes = BitConverter.GetBytes(sizeOfFile);
								for (int i = 0; i < fileSizeAsBytes.Length; i++)
								{
									readData[i] = fileSizeAsBytes[i];
								}

								// push the write time
								byte[] writeTimeAsBytes = BitConverter.GetBytes(currentWriteTime);
								for (int i = 0; i < writeTimeAsBytes.Length; i++)
								{
									readData[fileSizeAsBytes.Length + i] = fileSizeAsBytes[i];
								}

								this.fileHashCode = agg_basics.ComputeHash(readData);
							}
						}
						catch(Exception e)
						{
							Debug.Print(e.Message);
							GuiWidget.BreakInDebugger();
							this.fileHashCode = 0;
						}
					}
				}
				else
				{
					this.fileHashCode = 0;
				}

				return this.fileHashCode;
			}
		}

		public string FileLocation
		{
			get  => this.PrintItem.FileLocation;
			set => this.PrintItem.FileLocation = value;
		}

		public string Name
		{
			get => this.PrintItem.Name;
			set => this.PrintItem.Name = value;
		}

		public PrintItem PrintItem { get; set; }

		public bool SlicingHadError { get; private set; } = false;

		public ILibraryContainer SourceLibraryProviderLocator { get; private set; }

		public bool UseIncrementedNameDuringTypeChange { get; internal set; }

		public void Delete()
		{
			PrintItem.Delete();

			// Reset the Id field after calling delete to clear the association and ensure that future db operations
			// result in inserts rather than update statements on a missing row
			this.PrintItem.Id = 0;
		}

		public string GetGCodePathAndFileName()
		{
			if (FileLocation.Trim() != "")
			{
				if (Path.GetExtension(FileLocation).ToUpper() == ".GCODE")
				{
					return FileLocation;
				}

				string engineString = (0).ToString();

				string gcodeFileName = this.FileHashCode.ToString() + "_" + engineString + "_" + ActiveSliceSettings.Instance.GetLongHashCode().ToString();
				string gcodePathAndFileName = Path.Combine(ApplicationDataStorage.Instance.GCodeOutputPath, gcodeFileName + ".gcode");
				return gcodePathAndFileName;
			}
			else
			{
				return null;
			}
		}

		public void OnSlicingOutputMessage(EventArgs e)
		{
		}
	}
}