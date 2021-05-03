/*
Copyright (c) 2014, Lars Brubaker
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

using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.Plugins.EditorTools
{
	public class ScaleController
	{
		public ScaleStates FinalState;

		public ScaleStates InitialState = new ScaleStates();

		private IObject3DControlContext context;

		private List<Func<double>> getDiameters;

		private List<Action<double>> setDiameters;

		public ScaleController(List<Func<double>> getDiameters = null, List<Action<double>> setDiameters = null)
		{
			this.getDiameters = getDiameters;
			this.setDiameters = setDiameters;

			if (getDiameters != null)
			{
				for (int i = 0; i < getDiameters.Count; i++)
				{
					InitialState.Diameters.Add(0);
				}
			}
		}

		public bool HasChange
		{
			get
			{
				if (selectedItem is IObjectWithWidthAndDepth widthDepthItem
				   && (widthDepthItem.Width != InitialState.Width || widthDepthItem.Depth != InitialState.Depth))
				{
					return true;
				}

				return false;
			}
		}

		private IObject3D selectedItem
		{
			get
			{
				var selectedItemRoot = context.Scene.SelectedItemRoot;
				var selectedItem = context.Scene.SelectedItem;
				return (selectedItemRoot == selectedItem) ? selectedItem : null;
			}
		}

		public void Cancel()
		{
			if (selectedItem is IObjectWithWidthAndDepth widthDepthItem)
			{
				widthDepthItem.Width = InitialState.Width;
				widthDepthItem.Depth = InitialState.Depth;
			}

			if (selectedItem is IObjectWithHeight heightItem)
			{
				heightItem.Height= InitialState.Height;
			}

			if (setDiameters != null)
			{
				for (int i = 0; i < setDiameters.Count; i++)
				{
					setDiameters[i](InitialState.Diameters[i]);
				}
			}

			selectedItem.Rebuild();

			selectedItem.Matrix = InitialState.Matrix;
		
			selectedItem?.Invalidate(new InvalidateArgs(selectedItem, InvalidateType.DisplayValues));
		}

		/// <summary>
		/// Save undo & do data from the state information
		/// </summary>
		public void EditComplete()
		{
			var doState = FinalState;
			doState.Matrix = selectedItem.Matrix;

			var undoState = InitialState;

			EditComplete(undoState, doState);
		}

		public void ScaleDepth(double newDepth)
		{
			FinalState = new ScaleStates(InitialState);
			FinalState.Depth = newDepth;
			if (context.GuiSurface.ModifierKeys == Keys.Shift)
			{
				ScaleProportional(newDepth / InitialState.Depth);
			}

			SetItem(selectedItem, FinalState);
		}

		public void ScaleDiameter(double newSize, int index)
		{
			FinalState = new ScaleStates(InitialState);
			FinalState.Diameters[index] = newSize;
			if (context.GuiSurface.ModifierKeys == Keys.Shift)
			{
				ScaleProportional(newSize / InitialState.Diameters[index]);
			}

			SetItem(selectedItem, FinalState);
		}

		public void ScaleHeight(double newHeight)
		{
			FinalState = new ScaleStates(InitialState);
			FinalState.Height = newHeight;
			if (context.GuiSurface.ModifierKeys == Keys.Shift)
			{
				ScaleProportional(newHeight / InitialState.Height);
			}

			SetItem(selectedItem, FinalState);
		}

		public void ScaleWidth(double newWidth)
		{
			FinalState = new ScaleStates(InitialState);
			FinalState.Width = newWidth;
			if (context.GuiSurface.ModifierKeys == Keys.Shift)
			{
				ScaleProportional(newWidth / InitialState.Width);
			}

			SetItem(selectedItem, FinalState);
		}

		public void SetInitialState(IObject3DControlContext context)
		{
			this.context = context;

			if (selectedItem is IObjectWithWidthAndDepth widthDepthItem)
			{
				InitialState.Width = widthDepthItem.Width;
				InitialState.Depth = widthDepthItem.Depth;
			}

			if (selectedItem is IObjectWithHeight heightItem)
			{
				InitialState.Height = heightItem.Height;
			}

			if (getDiameters != null)
			{
				for (int i = 0; i < getDiameters.Count; i++)
				{
					InitialState.Diameters[i] = getDiameters[i]();
				}
			}

			InitialState.Matrix = selectedItem.Matrix;
		}

		internal void ScaleWidthDepth(double newWidth, double newDepth)
		{
			FinalState = new ScaleStates(InitialState);
			FinalState.Width = newWidth;
			FinalState.Depth = newDepth;
			if (context.GuiSurface.ModifierKeys == Keys.Shift)
			{
				ScaleProportional(newWidth / InitialState.Width);
			}

			SetItem(selectedItem, FinalState);
		}

		private void EditComplete(ScaleStates undoState, ScaleStates doState)
		{
			var undoBuffer = context.Scene.UndoBuffer;
			var selectedItem = this.selectedItem;

			undoBuffer.Add(new UndoRedoActions(async () =>
			{
				SetItem(selectedItem, undoState);
				await selectedItem.Rebuild();
				// we set the matrix again after as the rebuild might move the object
				selectedItem.Matrix = undoState.Matrix;
				selectedItem?.Invalidate(new InvalidateArgs(selectedItem, InvalidateType.DisplayValues));
			},
			async () =>
			{
				SetItem(selectedItem, doState);
				await selectedItem.Rebuild();
				// we set the matrix again after as the rebuild might move the object
				selectedItem.Matrix = doState.Matrix;
				selectedItem?.Invalidate(new InvalidateArgs(selectedItem, InvalidateType.DisplayValues));
			}));
		}

		private void ScaleProportional(double scale)
		{
			FinalState.Width = InitialState.Width * scale;
			FinalState.Depth = InitialState.Depth * scale;
			FinalState.Height = InitialState.Height * scale;
			for (int i = 0; i < FinalState.Diameters.Count; i++)
			{
				FinalState.Diameters[i] = InitialState.Diameters[i] * scale;
			}
		}

		private void SetItem(IObject3D item, ScaleStates states)
		{
			if (item is IObjectWithWidthAndDepth widthDepthItem)
			{
				widthDepthItem.Width = states.Width;
				widthDepthItem.Depth = states.Depth;
			}

			if (item is IObjectWithHeight heightItem)
			{
				heightItem.Height = states.Height;
			}

			if (setDiameters != null)
			{
				for (int i = 0; i < setDiameters.Count; i++)
				{
					setDiameters[i](states.Diameters[i]);
				}
			}

			item.Matrix = states.Matrix;

			item.Invalidate(new InvalidateArgs(item, InvalidateType.DisplayValues));
		}

		public class ScaleStates
		{
			public double Depth;

			public double Height;

			public double Width;

			public List<double> Diameters { get; internal set; } = new List<double>();

			public Matrix4X4 Matrix { get; set; }

			public ScaleStates()
			{

			}

			public ScaleStates(ScaleStates initialState)
			{
				this.Depth = initialState.Depth;
				this.Height = initialState.Height;
				this.Width = initialState.Width;
				this.Matrix = initialState.Matrix;

				for (int i = 0; i < initialState.Diameters.Count; i++)
				{
					this.Diameters.Add(initialState.Diameters[i]);
				}
			}
		}
	}
}