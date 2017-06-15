using MatterHackers.Agg.UI;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using System.Linq;
using MatterHackers.DataConverters3D;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	internal class CopyUndoCommand : IUndoRedoCommand
	{
		private int newItemIndex;
		private View3DWidget view3DWidget;

		IObject3D addedObject3D;

		bool wasLastItem;

		public CopyUndoCommand(View3DWidget view3DWidget, int newItemIndex)
		{
			this.view3DWidget = view3DWidget;
			this.newItemIndex = newItemIndex;

			addedObject3D = view3DWidget.Scene.Children[newItemIndex];

			wasLastItem = view3DWidget.Scene.Children.Last() == addedObject3D;
		}

		public void Undo()
		{
			view3DWidget.Scene.Children.RemoveAt(newItemIndex);

			if (wasLastItem)
			{
				view3DWidget.Scene.SelectLastChild();
			}
			view3DWidget.PartHasBeenChanged();
		}

		public void Do()
		{
			view3DWidget.Scene.Children.Insert(newItemIndex, addedObject3D);
			view3DWidget.Invalidate();
			view3DWidget.Scene.SelectLastChild();
		}
	}
}