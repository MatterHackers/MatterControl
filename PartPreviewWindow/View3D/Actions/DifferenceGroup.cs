/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.PolygonMesh;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.PartPreviewWindow.View3D
{
	public class DifferenceEditor : IObject3DEditor
	{
		private View3DWidget view3DWidget;
		private IObject3D item;

		public string Name => "Primitives";

		public bool Unlocked { get; } = true;

		public IEnumerable<Type> SupportedTypes() => new Type[]
		{
			typeof(DifferenceGroup),
		};

		public GuiWidget Create(IObject3D item, View3DWidget view3DWidget, ThemeConfig theme)
		{
			this.view3DWidget = view3DWidget;
			this.item = item;

			var mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);

			var tabContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				Visible = true,
				Width = theme.WhiteButtonFactory.Options.FixedWidth
			};
			mainContainer.AddChild(tabContainer);

			if (item is DifferenceGroup)
			{
				AddHoleSelector(view3DWidget, tabContainer, theme);
			}

			return mainContainer;
		}

		private void AddHoleSelector(View3DWidget view3DWidget, FlowLayoutWidget tabContainer, ThemeConfig theme)
		{
			var differenceGroup = this.item as DifferenceGroup;

			var differenceItems = differenceGroup.Descendants().Where((obj) => obj.OwnerID == differenceGroup.ID).ToList();

			tabContainer.AddChild(new TextWidget("Set as Hole")
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				HAnchor = HAnchor.Left,
				AutoExpandBoundsToText = true,
			});

			int itemNumber = 1;
			foreach(var differenceItem in differenceItems)
			{
				FlowLayoutWidget rowContainer = new FlowLayoutWidget();

				rowContainer.AddChild(new CheckBox($"{itemNumber++} {differenceItem.Name}"));

				tabContainer.AddChild(rowContainer);
			}

			var updateButton = theme.ButtonFactory.Generate("Update".Localize());
			updateButton.Margin = new BorderDouble(5);
			updateButton.HAnchor = HAnchor.Right;
			updateButton.Click += (s, e) =>
			{
			};
			tabContainer.AddChild(updateButton);
		}

		private static FlowLayoutWidget CreateSettingsRow(string labelText)
		{
			var rowContainer = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				HAnchor = HAnchor.Stretch,
				Padding = new BorderDouble(5)
			};

			var label = new TextWidget(labelText + ":", textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				Margin = new BorderDouble(0, 0, 3, 0),
				VAnchor = VAnchor.Center
			};
			rowContainer.AddChild(label);

			rowContainer.AddChild(new HorizontalSpacer());

			return rowContainer;
		}
	}

	public class DifferenceGroup : Object3D
	{
		public DifferenceGroup(SafeList<IObject3D> children)
		{
			Children.Modify((list) =>
			{
				foreach (var child in children)
				{
					list.Add(child);
				}
			});

			bool first = true;
			// now wrap every first decendant that has a mesh
			var visibleMeshes = this.VisibleMeshes().Where((o) => o.Mesh != null).ToList();
			foreach (var child in visibleMeshes)
			{
				// wrap the child in a DifferenceItem
				child.Parent.Children.Modify((list) =>
				{
					list.Remove(child);
					list.Add(new DifferenceItem(child, this.ID, !first));
					first = false;
				});
			}

			ProcessBooleans();
		}

		private async void ProcessBooleans()
		{
			// spin up a task to remove holes from the objects in the group
			await Task.Run(() =>
			{
				var container = this;
				var participants = this.VisibleMeshes().Where(o => o.OwnerID == this.ID).ToList();
				var removeObjects = participants.Where((obj) => obj.OutputType == PrintOutputTypes.Hole).ToList();
				var keepObjects = participants.Where((obj) => obj.OutputType != PrintOutputTypes.Hole).ToList();

				if (removeObjects.Any()
					&& keepObjects.Any())
				{
					foreach (var remove in removeObjects)
					{
						foreach (var keep in keepObjects)
						{
							var transformedRemove = Mesh.Copy(remove.Mesh, CancellationToken.None);
							transformedRemove.Transform(remove.WorldMatrix());

							var transformedKeep = Mesh.Copy(keep.Mesh, CancellationToken.None);
							transformedKeep.Transform(keep.WorldMatrix());

							transformedKeep = PolygonMesh.Csg.CsgOperations.Subtract(transformedKeep, transformedRemove);
							var inverse = keep.WorldMatrix();
							inverse.Invert();
							transformedKeep.Transform(inverse);
							keep.Mesh = transformedKeep;
						}

						remove.Visible = false;
					}
				}
			});
		}
	}

	public class DifferenceItem : MeshWrapper
	{
		public DifferenceItem()
		{
		}

		public DifferenceItem(IObject3D child, string ownerId, bool makeHole)
			: base(child, ownerId)
		{
			if (makeHole)
			{
				OutputType = PrintOutputTypes.Hole;
			}
		}
	}
}