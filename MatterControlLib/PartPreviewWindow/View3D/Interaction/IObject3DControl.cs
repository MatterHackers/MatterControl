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

using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.RayTracer;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.MeshVisualizer
{
	/// <summary>
	/// Basic Interaction control - notified of position per OpenGL draw
	/// </summary>
	public interface IObject3DControl : IDisposable
	{
		string Name { get; }

		string UiHint { get; }

		void SetPosition(IObject3D selectedItem, MeshSelectInfo selectInfo);

		/// <summary>
		/// The Control has been requested to cancel (usually by the user).
		/// The current state of the operation or edit should be returned to the starting state.
		/// </summary>
		void CancelOperation();

		void OnMouseDown(Mouse3DEventArgs mouseEvent3D);

		void OnMouseMove(Mouse3DEventArgs mouseEvent3D, bool mouseIsOver);

		void OnMouseUp(Mouse3DEventArgs mouseEvent3D);

		/// <summary>
		/// Gets or sets a value indicating whether the control is currently visible.
		/// </summary>
		bool Visible { get; set; }

		bool DrawOnTop { get; }

		void Draw(DrawGlContentEventArgs e);
		
		/// <returns>The worldspace AABB of the 3D geometry drawn by Draw.</returns>
		AxisAlignedBoundingBox GetWorldspaceAABB();

		ITraceable GetTraceable();
	}
}