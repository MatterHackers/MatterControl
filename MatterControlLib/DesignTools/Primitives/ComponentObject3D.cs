/*
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
using System.ComponentModel;
using System.Linq;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.DataConverters3D.UndoCommands;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DesignTools.Operations;

namespace MatterHackers.MatterControl.DesignTools
{
	[HideChildrenFromTreeView]
	public class ComponentObject3D : Object3D
	{
		private const string ImageConverterComponentID = "4D9BD8DB-C544-4294-9C08-4195A409217A";

		public ComponentObject3D()
		{
		}

		public ComponentObject3D(IEnumerable<IObject3D> children)
			: base(children)
		{
		}

		public override bool CanFlatten => Finalized;

		public override bool Persistable => ApplicationController.Instance.UserHasPermission(this);

		public bool Finalized { get; set; } = true;

		public List<string> SurfacedEditors { get; set; } = new List<string>();

		[HideFromEditor]
		public string ComponentID { get; set; } = "";

		[Description("MatterHackers Internal Use")]
		public string DetailPage { get; set; } = "";

		[Description("MatterHackers Internal Use")]
		public string PermissionKey { get; set; } = "";

		public override void Flatten(UndoBuffer undoBuffer)
		{
			// we want to end up with just a group of all the visible mesh objects
			using (RebuildLock())
			{
				var newChildren = new List<IObject3D>();

				// push our matrix into a copy of our visible children
				foreach (var child in this.VisibleMeshes())
				{
					var meshOnlyItem = new Object3D
					{
						Matrix = child.WorldMatrix(this),
						Color = child.WorldColor(this),
						MaterialIndex = child.WorldMaterialIndex(this),
						OutputType = child.WorldOutputType(this),
						Mesh = child.Mesh,
						Name = "Mesh".Localize()
					};
					newChildren.Add(meshOnlyItem);
				}

				if (newChildren.Count > 1)
				{
					var group = new GroupObject3D
					{
						Name = this.Name
					};
					group.Children.Modify(list =>
					{
						list.AddRange(newChildren);
					});
					newChildren.Clear();
					newChildren.Add(group);
				}
				else if (newChildren.Count == 1)
				{
					newChildren[0].Name = this.Name;
				}

				// and replace us with the children
				undoBuffer.AddAndDo(new ReplaceCommand(new[] { this }, newChildren));
			}

			Invalidate(InvalidateType.Children);
		}

		public override void Remove(UndoBuffer undoBuffer)
		{
			// Custom remove for ImageConverter
			if (this.ComponentID == ImageConverterComponentID)
			{
				var parent = this.Parent;

				using (RebuildLock())
				{
					if (this.Descendants<ImageObject3D>().FirstOrDefault() is ImageObject3D imageObject3D)
					{
						imageObject3D.Matrix = this.Matrix;

						if (undoBuffer != null)
						{
							undoBuffer.AddAndDo(new ReplaceCommand(new[] { this }, new[] { imageObject3D }));
						}
						else
						{
							parent.Children.Modify(list =>
							{
								list.Remove(this);
								list.Add(imageObject3D);
							});
						}
					}
				}

				parent.Invalidate(new InvalidateArgs(this, InvalidateType.Children));
			}
			else
			{
				base.Remove(undoBuffer);
			}
		}
	}
}