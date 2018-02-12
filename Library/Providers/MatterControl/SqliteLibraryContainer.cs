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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;

namespace MatterHackers.MatterControl.Library
{
	public class SqliteFileItem : FileSystemFileItem
	{
		public PrintItem PrintItem { get; }
		public SqliteFileItem(PrintItem printItem)
			: base(printItem.FileLocation)
		{
			this.PrintItem = printItem;
		}

		public override string Name { get => this.PrintItem.Name; set => this.PrintItem.Name = value; }
	}

	public class SqliteLibraryContainer : WritableContainer
	{
		// Use default rootCollectionID - normally this constructor isn't used but exists to validate behavior in tests
		public SqliteLibraryContainer()
			: this(Datastore.Instance.dbSQLite.Table<PrintItemCollection>().Where(v => v.Name == "_library").Take(1).FirstOrDefault()?.Id ?? 0)
		{ }

		public SqliteLibraryContainer(int collectionID)
		{
			this.IsProtected = false;
			this.ChildContainers = new List<ILibraryContainerLink>();
			this.Items = new List<ILibraryItem>();
			this.Name = "Local Library".Localize();
			this.CollectionID = collectionID;
		}

		public int CollectionID { get; private set; }

		public override string KeywordFilter
		{
			get
			{
				return base.KeywordFilter;
			}
			set
			{
				if (base.KeywordFilter != value)
				{
					base.KeywordFilter = value;
					this.ReloadContent();
				}
			}
		}

		public override void Load()
		{
			var childCollections = this.GetChildCollections();

			var allFiles = this.GetLibraryItems(KeywordFilter);

			var zipFiles = allFiles.Where(f => string.Equals(Path.GetExtension(f.FileLocation), ".zip", StringComparison.OrdinalIgnoreCase));

			var nonZipFiles = allFiles.Except(zipFiles);

			IEnumerable<ILibraryContainerLink> childContainers = childCollections.Select(c => new SqliteLibraryContainerLink()
			{
				ContainerID = c.Id,
				Name = c.Name
			});

			this.ChildContainers = childContainers.Concat(
				zipFiles.Select(f => new LocalZipContainerLink(f.FileLocation, f.Name))).OrderBy(d => d.Name).ToList();

			// PrintItems projected onto FileSystemFileItem
			this.Items = nonZipFiles.Select<PrintItem, ILibraryItem>(printItem =>
			{
				if (File.Exists(printItem.FileLocation))
				{
					return new SqliteFileItem(printItem);
				}
				else
				{
					return new MessageItem($"{printItem.Name} (Missing)");
					//return new MissingFileItem() // Needs to return a content specific icon with a missing overlay - needs to lack all print operations
				}
			}).ToList();
		}

		public override async void Add(IEnumerable<ILibraryItem> items)
		{
			await Task.Run(async () =>
			{
				foreach (var item in items)
				{
					switch (item)
					{
						case CreateFolderItem newFolder:
							var newFolderCollection = new PrintItemCollection(newFolder.Name, "");
							newFolderCollection.ParentCollectionID = this.CollectionID;
							newFolderCollection.Commit();

							break;

						case ILibraryContainerLink containerInfo:
							var newCollection = new PrintItemCollection(containerInfo.Name, "");
							newCollection.ParentCollectionID = this.CollectionID;
							newCollection.Commit();

							break;

						case ILibraryAssetStream streamItem:

							var fileName = (streamItem as ILibraryAssetStream)?.FileName; 

							using (var streamInfo = await streamItem.GetContentStream(null))
							{
								// If the passed in item name equals the fileName, perform friendly name conversion, otherwise use supplied value
								string name = streamItem.Name;
								if (name == fileName)
								{
									name = PrintItemWrapperExtensionMethods.GetFriendlyName(Path.GetFileNameWithoutExtension(fileName));
								}

								AddItem(streamInfo.Stream, streamItem.ContentType, name);
							}

							break;
					}
				}

				this.ReloadContent();
			});
		}

		public List<PrintItem> GetLibraryItems(string keyphrase = null)
		{
			// TODO: String concatenation to build sql statements is the root of all sql injection attacks. This needs to be changed to use parameter objects as would be expected
			string query;
			if (string.IsNullOrEmpty(keyphrase))
			{
				query = $"SELECT * FROM PrintItem WHERE PrintItemCollectionID = {CollectionID} ORDER BY Name ASC;";
			}
			else
			{
				query = $"SELECT * FROM PrintItem WHERE PrintItemCollectionID = {CollectionID} AND Name LIKE '%{keyphrase}%' ORDER BY Name ASC;";
			}

			return Datastore.Instance.dbSQLite.Query<PrintItem>(query).ToList();
		}

		public override void Remove(IEnumerable<ILibraryItem> items)
		{
			// TODO: Handle Containers
			foreach(var item in items)
			{
				if (item is SqliteFileItem sqlItem)
				{
					sqlItem.PrintItem.Delete();
				}

				this.Items.Remove(item);
			}

			this.ReloadContent();
		}

		public override void Rename(ILibraryItem selectedItem, string revisedName)
		{
			if (selectedItem is SqliteFileItem sqliteItem)
			{
				sqliteItem.PrintItem.Name = revisedName;
				sqliteItem.PrintItem.Commit();
			}
			else if (selectedItem is SqliteLibraryContainerLink containerLink)
			{
				string sql = $"SELECT * FROM PrintItemCollection WHERE ID = {containerLink.ContainerID}";

				var container = Datastore.Instance.dbSQLite.Query<PrintItemCollection>(sql).FirstOrDefault();
				if (container != null)
				{
					container.Name = revisedName;
					container.Commit();
				}
			}

			this.ReloadContent();
		}

		/// <summary>
		/// Creates a database PrintItem entity, copies the source file to a new library 
		/// path and updates the PrintItem to point at the new target
		/// </summary>
		private void AddItem(Stream stream, string extension, string displayName)
		{
			// Create a new entity for the database
			var printItem = new PrintItem()
			{
				Name = displayName,
				PrintItemCollectionID = this.CollectionID,
				FileLocation = ApplicationDataStorage.Instance.GetNewLibraryFilePath(extension)
			};

			using (var outStream = File.Create(printItem.FileLocation))
			{
				stream.CopyTo(outStream);
			}

			printItem.Commit();
		}

		protected List<PrintItemCollection> GetChildCollections()
		{
			return Datastore.Instance.dbSQLite.Query<PrintItemCollection>(
				$"SELECT * FROM PrintItemCollection WHERE ParentCollectionID = {CollectionID} ORDER BY Name ASC;").ToList();
		}

		public override void Dispose()
		{
		}

		public class SqliteLibraryContainerLink : ILibraryContainerLink
		{
			public string ID { get; } = Guid.NewGuid().ToString();

			public int ContainerID { get; set; }

			public string Name { get; set; }

			public bool IsProtected { get; set; } = false;

			public bool IsReadOnly { get; set; } = false;

			public bool IsVisible { get; set; } = true;

			public Task<ILibraryContainer> GetContainer(Action<double, string> reportProgress)
			{
				return Task.FromResult<ILibraryContainer>(
					new SqliteLibraryContainer(this.ContainerID)
					{
						Name = this.Name,
					});
			}
		}
	}
}
