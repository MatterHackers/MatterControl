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
using MatterHackers.MatterControl.DesignTools.Operations;
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
		private Func<double> getWidth;
		private Action<double> setWidth;
		private Func<double> getDepth;
		private Action<double> setDepth;
		private Func<double> getHeight;
		private Action<double> setHeight;

		public ScaleController(IObject3DControlContext context,
			Func<double> getWidth,
			Action<double> setWidth,
			Func<double> getDepth,
			Action<double> setDepth,
			Func<double> getHeight,
			Action<double> setHeight,
			List<Func<double>> getDiameters = null,
			List<Action<double>> setDiameters = null)
		{
			this.context = context;

			this.getWidth = getWidth;
			this.setWidth = setWidth;

			this.getDepth = getDepth;
			this.setDepth = setDepth;

			this.getHeight = getHeight;
			this.setHeight = setHeight;

			this.getDiameters = getDiameters;
			this.setDiameters = setDiameters;

			if (getDiameters != null)
			{
				for (int i = 0; i < getDiameters.Count; i++)
				{
					InitialState.Diameters.Add(0);
				}
			}

			SetInitialState(context);
		}

		public bool HasChange
		{
			get
			{
				if ((getWidth != null && getWidth() != InitialState.Width)
				   || (getDepth != null && getDepth() != InitialState.Depth))
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
			setWidth?.Invoke(InitialState.Width);
			setDepth?.Invoke(InitialState.Depth);

			setHeight?.Invoke(InitialState.Height);

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
			EditComplete(InitialState, FinalState);
		}

		public void ScaleDepth(double newDepth)
		{
			FinalState.Depth = newDepth;
			if (context.GuiSurface.ModifierKeys == Keys.Shift || selectedItem is IScaleLocker)
			{
				ScaleProportional(newDepth / InitialState.Depth);
			}

			SetItem(selectedItem, FinalState);
		}

		public void ScaleDiameter(double newSize, int index)
		{
			FinalState.Diameters[index] = newSize;
			if (context.GuiSurface.ModifierKeys == Keys.Shift)
			{
				ScaleProportional(newSize / InitialState.Diameters[index]);
			}

			SetItem(selectedItem, FinalState);
		}

		public void ScaleHeight(double newHeight)
		{
			FinalState.Height = newHeight;
			if (context.GuiSurface.ModifierKeys == Keys.Shift || selectedItem is IScaleLocker)
			{
				ScaleProportional(newHeight / InitialState.Height);
			}

			SetItem(selectedItem, FinalState);
		}

		public void ScaleWidth(double newWidth)
		{
			FinalState.Width = newWidth;
			if (context.GuiSurface.ModifierKeys == Keys.Shift || selectedItem is IScaleLocker)
			{
				ScaleProportional(newWidth / InitialState.Width);
			}

			SetItem(selectedItem, FinalState);
		}

		private void SetInitialState(IObject3DControlContext context)
		{
			if (getWidth != null)
			{
				InitialState.Width = getWidth();
			}

			if (getDepth != null)
			{
				InitialState.Depth = getDepth.Invoke();
			}

			if (getHeight != null)
			{
				InitialState.Height = getHeight();
			}

			if (getDiameters != null)
			{
				for (int i = 0; i < getDiameters.Count; i++)
				{
					InitialState.Diameters[i] = getDiameters[i]();
				}
			}

			if (selectedItem != null)
			{
				InitialState.Matrix = selectedItem.Matrix;
			}

			FinalState = new ScaleStates(InitialState);
		}

		internal void ScaleWidthDepth(double newWidth, double newDepth)
		{
			FinalState.Width = newWidth;
			FinalState.Depth = newDepth;
			if (context.GuiSurface.ModifierKeys == Keys.Shift || selectedItem is IScaleLocker)
			{
				ScaleProportional(newWidth / InitialState.Width);
			}

			SetItem(selectedItem, FinalState);
		}

		private void EditComplete(ScaleStates undoState, ScaleStates doState)
		{
			var selectedItem = this.selectedItem;

			// make copies of the scale states as they will be save into the undo redo stack
			doState = new ScaleStates(doState);
			doState.Matrix = selectedItem.Matrix;
			undoState = new ScaleStates(undoState);

			var undoBuffer = context.Scene.UndoBuffer;

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
			if (context.GuiSurface.ModifierKeys != Keys.Shift
				&& selectedItem is IScaleLocker scaleLocker)
			{
				switch (scaleLocker.LockProportion)
				{
					case LockProportions.None:
						break;

					case LockProportions.X_Y:
						if (FinalState.Width != InitialState.Width
							|| FinalState.Depth != InitialState.Depth)
						{
							FinalState.Width = InitialState.Width * scale;
							FinalState.Depth = InitialState.Depth * scale;
						}
						break;

					case LockProportions.X_Y_Z:
						FinalState.Width = InitialState.Width * scale;
						FinalState.Depth = InitialState.Depth * scale;
						FinalState.Height = InitialState.Height * scale;
						break;
				}

				scaleLocker.ScaledProportionally();
			}
			else
			{
				FinalState.Width = InitialState.Width * scale;
				FinalState.Depth = InitialState.Depth * scale;
				FinalState.Height = InitialState.Height * scale;
				for (int i = 0; i < FinalState.Diameters.Count; i++)
				{
					FinalState.Diameters[i] = InitialState.Diameters[i] * scale;
				}
			}
		}

		private void SetItem(IObject3D item, ScaleStates states)
		{
			setWidth?.Invoke(states.Width);
			setDepth?.Invoke(states.Depth);

			setHeight?.Invoke(states.Height);

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