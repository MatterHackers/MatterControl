/*
Copyright (c) 2016, Lars Brubaker, John Lewin
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System.Collections.Generic;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	internal class TransformData
	{
		public IObject3D TransformedObject { get; set; }
		public Matrix4X4 RedoTransform { get; set; }
		public Matrix4X4 UndoTransform { get; set; }
	}

	internal class TransformUndoCommand : IUndoRedoCommand
	{
		private List<TransformData> transformDatas = new List<TransformData>();

		public TransformUndoCommand(List<TransformData> transformDatas)
		{
			this.transformDatas = transformDatas;
		}

		public TransformUndoCommand(IObject3D transformedObject, Matrix4X4 undoTransform, Matrix4X4 redoTransform)
		{
			if (transformedObject.ItemType == Object3DTypes.SelectionGroup)
			{
				// move the group transform into the items
				foreach (var child in transformedObject.Children)
				{
					var itemUndo = new TransformData()
					{
						TransformedObject = child,
						UndoTransform = child.Matrix,
						RedoTransform = child.Matrix * transformedObject.Matrix
					};
					this.transformDatas.Add(itemUndo);

					child.Matrix = itemUndo.RedoTransform;
				}

				// clear the group transform
				transformedObject.Matrix = Matrix4X4.Identity;
			}
			else
			{
				this.transformDatas.Add(new TransformData()
				{
					TransformedObject = transformedObject,
					UndoTransform = undoTransform,
					RedoTransform = redoTransform
				});
			}
		}

		public void Do()
		{
			foreach(var transformData in transformDatas)
			{
				transformData.TransformedObject.Matrix = transformData.RedoTransform;
			}
		}

		public void Undo()
		{
			foreach (var transformData in transformDatas)
			{
				transformData.TransformedObject.Matrix = transformData.UndoTransform;
			}
		}
	}
}