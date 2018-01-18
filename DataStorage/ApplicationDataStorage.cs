/*
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

using System;
using System.IO;

namespace MatterHackers.MatterControl.DataStorage
{
	public class ApplicationDataStorage
	{
		public bool FirstRun = false;

		//Describes the location for storing all local application data
		private static ApplicationDataStorage globalInstance;
		private static readonly string applicationDataFolderName = "MatterControl";
		private readonly string datastoreName = "MatterControl.db";
		private string applicationPath;
		private static string applicationUserDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), applicationDataFolderName);

		public ApplicationDataStorage()
		//Constructor - validates that local storage folder exists, creates if necessary
		{
			DirectoryInfo dir = new DirectoryInfo(ApplicationUserDataPath);
			if (!dir.Exists)
			{
				dir.Create();
			}

			Directory.CreateDirectory(this.LibraryAssetsPath);
		}

		/// <summary>
		/// Creates a global instance of ApplicationDataStorage
		/// </summary>
		public static ApplicationDataStorage Instance
		{
			get
			{
				if (globalInstance == null)
				{
					globalInstance = new ApplicationDataStorage();
				}
				return globalInstance;
			}
		}

		public string GetTempFileName(string fileExtension = null)
		{
			string tempFileName = string.IsNullOrEmpty(fileExtension) ?
				Path.GetRandomFileName() :
				Path.ChangeExtension(Path.GetRandomFileName(), "." + fileExtension.TrimStart('.'));

			return Path.Combine(this.ApplicationTempDataPath, tempFileName);
		}

		public string GetNewLibraryFilePath(string extension)
		{
			string filePath;

			// Loop until we've found a non-conflicting library path for the given extension
			do
			{
				filePath = Path.Combine(
					ApplicationDataStorage.Instance.ApplicationLibraryDataPath,
					Path.ChangeExtension(Path.GetRandomFileName(), extension));

			} while (File.Exists(filePath));

			return filePath;
		}

		public string ApplicationLibraryDataPath
		{
			get
			{
				string libraryPath = Path.Combine(ApplicationDataStorage.ApplicationUserDataPath, "Library");

				//Create library path if it doesn't exist
				DirectoryInfo dir = new DirectoryInfo(libraryPath);
				if (!dir.Exists)
				{
					dir.Create();
				}
				return libraryPath;
			}
		}

		public string LibraryAssetsPath => Path.Combine(this.ApplicationLibraryDataPath, "Assets");

		/// <summary>
		/// Overrides the AppData location.
		/// </summary>
		/// <param name="path">The new AppData path.</param>
		internal void OverrideAppDataLocation(string path)
		{
			Console.WriteLine("   Overriding ApplicationUserDataPath: " + path);

			// Ensure the target directory exists
			Directory.CreateDirectory(path);

			applicationUserDataPath = path;

			// Initialize a fresh datastore instance after overriding the AppData path
			Datastore.Instance = new Datastore();
			Datastore.Instance.Initialize();
		}

		public string ApplicationPath
		{
			get
			{
				if (this.applicationPath == null)
				{
					applicationPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
				}
				return applicationPath;
			}
		}

		/// <summary>
		/// Returns the application temp data folder
		/// </summary>
		/// <returns></returns>
		public string ApplicationTempDataPath
		{
			get
			{
				return Path.Combine(applicationUserDataPath, "data", "temp");
			}
		}

		public string PlatingDirectory
		{
			get
			{
				string platingDirectory = Path.Combine(ApplicationTempDataPath, "Plating");
				Directory.CreateDirectory(platingDirectory);

				return platingDirectory;
			}
		}

		public string DownloadsDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

		public string CustomLibraryFoldersPath { get; } = Path.Combine(applicationUserDataPath, "LibraryFolders.conf");

		/// <summary>
		/// Returns the application user data folder
		/// </summary>
		/// <returns></returns>
		public static string ApplicationUserDataPath
		{
			get
			{
				return applicationUserDataPath;
			}
		}

		/// <summary>
		/// Returns the path to the sqlite database
		/// </summary>
		/// <returns></returns>
		public string DatastorePath
		{
			get { return Path.Combine(ApplicationUserDataPath, datastoreName); }
		}

		/// <summary>
		/// Returns the gcode output folder
		/// </summary>
		/// <returns></returns>
		public string GCodeOutputPath
		{
			get
			{
				string gcodeOutputPath = Path.Combine(ApplicationUserDataPath, "data", "gcode");
				if (!Directory.Exists(gcodeOutputPath))
				{
					Directory.CreateDirectory(gcodeOutputPath);
				}
				return gcodeOutputPath;
			}
		}

#if __ANDROID__
		/// <summary>
		/// Returns the public storage folder (ex. download folder on Android)
		/// </summary>
		public string PublicDataStoragePath { get; } = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads).AbsolutePath;
#endif
	}
}