/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
using MatterHackers.Localizations;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.RayTracer;
using MatterHackers.RayTracer.Traceable;
using MatterHackers.VectorMath;

namespace MatterHackers.MeshVisualizer
{
	public abstract class Object3DControl : IObject3DControl
	{
		public static string ScallingHint => "Hold 'Shift' to scale proportionally, Type 'Esc' to cancel".Localize();

		private bool _mouseIsOver = false;

		public Matrix4X4 TotalTransform { get; set; } = Matrix4X4.Identity;

		public Object3DControl(IObject3DControlContext object3DControlContext)
		{
			this.Object3DControlContext = object3DControlContext;
		}

		public ITraceable CollisionVolume { get; set; }

		public bool DrawOnTop { get; protected set; }

		public bool Visible { get; set; }

		public virtual string UiHint { get; }

		protected bool MouseDownOnControl { get; set; }

		public abstract void Dispose();

		public bool MouseIsOver
		{
			get => _mouseIsOver;
			set
			{
				if (_mouseIsOver != value)
				{
					_mouseIsOver = value;
					Invalidate();
				}
			}
		}

		public string Name { get; set; }

		protected IObject3DControlContext Object3DControlContext { get; }

		public IObject3D RootSelection
		{
			get
			{
				var selectedItemRoot = Object3DControlContext.Scene.SelectedItemRoot;
				var selectedItem = Object3DControlContext.Scene.SelectedItem;
				return (selectedItemRoot == selectedItem) ? selectedItem : null;
			}
		}

		public static Vector3 SetBottomControlHeight(AxisAlignedBoundingBox originalSelectedBounds, Vector3 cornerPosition)
		{
			if (originalSelectedBounds.MinXYZ.Z < 0)
			{
				if (originalSelectedBounds.MaxXYZ.Z < 0)
				{
					cornerPosition.Z = originalSelectedBounds.MaxXYZ.Z;
				}
				else
				{
					cornerPosition.Z = 0;
				}
			}

			return cornerPosition;
		}

		public virtual void Draw(DrawGlContentEventArgs e)
		{
		}

		public void Invalidate()
		{
			Object3DControlContext.GuiSurface.Invalidate();
		}

		public virtual void CancelOperation()
		{
			if (!string.IsNullOrEmpty(UiHint))
			{
				ApplicationController.Instance.UiHint = "";
			}
		}

		public virtual void OnMouseDown(Mouse3DEventArgs mouseEvent3D)
		{
			if (mouseEvent3D.MouseEvent2D.Button == MouseButtons.Left)
			{
				MouseDownOnControl = true;
				this.Object3DControlContext.GuiSurface.Invalidate();
				if (!string.IsNullOrEmpty(UiHint))
				{
					ApplicationController.Instance.UiHint = UiHint;
				}
			}
		}

		public virtual void OnMouseMove(Mouse3DEventArgs mouseEvent3D, bool mouseIsOver)
		{
			this.MouseIsOver = mouseIsOver;
		}

		public virtual void OnMouseUp(Mouse3DEventArgs mouseEvent3D)
		{
			MouseDownOnControl = false;

			if (!string.IsNullOrEmpty(UiHint))
			{
				ApplicationController.Instance.UiHint = "";
			}
		}

		public virtual void SetPosition(IObject3D selectedItem, MeshSelectInfo selectInfo)
		{
		}

		public ITraceable GetTraceable()
		{
			if (CollisionVolume != null)
			{
				ITraceable traceData = CollisionVolume;
				return new Transform(traceData, TotalTransform);
			}

			return null;
		}
	}
}