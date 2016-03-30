using MatterHackers.Agg.UI;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using System.Linq;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class InsertCommand : IUndoRedoCommand
	{
		private IObject3D item;
		private Matrix4X4 originalTransform;
		private View3DWidget view3DWidget;

		bool firstPass = true;

		public InsertCommand(View3DWidget view3DWidget, IObject3D insertingItem)
		{
			this.view3DWidget = view3DWidget;
			this.item = insertingItem;
			this.originalTransform = insertingItem.Matrix;
		}

		public void Do()
		{
			if (!firstPass)
			{
				item.Matrix = originalTransform;
			}

			view3DWidget.Scene.ModifyChildren(children =>
			{
				children.Add(item);
			});

			firstPass = false;

			view3DWidget.Scene.Select(item);

			view3DWidget.PartHasBeenChanged();
		}

		public void Undo()
		{
			view3DWidget.Scene.ModifyChildren(children =>
			{
				children.Remove(item);
			});

			view3DWidget.Scene.SelectLastChild();

			view3DWidget.PartHasBeenChanged();
		}
	}
	/*
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
	} */
}