﻿/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using System.Threading.Tasks;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	[HideChildrenFromTreeView]
	public abstract class PipeWorksBaseObject3D : Object3D
	{
		protected int Sides => 50;

		protected double ValidateValue(double currentValue, string keyName, double defaultValue)
		{
			double databaseValue = UserSettings.Instance.Fields.GetDouble(keyName, defaultValue);
			if (currentValue == 0)
			{
				currentValue = databaseValue;
			}

			if (currentValue != databaseValue)
			{
				UserSettings.Instance.Fields.SetDouble(keyName, currentValue);
			}

			return currentValue;
		}

		public override bool Persistable => ApplicationController.Instance.UserHasPermission(this);

		public override void OnInvalidate(InvalidateArgs invalidateType)
		{
			if (invalidateType.InvalidateType.HasFlag(InvalidateType.Properties)
				&& invalidateType.Source == this)
			{
				Rebuild();
			}
			else
			{
				base.OnInvalidate(invalidateType);
			}
		}

		protected IObject3D CreateReach(double reach, double innerDiameter)
		{
			var finWidth = 4.0;
			var finLength = innerDiameter;

			var pattern = new VertexStorage();
			pattern.MoveTo(0, 0);
			pattern.LineTo(finLength / 2, 0);
			pattern.LineTo(finLength / 2, reach - finLength / 8);
			pattern.LineTo(finLength / 2 - finLength / 8, reach);
			pattern.LineTo(-finLength / 2 + finLength / 8, reach);
			pattern.LineTo(-finLength / 2, reach - finLength / 8);
			pattern.LineTo(-finLength / 2, 0);

			var fin1 = new Object3D()
			{
				Mesh = VertexSourceToMesh.Extrude(pattern, finWidth)
			};
			fin1 = new TranslateObject3D(fin1, 0, 0, -finWidth / 2);
			// fin1.ChamferEdge(Face.Top | Face.Back, finLength / 8);
			// fin1.ChamferEdge(Face.Top | Face.Front, finLength / 8);
			fin1 = new RotateObject3D(fin1, -MathHelper.Tau / 4);
			var fin2 = new SetCenterObject3D(new RotateObject3D(fin1, 0, 0, MathHelper.Tau / 4), fin1.GetCenter());

			return new Object3D().SetChildren(new List<IObject3D>() { fin1, fin2 });
		}

		public override abstract Task Rebuild();
	}
}