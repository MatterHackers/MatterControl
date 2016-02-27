using MatterHackers.Agg.UI;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	internal class TransformUndoCommand : IUndoRedoCommand
	{
		private int meshGroupIndex;
		private Matrix4X4 redoTransform;
		private Matrix4X4 undoTransform;
		private View3DWidget view3DWidget;

		public TransformUndoCommand(View3DWidget view3DWidget, int meshGroupIndex, Matrix4X4 undoTransform, Matrix4X4 redoTransform)
		{
			this.view3DWidget = view3DWidget;
			this.meshGroupIndex = meshGroupIndex;
			this.undoTransform = undoTransform;
			this.redoTransform = redoTransform;
		}

		public void Do()
		{
			view3DWidget.MeshGroupTransforms[meshGroupIndex] = redoTransform;
		}

		public void Undo()
		{
			view3DWidget.MeshGroupTransforms[meshGroupIndex] = undoTransform;
		}
	}
}