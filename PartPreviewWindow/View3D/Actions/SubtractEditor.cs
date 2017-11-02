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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.PolygonMesh;

namespace MatterHackers.MatterControl.PartPreviewWindow.View3D
{
	public class SubtractEditor : IObject3DEditor
	{
		private MeshWrapperOperation group;
		private View3DWidget view3DWidget;
		public string Name => "Subtract";

		public bool Unlocked { get; } = true;

		public GuiWidget Create(IObject3D group, View3DWidget view3DWidget, ThemeConfig theme)
		{
			this.view3DWidget = view3DWidget;
			this.group = group as MeshWrapperOperation;

			var mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);

			if (group is MeshWrapperOperation)
			{
				AddHoleSelector(view3DWidget, mainContainer, theme);
			}

			return mainContainer;
		}

		public IEnumerable<Type> SupportedTypes() => new Type[]
		{
			typeof(MeshWrapperOperation),
		};

		private void AddHoleSelector(View3DWidget view3DWidget, FlowLayoutWidget tabContainer, ThemeConfig theme)
		{
			var children = group.Children.ToList();

			tabContainer.AddChild(new TextWidget("Set as Hole")
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				HAnchor = HAnchor.Left,
				AutoExpandBoundsToText = true,
			});

			for (int i = 0; i < children.Count; i++)
			{
				var itemIndex = i;
				var item = children[itemIndex];
				FlowLayoutWidget rowContainer = new FlowLayoutWidget();

				var checkBox = new CheckBox(string.IsNullOrWhiteSpace(item.Name) ? $"{itemIndex}" : $"{item.Name}")
				{
					Checked = item.OutputType == PrintOutputTypes.Hole,
					TextColor = ActiveTheme.Instance.PrimaryTextColor
				};
				rowContainer.AddChild(checkBox);

				checkBox.CheckedStateChanged += (s, e) =>
				{
					// make sure the mesh on the group is not visible
					group.ResetMeshWrappers();

					foreach (var meshWrapper in item.Descendants().Where((obj) => obj.OwnerID == group.ID).ToList())
					{
						// and set the output type for this checkbox
						meshWrapper.OutputType = checkBox.Checked ? PrintOutputTypes.Hole : PrintOutputTypes.Solid;
					}
				};

				tabContainer.AddChild(rowContainer);
			}

			var updateButton = theme.ButtonFactory.Generate("Update".Localize());
			updateButton.Margin = new BorderDouble(5);
			updateButton.HAnchor = HAnchor.Right;
			updateButton.Click += (s, e) =>
			{
				// make sure the mesh on the group is not visible
				group.ResetMeshWrappers();
				ProcessBooleans(group);
			};
			tabContainer.AddChild(updateButton);
		}

		private async void ProcessBooleans(IObject3D group)
		{
			// spin up a task to remove holes from the objects in the group
			await Task.Run(() =>
			{
				var participants = group.Descendants().Where(o => o.OwnerID == group.ID).ToList();
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
}