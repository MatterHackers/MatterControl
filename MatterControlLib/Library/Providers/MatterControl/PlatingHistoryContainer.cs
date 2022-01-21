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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;

namespace MatterHackers.MatterControl.Library
{
	public class PlatingHistoryContainer : HistoryContainerBase
	{
		public PlatingHistoryContainer():
			base (ApplicationDataStorage.Instance.PlatingDirectory)
		{
			this.Name = "Plating History".Localize();
		}
	}

	public abstract class HistoryContainerBase : FileSystemContainer, ILibraryWritableContainer
	{
		public HistoryContainerBase(string fullPath)
			: base(fullPath)
		{
			this.ChildContainers = new SafeList<ILibraryContainerLink>();
			this.Items = new SafeList<ILibraryItem>();
			this.IsProtected = false;

			DefaultSort = new LibrarySortBehavior()
			{
				SortKey = SortKey.ModifiedDate,
			};
		}

		public override bool AllowAction(ContainerActions containerActions)
		{
			switch(containerActions)
			{
				case ContainerActions.AddItems:
				case ContainerActions.AddContainers:
				case ContainerActions.RemoveItems:
				case ContainerActions.RenameItems:
					return false;

				default:
					System.Diagnostics.Debugger.Break();
					return false;
			}
		}

		internal ILibraryItem NewPlatingItem(InteractiveScene scene)
		{
			string now = "Workspace " + DateTime.Now.ToString("yyyy-MM-dd HH_mm_ss");
			string mcxPath = Path.Combine(this.FullPath, now + ".mcx");

			File.WriteAllText(mcxPath, new Object3D().ToJson());

			return new FileSystemFileItem(mcxPath);
		}

		public override void SetThumbnail(ILibraryItem item, int width, int height, ImageBuffer imageBuffer)
		{
		}
	}
}
