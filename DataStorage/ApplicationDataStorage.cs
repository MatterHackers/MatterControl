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
		private const string applicationDataFolderName = "MatterControl";
		private const string datastoreName = "MatterControl.db";

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

		private string _applicationPath;
		public string ApplicationPath
		{
			get
			{
				if (_applicationPath == null)
				{
					_applicationPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
				}

				return _applicationPath;
			}
		}

		private static string _applicationUserDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), applicationDataFolderName);
		public static string ApplicationUserDataPath => EnsurePath(_applicationUserDataPath);

		private static string _applicationLibraryDataPath = Path.Combine(ApplicationUserDataPath, "Library");
		public string ApplicationLibraryDataPath => EnsurePath(_applicationLibraryDataPath);

		private static string _libraryAssetPath = Path.Combine(_applicationLibraryDataPath, "Assets");
		public string LibraryAssetsPath => EnsurePath(_libraryAssetPath);

		private static string _applicationTempDataPath = Path.Combine(_applicationUserDataPath, "data", "temp");
		public string ApplicationTempDataPath => EnsurePath(_applicationTempDataPath);

		private static string _platingDirectory = Path.Combine(_applicationLibraryDataPath, "Plating");
		public string PlatingDirectory => EnsurePath(_platingDirectory);

		public string DownloadsDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

		public string CustomLibraryFoldersPath { get; } = Path.Combine(_applicationUserDataPath, "LibraryFolders.conf");

		/// <summary>
		/// Returns the path to the sqlite database
		/// </summary>
		/// <returns></returns>
		public string DatastorePath { get; } = Path.Combine(_applicationUserDataPath, datastoreName);

		private static string _gcodeOutputPath = Path.Combine(_applicationUserDataPath, "data", "gcode");
		public string GCodeOutputPath => EnsurePath(_gcodeOutputPath);

#if __ANDROID__
		/// <summary>
		/// Returns the public storage folder (ex. download folder on Android)
		/// </summary>
		public string PublicDataStoragePath { get; } = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads).AbsolutePath;
#endif

		/// <summary>
		/// Invokes CreateDirectory on all paths, creating if missing, before returning
		/// </summary>
		/// <returns></returns>
		private static string EnsurePath(string fullPath)
		{
			Directory.CreateDirectory(fullPath);
			return fullPath;
		}

		/// <summary>
		/// Overrides the AppData location. Used by tests to set a non-standard AppData location
		/// </summary>
		/// <param name="path">The new AppData path.</param>
		internal void OverrideAppDataLocation(string path)
		{
			Console.WriteLine("   Overriding ApplicationUserDataPath: " + path);

			// Ensure the target directory exists
			Directory.CreateDirectory(path);

			_applicationUserDataPath = path;

			// Initialize a fresh datastore instance after overriding the AppData path
			Datastore.Instance = new Datastore();
			Datastore.Instance.Initialize();
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
					_applicationLibraryDataPath,
					Path.ChangeExtension(Path.GetRandomFileName(), extension));

			} while (File.Exists(filePath));

			return filePath;
		}
	}
}