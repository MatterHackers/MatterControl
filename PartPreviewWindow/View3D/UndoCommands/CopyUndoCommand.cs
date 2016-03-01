using MatterHackers.Agg.UI;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	internal class CopyUndoCommand : IUndoRedoCommand
	{
		private int newItemIndex;
		private View3DWidget view3DWidget;
		private Matrix4X4 newItemTransform;
		PlatingMeshGroupData newItemPlatingData;

		MeshGroup meshGroupThatWasDeleted;

		public CopyUndoCommand(View3DWidget view3DWidget, int newItemIndex)
		{
			this.view3DWidget = view3DWidget;
			this.newItemIndex = newItemIndex;
			meshGroupThatWasDeleted = view3DWidget.MeshGroups[newItemIndex];
			newItemTransform = view3DWidget.MeshGroupTransforms[newItemIndex];
			newItemPlatingData = view3DWidget.MeshGroupExtraData[newItemIndex];
		}

		public void Undo()
		{
			view3DWidget.MeshGroups.RemoveAt(newItemIndex);
			view3DWidget.MeshGroupExtraData.RemoveAt(newItemIndex);
			view3DWidget.MeshGroupTransforms.RemoveAt(newItemIndex);
			if(view3DWidget.SelectedMeshGroupIndex >= view3DWidget.MeshGroups.Count)
			{
				view3DWidget.SelectedMeshGroupIndex = view3DWidget.MeshGroups.Count - 1;
			}
			view3DWidget.PartHasBeenChanged();
		}

		public void Do()
		{
			view3DWidget.MeshGroups.Insert(newItemIndex, meshGroupThatWasDeleted);
			view3DWidget.MeshGroupTransforms.Insert(newItemIndex, newItemTransform);
			view3DWidget.MeshGroupExtraData.Insert(newItemIndex, newItemPlatingData);
			view3DWidget.Invalidate();
			view3DWidget.SelectedMeshGroupIndex = view3DWidget.MeshGroups.Count - 1;
		}
	}
}