using MatterHackers.Agg.UI;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using System.Linq;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class UngroupCommand : IUndoRedoCommand
	{
		private IObject3D item;
		private View3DWidget view3DWidget;

		GroupCommand groupCommand;

		public UngroupCommand(View3DWidget view3DWidget, IObject3D ungroupingItem)
		{
			this.groupCommand = new GroupCommand(view3DWidget, ungroupingItem);
		}

		public void Do()
		{
			groupCommand.Undo();
		}

		public void Undo()
		{
			groupCommand.Do();
		}
	}
}