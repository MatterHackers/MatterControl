/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using System.Collections.Generic;
using MatterControl.Printing;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.RenderOpenGl;
using MatterHackers.RenderOpenGl.OpenGl;

namespace MatterHackers.GCodeVisualizer
{
	[Flags]
	public enum RenderType
	{
		None = 0,
		Extrusions = 1,
		Moves = 2,
		Retractions = 4,
		SpeedColors = 8,
		SimulateExtrusion = 16,
		TransparentExtrusion = 64,
		GrayColors = 128
	};

	public class GCodeRenderer : IDisposable
	{
		public static double ExtruderWidth { get; set; } = .4;

		public static Color TravelColor = Color.Green;

		private static readonly bool Is32Bit = IntPtr.Size == 4;

		private List<List<int>> featureStartIndex = new List<List<int>>();
		private List<List<int>> featureEndIndex = new List<List<int>>();
		private List<List<RenderFeatureBase>> renderFeatures = new List<List<RenderFeatureBase>>();

		private List<GCodeVertexBuffer> layerVertexBuffer;
		private RenderType lastRenderType = RenderType.None;
		private GCodeRenderInfo renderInfo;
		private GCodeFile gCodeFileToDraw;

		public GCodeRenderer(GCodeFile gCodeFileToDraw)
		{
			if (gCodeFileToDraw != null)
			{
				this.gCodeFileToDraw = gCodeFileToDraw;

				if (gCodeFileToDraw is GCodeMemoryFile memoryFile)
				{
					this.ExtrusionColors = new ExtrusionColors(memoryFile.Speeds);
				}

				for (int i = 0; i < gCodeFileToDraw.LayerCount; i++)
				{
					renderFeatures.Add(new List<RenderFeatureBase>());
				}
			}
		}

		public GCodeFile GCodeFileToDraw => gCodeFileToDraw;

		public ExtrusionColors ExtrusionColors { get; } = null;

		public Color Gray { get; set; }

		public void CreateFeaturesForLayerIfRequired(int layerToCreate)
		{
			if (renderFeatures.Count == 0
				|| renderFeatures[layerToCreate].Count > 0)
			{
				return;
			}

			List<RenderFeatureBase> renderFeaturesForLayer = renderFeatures[layerToCreate];

			int startRenderIndex = gCodeFileToDraw.GetFirstLayerInstruction(layerToCreate);
			int endRenderIndex = gCodeFileToDraw.LineCount - 1;
			if (layerToCreate < gCodeFileToDraw.LayerCount - 1)
			{
				endRenderIndex = gCodeFileToDraw.GetFirstLayerInstruction(layerToCreate + 1);
			}

			for (int instructionIndex = startRenderIndex; instructionIndex < endRenderIndex; instructionIndex++)
			{
				PrinterMachineInstruction currentInstruction = gCodeFileToDraw.Instruction(instructionIndex);
				PrinterMachineInstruction previousInstruction = currentInstruction;
				if (instructionIndex > 0)
				{
					previousInstruction = gCodeFileToDraw.Instruction(instructionIndex - 1);
				}

				if (currentInstruction.Position == previousInstruction.Position)
				{
					double eMovement = 0;
					if (currentInstruction.PositionSet != PositionSet.E)
					{
						eMovement = currentInstruction.EPosition - previousInstruction.EPosition;
					}

					if (Math.Abs(eMovement) > 0)
					{
						// this is a retraction
						renderFeaturesForLayer.Add(new RenderFeatureRetract(instructionIndex, currentInstruction.Position, eMovement, currentInstruction.ToolIndex, currentInstruction.FeedRate));
					}
					if (currentInstruction.Line.StartsWith("G10"))
					{
						renderFeaturesForLayer.Add(new RenderFeatureRetract(instructionIndex, currentInstruction.Position, -1, currentInstruction.ToolIndex, currentInstruction.FeedRate));
					}
					else if (currentInstruction.Line.StartsWith("G11"))
					{
						renderFeaturesForLayer.Add(new RenderFeatureRetract(instructionIndex, currentInstruction.Position, 1, currentInstruction.ToolIndex, currentInstruction.FeedRate));
					}
				}
				else
				{
					if (gCodeFileToDraw.IsExtruding(instructionIndex))
					{
						double layerThickness = gCodeFileToDraw.GetLayerHeight(layerToCreate);

						Color extrusionColor = ExtrusionColors.GetColorForSpeed((float)currentInstruction.FeedRate);
						renderFeaturesForLayer.Add(
							new RenderFeatureExtrusion(
								instructionIndex,
								previousInstruction.Position,
								currentInstruction.Position,
								currentInstruction.ToolIndex,
								currentInstruction.FeedRate,
								currentInstruction.EPosition - previousInstruction.EPosition,
								gCodeFileToDraw.GetFilamentDiameter(),
								layerThickness,
								extrusionColor,
								this.Gray));
					}
					else
					{
						renderFeaturesForLayer.Add(
							new RenderFeatureTravel(
								instructionIndex,
								previousInstruction.Position,
								currentInstruction.Position,
								currentInstruction.ToolIndex,
								currentInstruction.FeedRate));
					}
				}
			}
		}

