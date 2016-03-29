using MatterHackers.Agg.UI;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	internal class TransformUndoCommand : IUndoRedoCommand
	{
		private IObject3D transformedObject;
		private Matrix4X4 redoTransform;
		private Matrix4X4 undoTransform;
		private View3DWidget view3DWidget;

		public TransformUndoCommand(View3DWidget view3DWidget, IObject3D transformedObject, Matrix4X4 undoTransform, Matrix4X4 redoTransform)
		{
			this.view3DWidget = view3DWidget;
			this.transformedObject = transformedObject;
			this.undoTransform = undoTransform;
			this.redoTransform = redoTransform;
		}

		public void Do()
		{
			transformedObject.Matrix = redoTransform;
		}

		public void Undo()
		{
			transformedObject.Matrix = undoTransform;
		}
	}
}