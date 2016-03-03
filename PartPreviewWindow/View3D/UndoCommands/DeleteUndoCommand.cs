using MatterHackers.Agg.UI;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using System.Linq;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	internal class DeleteUndoCommand : IUndoRedoCommand
	{
		private int deletedIndex;
		private View3DWidget view3DWidget;
		private Matrix4X4 deletedTransform;
		PlatingMeshGroupData deletedPlatingData;

		IObject3D meshGroupThatWasDeleted;

		bool wasLastItem;

		public DeleteUndoCommand(View3DWidget view3DWidget, int deletedIndex)
		{
			this.view3DWidget = view3DWidget;
			this.deletedIndex = deletedIndex;
			meshGroupThatWasDeleted = view3DWidget.Scene.Children[deletedIndex];

			wasLastItem = view3DWidget.Scene.Children.Last() == meshGroupThatWasDeleted;

			deletedPlatingData = view3DWidget.Scene.Children[deletedIndex].ExtraData;
		}

		public void Do()
		{
			view3DWidget.Scene.Children.RemoveAt(deletedIndex);

			if (wasLastItem)
			{
				view3DWidget.Scene.SetSelectionToLastItem();
			}

			view3DWidget.PartHasBeenChanged();
		}

		public void Undo()
		{
			view3DWidget.Scene.Children.Insert(deletedIndex, meshGroupThatWasDeleted);
			view3DWidget.Invalidate();

			view3DWidget.Scene.SetSelectionToLastItem();
		}
	}
}