		public int GetNumFeatures(int layerToCountFeaturesOn)
		{
			CreateFeaturesForLayerIfRequired(layerToCountFeaturesOn);
			return renderFeatures[layerToCountFeaturesOn].Count;
		}

		public RenderFeatureBase this[int layerIndex, int featureIndex]
		{
			get
			{
				try
				{
					var layer = renderFeatures[layerIndex];

					if (featureIndex < layer.Count)
					{
						return layer[featureIndex];
					}
				}
				catch
				{
				}

				// Callers should test for non-null values
				return null;
			}
		}

		public bool GCodeInspector { get; set; } = false;

		public void Render(Graphics2D graphics2D, GCodeRenderInfo renderInfo)
		{
			if (renderFeatures.Count > 0)
			{
				CreateFeaturesForLayerIfRequired(renderInfo.EndLayerIndex);

				int featuresOnLayer = renderFeatures[renderInfo.EndLayerIndex].Count;
				int endFeature = (int)(featuresOnLayer * renderInfo.FeatureToEndOnRatio0To1 + .5);
				endFeature = Math.Max(0, Math.Min(endFeature, featuresOnLayer));

				int startFeature = (int)(featuresOnLayer * renderInfo.FeatureToStartOnRatio0To1 + .5);
				startFeature = Math.Max(0, Math.Min(startFeature, featuresOnLayer));

				// try to make sure we always draw at least one feature
				if (endFeature <= startFeature)
				{
					endFeature = Math.Min(startFeature + 1, featuresOnLayer);
				}
				if (startFeature >= endFeature)
				{
					// This can only happen if the start and end are set to the last feature
					// Try to set the start feature to one from the end
					startFeature = Math.Max(endFeature - 1, 0);
				}

				Graphics2DOpenGL graphics2DGl = graphics2D as Graphics2DOpenGL;
				if (graphics2DGl != null)
				{
					graphics2DGl.PreRender(Color.White);
					GL.Begin(BeginMode.Triangles);

					int lastFeature = endFeature - 1;
					for (int i = startFeature; i < endFeature; i++)
					{
						RenderFeatureBase feature = renderFeatures[renderInfo.EndLayerIndex][i];
						if (feature != null)
						{
							feature.Render(graphics2DGl, renderInfo, highlightFeature: this.GCodeInspector && i == lastFeature);
						}
					}
					GL.End();
					graphics2DGl.PopOrthoProjection();
				}
				else
				{
					for (int i = startFeature; i < endFeature; i++)
					{
						RenderFeatureBase feature = renderFeatures[renderInfo.EndLayerIndex][i];
						if (feature != null)
						{
							feature.Render(graphics2D, renderInfo);
						}
					}
				}
			}
		}

		private GCodeVertexBuffer Create3DDataForLayer(int layerIndex, GCodeRenderInfo renderInfo)
		{
			var colorVertexData = new VectorPOD<ColorVertexData>();
			var vertexIndexArray = new VectorPOD<int>();

			featureStartIndex[layerIndex].Clear();
			featureEndIndex[layerIndex].Clear();

			for (int i = 0; i < renderFeatures[layerIndex].Count; i++)
			{
				featureStartIndex[layerIndex].Add(vertexIndexArray.Count);

				RenderFeatureBase feature = renderFeatures[layerIndex][i];

				if (feature != null)
				{
					// Build the color and index data for the feature
					feature.CreateRender3DData(colorVertexData, vertexIndexArray, renderInfo);
				}

				featureEndIndex[layerIndex].Add(vertexIndexArray.Count);
			}

			// Construct and return the new VertexBuffer object with all color/index data
			return new GCodeVertexBuffer(vertexIndexArray.Array, colorVertexData.Array);
		}

