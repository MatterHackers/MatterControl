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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintLibrary.Provider;
using MatterHackers.MatterControl.SlicerConfiguration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace MatterHackers.MatterControl.PrintQueue
{
	public class PrintItemWrapper
	{
		public event EventHandler FileHasChanged;
		public event EventHandler SlicingDone;
		public event EventHandler SlicingOutputMessage;
		private static string fileNotFound = "File Not Found\n'{0}'".Localize();

		private static string readyToPrint = "Ready to Print".Localize();

		private static string slicingError = "Slicing Error".Localize();

		private bool doneSlicing;

		private long fileHashCode;

		private String fileType;

		private bool slicingHadError = false;

		private long writeTime = 0;

		private FileSystemWatcher diskFileWatcher = new FileSystemWatcher();

		public PrintItemWrapper(DataStorage.PrintItem printItem, List<ProviderLocatorNode> sourceLibraryProviderLocator = null)
		{
			this.PrintItem = printItem;
			UpdateFileTracking(FileLocation);

			this.fileType = Path.GetExtension(FileLocation).ToUpper();

			SourceLibraryProviderLocator = sourceLibraryProviderLocator;
		}

		public PrintItemWrapper(int printItemId)
		{
			this.PrintItem = DataStorage.Datastore.Instance.dbSQLite.Table<DataStorage.PrintItem>().Where(v => v.Id == printItemId).Take(1).FirstOrDefault();
			try
			{
				this.fileType = Path.GetExtension(this.FileLocation).ToUpper();
			}
			catch(Exception e)
			{
				Debug.Print(e.Message);
				Debugger.Break();
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
						slicingHadError = true;
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
									slicingHadError = false;
								}
							}
						}
						else
						{
							message = string.Format(fileNotFound, FileLocation);
						}

						OnSlicingOutputMessage(new StringEventArgs(message));

						if (SlicingDone != null)
						{
							SlicingDone(this, null);
						}
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

								// push the wirte time
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
							Debugger.Break();
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
			get { return this.PrintItem.FileLocation; }
			set
			{
				this.PrintItem.FileLocation = value;

				UpdateFileTracking(value);
			}
		}

		public string Name
		{
			get { return this.PrintItem.Name; }
			set
			{
				this.PrintItem.Name = value;
			}
		}

		Stopwatch timeSinceLastFileUpdate = new Stopwatch();
		private void UpdateFileTracking(string value)
		{
			if (value != diskFileWatcher.Filter)
			{
				if (Directory.Exists(Path.GetDirectoryName(value)))
				{
					diskFileWatcher.Path = Path.GetDirectoryName(value);
					diskFileWatcher.Filter = Path.GetFileName(value);

					diskFileWatcher.NotifyFilter = NotifyFilters.LastWrite;
					diskFileWatcher.Changed += SetFileChanged;
					diskFileWatcher.Created += SetFileChanged;

					// Begin watching.
					diskFileWatcher.EnableRaisingEvents = true;
				}
				else
				{
					diskFileWatcher.EnableRaisingEvents = false;
				}
			}
		}

		public void ReportFileChange()
		{
			if (timeSinceLastFileUpdate.IsRunning)
			{
				if (timeSinceLastFileUpdate.Elapsed.TotalSeconds > 1)
				{
					timeSinceLastFileUpdate.Stop();
					if (FileHasChanged != null)
					{
						FileHasChanged(this, null);
					}
				}
				else
				{
					UiThread.RunOnIdle(ReportFileChange, .05);
				}
			}
		}

		private void SetFileChanged(object sender, FileSystemEventArgs e)
		{
			timeSinceLastFileUpdate.Restart();

			UiThread.RunOnIdle(ReportFileChange, .05);
		}

		PrintItem printItem;
		public PrintItem PrintItem 
		{
			get { return printItem; }
			set
			{
				printItem = value;
				UpdateFileTracking(printItem.FileLocation);
			}
		}

		public bool SlicingHadError { get { return slicingHadError; } }

		public List<ProviderLocatorNode> SourceLibraryProviderLocator { get; private set; }

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

				string engineString = ((int)ActivePrinterProfile.Instance.ActiveSliceEngineType).ToString();

				string gcodeFileName = this.FileHashCode.ToString() + "_" + engineString + "_" + ActiveSliceSettings.Instance.GetHashCode().ToString();
				string gcodePathAndFileName = Path.Combine(DataStorage.ApplicationDataStorage.Instance.GCodeOutputPath, gcodeFileName + ".gcode");
				return gcodePathAndFileName;
			}
			else
			{
				return null;
			}
		}

		public bool IsGCodeFileComplete(string gcodePathAndFileName)
		{
			if (Path.GetExtension(FileLocation).ToUpper() == ".GCODE")
			{
				return true;
			}

			bool gCodeFileIsComplete = false;
			if (File.Exists(gcodePathAndFileName))
			{
				string gcodeFileContents = "";
				using (FileStream fileStream = new FileStream(gcodePathAndFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				{
					using (StreamReader gcodeStreamReader = new StreamReader(fileStream))
					{
						gcodeFileContents = gcodeStreamReader.ReadToEnd();
					}
				}

				// check if there is a known line at the end of the file (this will let us know if slicer finished building the file).
				switch (ActivePrinterProfile.Instance.ActiveSliceEngineType)
				{
					case ActivePrinterProfile.SlicingEngineTypes.CuraEngine:
					case ActivePrinterProfile.SlicingEngineTypes.MatterSlice:
					case ActivePrinterProfile.SlicingEngineTypes.Slic3r:
						if (gcodeFileContents.Contains("filament used ="))
						{
							gCodeFileIsComplete = true;
						}
						break;

					default:
						throw new NotImplementedException();
				}
			}

			return gCodeFileIsComplete;
		}

		public void OnSlicingOutputMessage(EventArgs e)
		{
			StringEventArgs message = e as StringEventArgs;
			if (SlicingOutputMessage != null)
			{
				SlicingOutputMessage(this, message);
			}
		}
	}
}