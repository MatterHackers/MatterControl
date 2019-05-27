/*
Copyright (c) 2018, John Lewin
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

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.DesignTools;

namespace MatterHackers.MatterControl.Library
{
	public class LibraryCollectionContainer : LibraryContainer
	{
		public LibraryCollectionContainer()
		{
			this.ChildContainers = new List<ILibraryContainerLink>();
			this.Items = new List<ILibraryItem>();
			this.Name = "Library".Localize();

			var rootLibraryCollection = Datastore.Instance.dbSQLite.Table<PrintItemCollection>().Where(v => v.Name == "_library").Take(1).FirstOrDefault();
			if (rootLibraryCollection != null)
			{
				this.ChildContainers.Add(
					new DynamicContainerLink(
						() => "Local Library".Localize(),
						AggContext.StaticData.LoadIcon(Path.Combine("Library", "library_20x20.png")),
						AggContext.StaticData.LoadIcon(Path.Combine("Library", "library_folder.png")),
						() => new SqliteLibraryContainer(rootLibraryCollection.Id)));
			}

			this.ChildContainers.Add(
				new DynamicContainerLink(
					() => "Calibration Parts".Localize(),
					AggContext.StaticData.LoadIcon(Path.Combine("Library", "folder_20x20.png")),
					AggContext.StaticData.LoadIcon(Path.Combine("Library", "folder.png")),
					() => new CalibrationPartsContainer())
				{
					IsReadOnly = true
				});

			this.ChildContainers.Add(
				new DynamicContainerLink(
					() => "Primitives".Localize(),
					AggContext.StaticData.LoadIcon(Path.Combine("Library", "folder_20x20.png")),
					AggContext.StaticData.LoadIcon(Path.Combine("Library", "folder.png")),
					() => new PrimitivesContainer())
				{
					IsReadOnly = true
				});

			this.ChildContainers.Add(
				new DynamicContainerLink(
					() => "Print Queue".Localize(),
					AggContext.StaticData.LoadIcon(Path.Combine("Library", "queue_20x20.png")),
					AggContext.StaticData.LoadIcon(Path.Combine("Library", "queue_folder.png")),
					() => new PrintQueueContainer()));

#if DEBUG
			this.ChildContainers.Add(
				new DynamicContainerLink(
					() => "Pipe Works".Localize(),
					AggContext.StaticData.LoadIcon(Path.Combine("Library", "folder_20x20.png")),
					AggContext.StaticData.LoadIcon(Path.Combine("Library", "folder.png")),
					() => new PipeWorksContainer()));

			int index = 0;

			this.ChildContainers.Add(
				new DynamicContainerLink(
					() => "Experimental".Localize(),
					AggContext.StaticData.LoadIcon(Path.Combine("Library", "folder_20x20.png")),
					AggContext.StaticData.LoadIcon(Path.Combine("Library", "folder.png")),
					() => new DynamicContainer()
					{
						Items = new List<ILibraryItem>()
						{
							new GeneratorItem(
								() => "Calibration Tab".Localize(),
								async () => await XyCalibrationTabObject3D.Create())
							{ DateCreated = new System.DateTime(index++) },
							new GeneratorItem(
								() => "Calibration Face".Localize(),
								async () => await XyCalibrationFaceObject3D.Create())
							{ DateCreated = new System.DateTime(index++) },
							new GeneratorItem(
								() => "Text2".Localize(),
								async () => await TextPathObject3D.Create())
							{ DateCreated = new System.DateTime(index++) },
							new GeneratorItem(
								() => "Path".Localize(),
								() =>
								{
									var storage = new VertexStorage();
									storage.MoveTo(5, 5);
									storage.LineTo(10, 5);
									storage.LineTo(7.5, 10);
									storage.ClosePolygon();

									var path = new PathObject3D()
									{
										VertexSource = storage
									};

									return Task.FromResult<IObject3D>(path);
								})
								{ DateCreated = new System.DateTime(index++) },
						}
					}));
#endif
		}

		public override void Load()
		{
		}
	}
}