		public void Dispose()
		{
			Clear3DGCode();
		}

		public void Clear3DGCode()
		{
			if (layerVertexBuffer != null)
			{
				for (int i = 0; i < layerVertexBuffer.Count; i++)
				{
					if (layerVertexBuffer[i] != null)
					{
						layerVertexBuffer[i].Dispose();
						layerVertexBuffer[i] = null;
					}
				}
			}
		}


		public void Render3D(GCodeRenderInfo renderInfo, DrawEventArgs e)
		{
			if (renderInfo == null)
			{
				return;
			}

			if (layerVertexBuffer == null)
			{
				layerVertexBuffer = new List<GCodeVertexBuffer>(gCodeFileToDraw.LayerCount);
				for (int layerIndex = 0; layerIndex < gCodeFileToDraw.LayerCount; layerIndex++)
				{
					layerVertexBuffer.Add(null);
					featureStartIndex.Add(new List<int>());
					featureEndIndex.Add(new List<int>());
				}
			}

			for (int layerIndex = 0; layerIndex < gCodeFileToDraw.LayerCount; layerIndex++)
			{
				CreateFeaturesForLayerIfRequired(layerIndex);
			}

			if (lastRenderType != renderInfo.CurrentRenderType)
			{
				Clear3DGCode();
				lastRenderType = renderInfo.CurrentRenderType;
			}

			if (renderFeatures.Count > 0)
			{
				for (int i = renderInfo.EndLayerIndex - 1; i >= renderInfo.StartLayerIndex; i--)
				{
					// If its the first render or we change what we are trying to render then create vertex data.
					if (layerVertexBuffer.Count > i
						&&  layerVertexBuffer[i] == null)
					{
						layerVertexBuffer[i] = Create3DDataForLayer(i, renderInfo);
					}
				}

				GL.Disable(EnableCap.Texture2D);
				GL.PushAttrib(AttribMask.EnableBit);
				GL.DisableClientState(ArrayCap.TextureCoordArray);
				GL.Enable(EnableCap.PolygonSmooth);

				if (renderInfo.EndLayerIndex - 1 > renderInfo.StartLayerIndex)
				{
					for (int i = renderInfo.StartLayerIndex; i < renderInfo.EndLayerIndex - 1; i++)
					{
						int featuresOnLayer = renderFeatures[i].Count;
						if (featuresOnLayer > 1
							&& layerVertexBuffer[i] != null)
						{
							layerVertexBuffer[i].RenderRange(0, featureEndIndex[i][featuresOnLayer - 1]);
						}
					}
				}

				// draw the partial layer of end-1 from startRatio to endRatio
				{
					int layerIndex = renderInfo.EndLayerIndex - 1;
					int featuresOnLayer = renderFeatures[layerIndex].Count;
					int startFeature = (int)(featuresOnLayer * renderInfo.FeatureToStartOnRatio0To1 + .5);
					startFeature = Math.Max(0, Math.Min(startFeature, featuresOnLayer));

					int endFeature = (int)(featuresOnLayer * renderInfo.FeatureToEndOnRatio0To1 + .5);
					endFeature = Math.Max(0, Math.Min(endFeature, featuresOnLayer));

					// try to make sure we always draw at least one feature
					if (endFeature <= startFeature)
					{
						endFeature = Math.Min(startFeature + 1, featuresOnLayer);
					}
					if (startFeature >= endFeature)
					{
						// This can only happen if the start and end are set to the last feature
						// Try to set the start feature to one from the end
						startFeature = Math.Max(endFeature - 1, 0);
					}

					if (endFeature > startFeature
						&& layerVertexBuffer[layerIndex] != null)
					{
						int ellementCount = featureEndIndex[layerIndex][endFeature - 1] - featureStartIndex[layerIndex][startFeature];

						layerVertexBuffer[layerIndex].RenderRange(featureStartIndex[layerIndex][startFeature], ellementCount);
					}
				}
				GL.PopAttrib();
			}
		}
	}
}