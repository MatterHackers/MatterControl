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

using MatterHackers.Agg;
using System;
using System.IO;

namespace MatterHackers.MatterControl.Library
{
	/// <summary>
	/// A library item rooted to a local file system path
	/// </summary>
	public class FileSystemItem : ILibraryItem
	{
		public FileSystemItem(string path)
		{
			this.Path = path;
			var type = GetType();

			try
			{
				if (type == typeof(FileSystemFileItem))
				{
					var fileInfo = new FileInfo(path);

					this.DateCreated = fileInfo.CreationTime;
					this.DateModified = fileInfo.LastWriteTime;
				}
				else
				{
					var directoryInfo = new DirectoryInfo(path);

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

		public virtual string ID => agg_basics.GetLongHashCode(this.Path).ToString();

		public virtual bool IsProtected => false;

		public virtual bool IsVisible => true;

		public virtual bool LocalContentExists => true;

		public event EventHandler NameChanged;

		public virtual string Name
		{
			get
			{
				return System.IO.Path.GetFileName(this.Path);
			}

			set
			{
				if (Name != value)
				{
					string sourceFile = this.Path;
					if (File.Exists(sourceFile))
					{
						string extension = System.IO.Path.GetExtension(sourceFile);
						string destFile = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(sourceFile), value);
						destFile = System.IO.Path.ChangeExtension(destFile, extension);

						if (sourceFile != destFile)
						{
							File.Move(sourceFile, destFile);

							this.Path = destFile;

							ApplicationController.Instance.MainView.Broadcast("ILibraryItem Name Changed", new LibraryItemNameChangedEvent(this.ID));
						}
					}

					NameChanged?.Invoke(this, EventArgs.Empty);
				}

				/*
				if (item is DirectoryContainerLink directoryLink)
				{
					if (Directory.Exists(directoryLink.Path))
					{
						Process.Start(this.FullPath);
					}
				}
				else if (item is FileSystemFileItem fileItem)
				{
					string sourceFile = fileItem.Path;
					if (File.Exists(sourceFile))
					{
						string extension = Path.GetExtension(sourceFile);
						string destFile = Path.Combine(Path.GetDirectoryName(sourceFile), revisedName);
						destFile = Path.ChangeExtension(destFile, extension);

						File.Move(sourceFile, destFile);

						fileItem.Path = destFile;

						this.ReloadContent();
					}
				}
				else if (item is LocalZipContainerLink zipFile)
				{
					string sourceFile = zipFile.Path;
					if (File.Exists(sourceFile))
					{
						string extension = Path.GetExtension(sourceFile);
						string destFile = Path.Combine(Path.GetDirectoryName(sourceFile), revisedName);
						destFile = Path.ChangeExtension(destFile, extension);

						File.Move(sourceFile, destFile);

						this.ReloadContent();
					}
				}
				*/
			}
		}

		public string Path { get; set; }
	}
}