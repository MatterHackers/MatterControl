﻿/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.DataConverters3D.UndoCommands;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	public class OperationSourceContainerObject3D : Object3D
	{
		[JsonIgnore]
		public IObject3D SourceContainer
		{
			get
			{
				IObject3D sourceContainer = this.Children.FirstOrDefault(c => c is OperationSourceObject3D);
				if (sourceContainer == null)
				{
					using (this.RebuildLock())
					{
						sourceContainer = new OperationSourceObject3D();

						// Move all the children to sourceContainer
						this.Children.Modify(thisChildren =>
						{
							sourceContainer.Children.Modify(sourceChildren =>
							{
								foreach (var child in thisChildren)
								{
									sourceChildren.Add(child);
								}
							});

							// and then add the source container to this
							thisChildren.Clear();
							thisChildren.Add(sourceContainer);
						});
					}
				}

				return sourceContainer;
			}
		}

		public override void Flatten(UndoBuffer undoBuffer)
		{
			using (this.RebuildLock())
			{
				// The idea is we leave everything but the source and that is the applied operation
				this.Children.Modify(list =>
				{
					var sourceItem = list.FirstOrDefault(c => c is OperationSourceObject3D);
					if (sourceItem != null)
					{
						list.Remove(sourceItem);
					}

					if (list.Count > 1)
					{
						// wrap the children in an object so they remain a group
						var group = new Object3D();
						group.Children.Modify((groupList) =>
						{
							groupList.AddRange(list);
						});

						list.Clear();
						list.Add(group);
					}
				});
			}

			base.Flatten(undoBuffer);
		}

		public override void Remove(UndoBuffer undoBuffer)
		{
			using (this.RebuildLock())
			{
				this.Children.Modify(list =>
				{
					var sourceItem = list.FirstOrDefault(c => c is OperationSourceObject3D);
					if (sourceItem != null)
					{
						IObject3D firstChild = sourceItem.Children.First();
						if (firstChild != null)
						{
							list.Clear();
							list.Add(firstChild);
						}
					}
				});
			}

			base.Remove(undoBuffer);
		}

		public void RemoveAllButSource()
		{
			var sourceContainer = SourceContainer;
			this.Children.Modify(list =>
			{
				list.Clear();
				list.Add(sourceContainer);
			});
		}

		public void WrapSelectedItemAndSelect(InteractiveScene scene)
		{
			using (RebuildLock())
			{
				var selectedItems = scene.GetSelectedItems();

				if (selectedItems.Count > 0)
				{
					// clear the selected item
					scene.SelectedItem = null;

					using (RebuildLock())
					{
						var clonedItemsToAdd = new List<IObject3D>(selectedItems.Select((i) => i.Clone()));

						Children.Modify((list) =>
						{
							list.Clear();

							foreach (var child in clonedItemsToAdd)
							{
								list.Add(child);
							}
						});
					}

					scene.UndoBuffer.AddAndDo(
						new ReplaceCommand(
							new List<IObject3D>(selectedItems),
							new List<IObject3D> { this }));

					// and select this
					scene.SelectedItem = this;
				}
			}

			Invalidate(new InvalidateArgs(this, InvalidateType.Properties, null));
		}
	}

	public class OperationSourceObject3D : Object3D
	{
		public OperationSourceObject3D()
		{
			Name = "Source".Localize();
		}
	}
}