using MatterHackers.Agg.UI;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using System.Linq;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class GroupCommand : IUndoRedoCommand
	{
		private IObject3D item;
		private View3DWidget view3DWidget;

		public GroupCommand(View3DWidget view3DWidget, IObject3D groupingItem)
		{
			this.view3DWidget = view3DWidget;
			this.item = groupingItem;
		}

		public void Do()
		{
			if (view3DWidget.Scene.Children.Contains(item))
			{
				// If the item exits, it's likely still a selection group and we simply need to toggle that off
				item.ItemType = Object3DTypes.Group;
			}
			else
			{
				// Otherwise it's been removed and we need to re-add it
				view3DWidget.Scene.ModifyChildren(children =>
				{
					// Remove all children from the scene
					foreach(var child in item.Children)
					{
						children.Remove(child);
					}

					// Add the item
					children.Add(item);
				});
			}

			view3DWidget.Scene.Select(item);

			view3DWidget.PartHasBeenChanged();
		}

		public void Undo()
		{
			if(!view3DWidget.Scene.Children.Contains(item))
			{
				return;
			}

			view3DWidget.Scene.ModifyChildren(children =>
			{
				// Remove the group
				children.Remove(item);

				// Add all children from the group
				children.AddRange(item.Children);
			});

			view3DWidget.Scene.SelectLastChild();

			view3DWidget.PartHasBeenChanged();
		}
	}
}