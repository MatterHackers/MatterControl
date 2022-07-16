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
using System.Threading;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.DataConverters3D.UndoCommands;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.PartPreviewWindow;

namespace MatterHackers.MatterControl.DesignTools
{
	[HideChildrenFromTreeView]
	public class ComponentObject3D : Object3D, IRightClickMenuProvider
	{
		private const string ImageConverterComponentID = "4D9BD8DB-C544-4294-9C08-4195A409217A";

		public ComponentObject3D()
		{
		}

		public ComponentObject3D(IEnumerable<IObject3D> children)
			: base(children)
		{
		}

		public override bool CanApply => !Finalized || Persistable;

		public override bool Persistable => ApplicationController.Instance.UserHasPermission(this);

		private bool _finalizade = true;
		
		[Description("Switch from editing to distribution")]
		public bool Finalized
		{
			get => _finalizade;
			
			set
			{
				_finalizade = value;
				// on any invalidate ensure that the visibility setting are correct for embedded sheet objects
				foreach (var child in this.Descendants())
				{
					if (child is SheetObject3D)
					{
						child.Visible = !this.Finalized;
					}
				}
			}
		}

		public List<string> SurfacedEditors { get; set; } = new List<string>();

		[HideFromEditor]
		public string ComponentID { get; set; } = "";

		[Description("MatterHackers Internal Use")]
		public bool ProOnly { get; set; }

		public override void Apply(UndoBuffer undoBuffer)
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
					var group = new GroupHolesAppliedObject3D
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

		public (string cellId, string cellData) DecodeContent(int editorIndex)
		{
			if (SurfacedEditors[editorIndex].StartsWith("!"))
			{
				var cellData2 = SurfacedEditors[editorIndex].Substring(1);
				var cellId2 = cellData2.ToLower();
				// check if it has embededdata
				var separator = cellData2.IndexOf(',');
				if (separator != -1)
				{
					cellId2 = cellData2.Substring(0, separator).ToLower();
					cellData2 = cellData2.Substring(separator + 1);
				}
				else
				{
					var firtSheet = this.Descendants<SheetObject3D>().FirstOrDefault();
					if (firtSheet != null)
					{
						// We don't have any cache of the cell content, get the current content
						double.TryParse(firtSheet.SheetData.EvaluateExpression(cellId2), out double value);
						cellData2 = value.ToString();
					}
				}

				return (cellId2, cellData2);
			}

			return (null, null);
		}


		private void RecalculateSheet()
		{
			// if there are editors that reference cells
			for (int i=0; i<SurfacedEditors.Count; i++)
            {
				var (cellId, cellData) = this.DecodeContent(i);
				if (cellData.StartsWith("="))
				{
					var expression = new DoubleOrExpression(cellData);
					var firtSheet = this.Descendants<SheetObject3D>().FirstOrDefault();
					if (firtSheet != null)
					{
						var cell = firtSheet.SheetData[cellId];
						if (cell != null)
						{
							cell.Expression = expression.Value(this).ToString();
						}
					}
				}
			}

			if (SurfacedEditors.Any(se => se.StartsWith("!"))
				&& !this.RebuildLocked)
			{
				var firtSheet = this.Descendants<SheetObject3D>().FirstOrDefault();

				var componentLock = this.RebuildLock();
				firtSheet.SheetData.Recalculate();

				UiThread.RunOnIdle(() =>
				{
					// wait until the sheet is done rebuilding (or 30 seconds)
					var startTime = UiThread.CurrentTimerMs;
					while (firtSheet.RebuildLocked
						&& startTime + 30000 < UiThread.CurrentTimerMs)
					{
						Thread.Sleep(1);
					}

					componentLock.Dispose();
				});
			}
		}

		public override void OnInvalidate(InvalidateArgs invalidateType)
        {
			switch(invalidateType.InvalidateType)
            {
				case InvalidateType.SheetUpdated:
				case InvalidateType.Properties:
					RecalculateSheet();
					break;
            }

            base.OnInvalidate(invalidateType);
        }

        public override void Cancel(UndoBuffer undoBuffer)
		{
			// Make any hiden children visible
			// on any invalidate ensure that the visibility setting are correct for embedded sheet objects
			foreach (var child in this.Descendants())
			{
				if (child is SheetObject3D)
				{
					child.Visible = true;
				}
			}

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
				if (ProOnly)
				{
					// just delete it
					var parent = this.Parent;

					using (RebuildLock())
					{
						if (undoBuffer != null)
						{
							// and replace us with nothing
							undoBuffer.AddAndDo(new ReplaceCommand(new[] { this }, new List<IObject3D>(), false));
						}
						else
						{
							parent.Children.Modify(list =>
							{
								list.Remove(this);
							});
						}
					}

					parent.Invalidate(new InvalidateArgs(this, InvalidateType.Children));
				}
				else
				{
					// remove the component and leave the inside parts
					base.Cancel(undoBuffer);
				}
			}
		}

        public void AddRightClickMenuItemsItems(PopupMenu popupMenu, ThemeConfig theme)
        {
            popupMenu.CreateSeparator();

            string componentID = this.ComponentID;

			var helpItem = popupMenu.CreateMenuItem("Help".Localize());
			var helpArticlesByID = ApplicationController.Instance.HelpArticlesByID;
			helpItem.Enabled = !string.IsNullOrEmpty(componentID) && helpArticlesByID.ContainsKey(componentID);
			helpItem.Click += (s, e) =>
			{
				var helpTab = ApplicationController.Instance.ActivateHelpTab("Docs");
				if (helpTab.TabContent is HelpTreePanel helpTreePanel)
				{
					if (helpArticlesByID.TryGetValue(componentID, out HelpArticle helpArticle))
					{
						helpTreePanel.ActiveNodePath = componentID;
					}
				}
			};
		}

        public void AddRightClickMenuItemsItems(PopupMenu popupMenu)
        {
            throw new System.NotImplementedException();
        }
    }
}