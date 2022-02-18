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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.Library
{
	public class SqliteLibraryContainer : WritableContainer, ICustomSearch
	{
		private string keywordFilter = "";

		// Use default rootCollectionID - normally this constructor isn't used but exists to validate behavior in tests
		public SqliteLibraryContainer()
			: this(Datastore.Instance.dbSQLite.Table<PrintItemCollection>().Where(v => v.Name == "_library").Take(1).FirstOrDefault()?.Id ?? 0)
		{
		}

		public SqliteLibraryContainer(int collectionID)
		{
			this.CustomSearch = this;

			this.ID = "SqliteContainer" + collectionID;
			this.IsProtected = false;
			this.ChildContainers = new SafeList<ILibraryContainerLink>();
			this.Items = new SafeList<ILibraryItem>();
			this.Name = "Local Library".Localize();
			this.CollectionID = collectionID;
		}

		public int CollectionID { get; private set; }

		public override ICustomSearch CustomSearch { get; }

		public override async void Add(IEnumerable<ILibraryItem> items)
		{
			await Task.Run(async () =>
			{
				foreach (var item in items)
				{
					switch (item)
					{
						case CreateFolderItem newFolder:
							var newFolderCollection = new PrintItemCollection(newFolder.Name, "")
							{
								ParentCollectionID = this.CollectionID
							};
							newFolderCollection.Commit();

							break;

						case ILibraryContainerLink containerInfo:
							var newCollection = new PrintItemCollection(containerInfo.Name, "")
							{
								ParentCollectionID = this.CollectionID
							};
							newCollection.Commit();

							break;

						case ILibraryAssetStream streamItem:

							var fileName = (streamItem as ILibraryAssetStream)?.FileName;

							using (var streamInfo = await streamItem.GetStream(null))
							{
								// If the passed in item name equals the fileName, perform friendly name conversion, otherwise use supplied value
								string name = streamItem.Name;
								if (name == fileName)
								{
									name = Path.GetFileNameWithoutExtension(fileName);
								}

								AddItem(streamInfo.Stream, streamItem.ContentType, name);
							}

							break;
					}
				}

				UiThread.RunOnIdle(this.ReloadContent);
			});
		}

		public void ApplyFilter(string filter, ILibraryContext libraryContext)
		{
			keywordFilter = filter;
			this.Load();
			this.OnContentChanged();
		}

		public void ClearFilter()
		{
			keywordFilter = null;
			this.Load();
			this.OnContentChanged();
		}

		public override void Dispose()
		{
		}

		public override void Load()
		{
			var childCollections = this.GetChildCollections();

			var allFiles = this.GetLibraryItems(keywordFilter);

			var zipFiles = allFiles.Where(f => string.Equals(Path.GetExtension(f.FileLocation), ".zip", StringComparison.OrdinalIgnoreCase));

			var nonZipFiles = allFiles.Except(zipFiles);

			IEnumerable<ILibraryContainerLink> childContainers = childCollections.Select(c => new SqliteLibraryContainerLink(this)
			{
				CollectionID = c.Id,
				Name = c.Name
			});

			this.ChildContainers = new Agg.SafeList<ILibraryContainerLink>(childContainers.Concat(
				zipFiles.Select(f => new LocalLibraryZipContainerLink(f.Id, f.FileLocation, f.Name))).OrderBy(d => d.Name));

			// PrintItems projected onto FileSystemFileItem
			this.Items = new Agg.SafeList<ILibraryItem>(nonZipFiles.Select<PrintItem, ILibraryItem>(printItem =>
			{
				if (File.Exists(printItem.FileLocation))
				{
					return new SqliteFileItem(printItem, this);
				}
				else
				{
					return new MessageItem($"{printItem.Name} (Missing)");
					// return new MissingFileItem() // Needs to return a content specific icon with a missing overlay - needs to lack all print operations
				}
			}));
		}

		public override void Remove(IEnumerable<ILibraryItem> items)
		{
			// TODO: Handle Containers
			foreach (var item in items)
			{
				if (item is SqliteFileItem sqlItem)
				{
					sqlItem.PrintItem.Delete();
				}
				else if (item is LocalLibraryZipContainerLink link)
				{
					string sql = $"SELECT * FROM PrintItem WHERE ID = @id";
					var container = Datastore.Instance.dbSQLite.Query<PrintItem>(sql, link.RowID).FirstOrDefault();
					container?.Delete();
				}
			}

			this.ReloadContent();
		}

		public override void Save(ILibraryItem item, IObject3D content)
		{
			if (item is FileSystemFileItem fileItem)
			{
				// save using the normal uncompressed mcx file
				// Serialize the scene to disk using a modified Json.net pipeline with custom ContractResolvers and JsonConverters
				File.WriteAllText(fileItem.FilePath, content.ToJson().Result);

				this.OnItemContentChanged(new LibraryItemChangedEventArgs(fileItem));
			}
		}

		public override void SetThumbnail(ILibraryItem item, int width, int height, ImageBuffer imageBuffer)
		{
#if DEBUG
			// throw new NotImplementedException();
#endif
		}

		protected List<PrintItemCollection> GetChildCollections()
		{
			return Datastore.Instance.dbSQLite.Query<PrintItemCollection>(
				$"SELECT * FROM PrintItemCollection WHERE ParentCollectionID = {CollectionID} ORDER BY Name ASC;").ToList();
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

		private List<PrintItem> GetLibraryItems(string keyphrase = null)
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

		public class SqliteLibraryContainerLink : ILibraryContainerLink
		{
            private SqliteLibraryContainer sqliteLibraryContainer;

            public SqliteLibraryContainerLink(SqliteLibraryContainer sqliteLibraryContainer)
            {
				this.sqliteLibraryContainer = sqliteLibraryContainer;
			}

			public int CollectionID { get; set; }

			public DateTime DateCreated { get; } = DateTime.Now;

			public DateTime DateModified { get; } = DateTime.Now;

			public string ID => "SqliteContainer" + this.CollectionID;

			public bool IsProtected { get; set; } = false;

			public bool IsReadOnly { get; set; } = false;

			public bool IsVisible { get; set; } = true;

			public event EventHandler NameChanged;

			public string Name
			{
				get
				{
					string sql = $"SELECT * FROM PrintItemCollection WHERE ID = {this.CollectionID}";

					var container = Datastore.Instance.dbSQLite.Query<PrintItemCollection>(sql).FirstOrDefault();
					return container.Name;
				}

				set
				{
					if (value != Name)
					{
						string sql = $"SELECT * FROM PrintItemCollection WHERE ID = {this.CollectionID}";

						var container = Datastore.Instance.dbSQLite.Query<PrintItemCollection>(sql).FirstOrDefault();

						if (value != container.Name)
						{
							if (container != null)
							{
								container.Name = value;
								container.Commit();
							}

							NameChanged?.Invoke(this, EventArgs.Empty);
							sqliteLibraryContainer.ReloadContent();
						}
					}
				}
			}
			public Task<ILibraryContainer> GetContainer(Action<double, string> reportProgress)
			{
				return Task.FromResult<ILibraryContainer>(
					new SqliteLibraryContainer(this.CollectionID)
					{
						Name = this.Name,
					});
			}
		}
	}
}