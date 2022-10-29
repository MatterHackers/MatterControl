/*
Copyright (c) 2022, John Lewin, Lars Brubaker
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
using MatterHackers.Localizations;
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace MatterHackers.MatterControl.Library
{
	/// <summary>
	/// A library item rooted to a local file system path
	/// </summary>
	public class FileSystemItem : ILibraryItem
	{
		public FileSystemItem(string filePath)
		{
			this.FilePath = filePath;
			var type = GetType();

			try
			{
				if (type == typeof(FileSystemFileItem))
				{
					var fileInfo = new FileInfo(filePath);

					this.DateCreated = fileInfo.CreationTime;
					this.DateModified = fileInfo.LastWriteTime;
				}
				else
				{
					var directoryInfo = new DirectoryInfo(filePath);

					this.DateCreated = directoryInfo.CreationTime;
					this.DateModified = directoryInfo.LastWriteTime;
				}
			}
			catch
			{
				this.DateCreated = DateTime.Now;
				this.DateModified = DateTime.Now;
			}
		}

		public string Category { get; set; }

		public DateTime DateCreated { get; }

		public DateTime DateModified { get; }

		public virtual string ID => agg_basics.GetLongHashCode(this.FilePath).ToString();

		public virtual bool IsProtected => false;

		public virtual bool IsVisible => true;

		public virtual bool LocalContentExists => true;

		public event EventHandler NameChanged;

		public virtual string Name
		{
			get
			{
				var finalDirectory = Path.GetFileName(this.FilePath);
				if (string.IsNullOrEmpty(finalDirectory))
				{
					if (FilePath.Length > 0)
					{
						return $"{FilePath[0]} " + "Drive".Localize();
					}
					else
					{
						return "Unknown".Localize();
					}
				}

				return finalDirectory;
			}

			set
			{
				if (Name != value)
				{
					string sourceFile = this.FilePath;
					if (File.Exists(sourceFile))
					{
						var extension = Path.GetExtension(sourceFile);
						var fileNameNumberMatch = new Regex("\\s*\\(\\d+\\)" + extension, RegexOptions.Compiled);

						var directory = Path.GetDirectoryName(sourceFile);
						var destName = value;
						var destPathAndName = Path.Combine(directory, Path.ChangeExtension(destName, extension));

						var uniqueFileIncrement = 0;
						while(File.Exists(destPathAndName))
                        {
							// remove any number
							destName = fileNameNumberMatch.Replace(sourceFile, "");
							// add the new number
							destName += $" ({++uniqueFileIncrement})";
							destName = Path.ChangeExtension(destName, extension);
							destPathAndName = Path.Combine(directory, Path.ChangeExtension(destName, extension));

							if (sourceFile == destPathAndName)
                            {
								// we have gotten back to the name we currently have (don't change it)
								break;
                            }
						}

						if (sourceFile != destPathAndName)
						{
							File.Move(sourceFile, destPathAndName);

							this.FilePath = destPathAndName;

							NameChanged?.Invoke(this, EventArgs.Empty);
						}
					}
				}
			}
		}

		public string FilePath { get; set; }
	}
}