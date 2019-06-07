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
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace MatterHackers.MatterControl.DataStorage
{
	[SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Private getters used to enforce presence of directories")]
	public class ApplicationDataStorage
	{
		// Required by Android
		public bool FirstRun { get; set; } = false;

		// Describes the location for storing all local application data
		private static ApplicationDataStorage globalInstance;

		private const string ApplicationDataFolderName = "MatterControl";

		private const string DatastoreName = "MatterControl.db";

		private string _applicationPath;

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

		private static string _applicationUserDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ApplicationDataFolderName);

		private static string _applicationLibraryDataPath => Path.Combine(_applicationUserDataPath, "Library");

		private static string _libraryAssetPath => Path.Combine(_applicationLibraryDataPath, "Assets");

		private static string _platingDirectory => Path.Combine(_applicationLibraryDataPath, "Plating");

		private static string _applicationTempDataPath => Path.Combine(_applicationUserDataPath, "data", "temp");

		private static string _gcodeOutputPath => Path.Combine(_applicationTempDataPath, "gcode");

		private static string _cacheDirectory => Path.Combine(_applicationTempDataPath, "cache");

		private static string _printHistoryPath => Path.Combine(_applicationLibraryDataPath, "PrintHistory");

		private static string _cloudLibraryPath => Path.Combine(_applicationLibraryDataPath, "CloudData");

		public static string ApplicationUserDataPath => EnsurePath(_applicationUserDataPath);

		public string ApplicationLibraryDataPath => EnsurePath(_applicationLibraryDataPath);

		public string CloudLibraryPath => EnsurePath(_cloudLibraryPath);

		public string LibraryAssetsPath => EnsurePath(_libraryAssetPath);

		public string ApplicationTempDataPath => EnsurePath(_applicationTempDataPath);

		public string PlatingDirectory => EnsurePath(_platingDirectory);

		public string GCodeOutputPath => EnsurePath(_gcodeOutputPath);

		public string CacheDirectory => EnsurePath(_cacheDirectory);

		public string PrintHistoryPath => EnsurePath(_printHistoryPath);

		public string DownloadsDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

		public string CustomLibraryFoldersPath => Path.Combine(_applicationUserDataPath, "LibraryFolders.conf");

		/// <summary>
		/// Gets the path to the Sqlite database
		/// </summary>
		/// <returns>The path toe Sqlite database</returns>
		public string DatastorePath => Path.Combine(EnsurePath(_applicationUserDataPath), DatastoreName);

		/// <summary>
		/// Gets or sets the public storage folder (ex. download folder on Android)
		/// </summary>
		public string PublicDataStoragePath { get; set;  }

		/// <summary>
		/// Invokes CreateDirectory on all paths, creating if missing, before returning
		/// </summary>
		/// <returns>Returns the  path to the given directory</returns>
		private static string EnsurePath(string fullPath)
		{
			Directory.CreateDirectory(fullPath);
			return fullPath;
		}

		/// <summary>
		/// Overrides the AppData location. Used by tests to set a non-standard AppData location
		/// </summary>
		/// <param name="path">The new AppData path.</param>
		/// <param name="sqliteBuilder">The Sqlite generator with platform specific bindings</param>
		internal void OverrideAppDataLocation(string path, Func<ISQLite> sqliteBuilder)
		{
			Console.WriteLine("   Overriding ApplicationUserDataPath: " + path);

			// Ensure the target directory exists
			Directory.CreateDirectory(path);

			_applicationUserDataPath = path;

			// Initialize a fresh datastore instance after overriding the AppData path
			Datastore.Instance = new Datastore();
			Datastore.Instance.Initialize(sqliteBuilder.Invoke());
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

			// Force lowercase file extensions
			extension = extension.ToLower();

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