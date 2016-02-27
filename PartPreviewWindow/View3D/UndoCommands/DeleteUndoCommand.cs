using MatterHackers.Agg.UI;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	internal class DeleteUndoCommand : IUndoRedoCommand
	{
		private int meshGroupIndex;
		private View3DWidget view3DWidget;
		private Matrix4X4 deletedTransform;
		PlatingMeshGroupData deletedPlatingData;

		MeshGroup meshGroupThatWasDeleted;

		public DeleteUndoCommand(View3DWidget view3DWidget, int meshGroupIndex)
		{
			this.view3DWidget = view3DWidget;
			this.meshGroupIndex = meshGroupIndex;
			meshGroupThatWasDeleted = view3DWidget.MeshGroups[meshGroupIndex];
			deletedTransform = view3DWidget.MeshGroupTransforms[meshGroupIndex];
			deletedPlatingData = view3DWidget.MeshGroupExtraData[meshGroupIndex];
		}

		public void Do()
		{
			view3DWidget.DeleteMeshGroup(meshGroupIndex);
		}

		public void Undo()
		{
			view3DWidget.MeshGroups.Insert(meshGroupIndex, meshGroupThatWasDeleted);
			view3DWidget.MeshGroupTransforms.Insert(meshGroupIndex, deletedTransform);
			view3DWidget.MeshGroupExtraData.Insert(meshGroupIndex, deletedPlatingData);
			view3DWidget.Invalidate();
		}
	}
}