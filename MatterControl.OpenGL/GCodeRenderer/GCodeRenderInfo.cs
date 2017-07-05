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

using System;
using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.VectorMath;

namespace MatterHackers.GCodeVisualizer
{
	public class GCodeRenderInfo
	{
		private Vector2[] extruderOffsets;

		public Vector2 GetExtruderOffset(int index)
		{
			if (extruderOffsets != null
				&& extruderOffsets.Length > index)
			{
				return extruderOffsets[index];
			}

			return Vector2.Zero;
		}

		public Func<int, RGBA_Bytes> GetMaterialColor { get; }

		public int StartLayerIndex { get; set; }

		public int EndLayerIndex { get; set; }

		public Affine Transform { get; }

		public double LayerScale { get; }

		public RenderType CurrentRenderType { get; private set; }

		public double FeatureToStartOnRatio0To1 { get; set; }

		public double FeatureToEndOnRatio0To1 { get; set; }

		private Func<RenderType> GetRenderType;

		public GCodeRenderInfo()
		{
		}

		public GCodeRenderInfo(int startLayerIndex, int endLayerIndex,
			Affine transform, double layerScale,
			double featureToStartOnRatio0To1, double featureToEndOnRatio0To1,
			Vector2[] extruderOffsets,
			Func<RenderType> getRenderType,
			Func<int, RGBA_Bytes> getMaterialColor)
		{
			this.GetMaterialColor = getMaterialColor;
			this.StartLayerIndex = startLayerIndex;
			this.EndLayerIndex = endLayerIndex;
			this.Transform = transform;
			this.LayerScale = layerScale;

			// Store delegate
			this.GetRenderType = getRenderType;

			// Invoke delegate
			this.CurrentRenderType = this.GetRenderType();

			this.FeatureToStartOnRatio0To1 = featureToStartOnRatio0To1;
			this.FeatureToEndOnRatio0To1 = featureToEndOnRatio0To1;
			this.extruderOffsets = extruderOffsets;
		}

		public void RefreshRenderType()
		{
			this.CurrentRenderType = this.GetRenderType();
		}
	}
}