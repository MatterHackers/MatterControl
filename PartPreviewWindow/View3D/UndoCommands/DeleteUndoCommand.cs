using MatterHackers.Agg.UI;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using System.Linq;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class DeleteCommand : IUndoRedoCommand
	{
		private IObject3D item;

		private View3DWidget view3DWidget;

		public DeleteCommand(View3DWidget view3DWidget, IObject3D deletingItem)
		{
			this.view3DWidget = view3DWidget;
			this.item = deletingItem;
		}

		public void Do()
		{
			view3DWidget.Scene.ModifyChildren(children =>
			{
				children.Remove(item);
			});

			view3DWidget.Scene.SelectLastChild();

			view3DWidget.PartHasBeenChanged();
		}

		public void Undo()
		{
			view3DWidget.Scene.ModifyChildren(children =>
			{
				children.Add(item);
			});

			view3DWidget.Scene.Select(item);

			view3DWidget.PartHasBeenChanged();
		}
	}
}