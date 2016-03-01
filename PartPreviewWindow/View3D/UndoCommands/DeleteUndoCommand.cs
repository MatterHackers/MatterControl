using MatterHackers.Agg.UI;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	internal class DeleteUndoCommand : IUndoRedoCommand
	{
		private int deletedIndex;
		private View3DWidget view3DWidget;
		private Matrix4X4 deletedTransform;
		PlatingMeshGroupData deletedPlatingData;

		MeshGroup meshGroupThatWasDeleted;

		public DeleteUndoCommand(View3DWidget view3DWidget, int deletedIndex)
		{
			this.view3DWidget = view3DWidget;
			this.deletedIndex = deletedIndex;
			meshGroupThatWasDeleted = view3DWidget.MeshGroups[deletedIndex];
			deletedTransform = view3DWidget.MeshGroupTransforms[deletedIndex];
			deletedPlatingData = view3DWidget.MeshGroupExtraData[deletedIndex];
		}

		public void Do()
		{
			view3DWidget.MeshGroups.RemoveAt(deletedIndex);
			view3DWidget.MeshGroupExtraData.RemoveAt(deletedIndex);
			view3DWidget.MeshGroupTransforms.RemoveAt(deletedIndex);
			if (view3DWidget.SelectedMeshGroupIndex >= view3DWidget.MeshGroups.Count)
			{
				view3DWidget.SelectedMeshGroupIndex = view3DWidget.MeshGroups.Count - 1;
			}
			view3DWidget.PartHasBeenChanged();
		}

		public void Undo()
		{
			view3DWidget.MeshGroups.Insert(deletedIndex, meshGroupThatWasDeleted);
			view3DWidget.MeshGroupTransforms.Insert(deletedIndex, deletedTransform);
			view3DWidget.MeshGroupExtraData.Insert(deletedIndex, deletedPlatingData);
			view3DWidget.Invalidate();
			view3DWidget.SelectedMeshGroupIndex = view3DWidget.MeshGroups.Count - 1;
		}
	}
}