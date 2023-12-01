/*
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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.DataConverters3D.UndoCommands;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MatterHackers.MatterControl.DesignTools
{
    public enum OutputDimensions
    {
        [EnumNameAttribute("2D")]
        [DescriptionAttribute("Create a 2D Path")]
        Output2D,
        [EnumNameAttribute("3D")]
        [DescriptionAttribute("Create a 3D Mesh")]
		Output3D
	}
    
	[HideChildrenFromTreeView]
    public class TextObject3D : PathContainerObject3D, IPropertyGridModifier, IEditorDraw, IPrimaryOperationsSpecifier
    {
        private bool refreshToolBar;

        [JsonConverter(typeof(StringEnumConverter))]
		public enum TextAlign
		{
			Left,
			Center,
			Right,
		}

		public TextObject3D()
		{
			Name = "Text".Localize();
			Color = Operations.Object3DExtensions.PrimitiveColors["Text"];
		}

		public static async Task<TextObject3D> Create(bool setTo2D = false)
		{
			var item = new TextObject3D();

            if (setTo2D)
            {
				item.Output = OutputDimensions.Output2D;
            }

			await item.Rebuild();

			return item;
		}

        public bool MultiLine { get; set; }

        [EnumDisplay(IconPaths = new string[] { "align_left.png", "align_center_x.png", "align_right.png" }, InvertIcons = true)]
		public TextAlign Alignment { get; set; } = TextAlign.Left;

		[DisplayName("Text")]
		public StringOrExpression NameToWrite { get; set; } = "Text";

		[MultiLineEdit]
		[DisplayName("Text")]
		public StringOrExpression MultiLineText { get; set; } = "MultiLine\nText";

        [Slider(1, 48, snapDistance: 1)]
		public DoubleOrExpression PointSize { get; set; } = 24;

		[MaxDecimalPlaces(2)]
		[Slider(1, 400, VectorMath.Easing.EaseType.Quadratic, useSnappingGrid: true)]
		public DoubleOrExpression Height { get; set; } = 3;

		[Sortable]
		[JsonConverter(typeof(StringEnumConverter))]
		public NamedTypeFace Font { get; set; } = NamedTypeFace.Nunito_Bold;

		[EnumDisplay(Mode = EnumDisplayAttribute.PresentationMode.Buttons)]
		public OutputDimensions Output { get; set; } = OutputDimensions.Output3D;

		public bool WrapLines { get; set; } = false;

        [Description("The width to wrap at in mm")]
        public DoubleOrExpression WrappingWidth { get; set; } = 200;

        [Description("The number of spaces to add after wrapping a line. Very useful for bullet points.")]
        public IntOrExpression WrappingIndent { get; set; } = 0;
        
		public override bool CanApply => true;

        public override bool MeshIsSolidObject => Output == OutputDimensions.Output3D;

        public override IVertexSource GetVertexSource()
		{
			if (Output == OutputDimensions.Output2D)
			{
				return this.CombinedVisibleChildrenPaths();
			}

			return null;
		}

        public override void Apply(UndoBuffer undoBuffer)
		{
			// change this from a text object to a group
			var newContainer = new GroupObject3D();
			newContainer.CopyProperties(this, Object3DPropertyFlags.All);
			foreach (var child in this.Children)
			{
				var clone = child.Clone();
				newContainer.Children.Add(clone);
			}

			undoBuffer.AddAndDo(new ReplaceCommand(new[] { this }, new[] { newContainer }));
			newContainer.Name = this.Name;
		}

		public override async void OnInvalidate(InvalidateArgs invalidateArgs)
		{
			if ((invalidateArgs.InvalidateType.HasFlag(InvalidateType.Children)
				|| invalidateArgs.InvalidateType.HasFlag(InvalidateType.Matrix)
				|| invalidateArgs.InvalidateType.HasFlag(InvalidateType.Mesh))
				&& invalidateArgs.Source != this
				&& !RebuildLocked)
			{
				await Rebuild();
			}
			else if (invalidateArgs.InvalidateType.HasFlag(InvalidateType.Properties)
				&& invalidateArgs.Source == this)
			{
				await Rebuild();
			}
			else if (Expressions.NeedRebuild(this, invalidateArgs))
			{
				await Rebuild();
			}
			else
			{
				base.OnInvalidate(invalidateArgs);
			}
		}

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");

			var rebuildLock = RebuildLock();

			return Task.Run(() =>
			{
				using (new CenterAndHeightMaintainer(this))
				{
					bool valuesChanged = false;
					var height = Height.ClampIfNotCalculated(this, .01, 1000000, ref valuesChanged);
					var wrappingWidth_mm = WrappingWidth.Value(this);
					var wrappingIndent = WrappingIndent.ClampIfNotCalculated(this, 0, 100, ref valuesChanged);

                    if (wrappingWidth_mm < 10)
					{
						wrappingWidth_mm = 0;
					}

					var textToWrite = MultiLine
						? MultiLineText.Value(this).Replace("\\n", "\n").Replace("\r", "\n")
						: NameToWrite.Value(this).Replace("\\n", "\n").Replace("\r", "\n");

                    var pointSize = PointSize.Value(this);

					var mmPerInch = 25.4;
                    var mmPerPoint = mmPerInch / StyledTypeFace.PointsPerInch;
                    var pixelsPerPoint = StyledTypeFace.PixelsPerInch / StyledTypeFace.PointsPerInch;

                    if (WrapLines && wrappingWidth_mm > 0)
					{
						var wrapper = new EnglishTextWrapping(pointSize);
						var wrappingPoints = wrappingWidth_mm / mmPerPoint;
                        var wrappingPixels = wrappingPoints * pixelsPerPoint;
                        textToWrite = wrapper.InsertCRs(textToWrite, wrappingPixels, MultiLine ? wrappingIndent : 0);
					}

                    if (string.IsNullOrWhiteSpace(textToWrite))
					{
						Mesh = PlatonicSolids.CreateCube(20, 10, height);
					}
					else
					{
						Mesh = null;
						var extrusionHeight = Output == OutputDimensions.Output2D ? Constants.PathPolygonsHeight : this.Height.Value(this);
						this.Children.Modify(list =>
						{
							list.Clear();

							var offset = Vector2.Zero;
							var lineNumber = 1;
							var leterNumber = 1;
							var lineObject = new Object3D()
							{
								Name = "Line {0}".Localize().FormatWith(lineNumber)
							};
							list.Add(lineObject);

							foreach (var letter in textToWrite.ToCharArray())
							{
								var style = new StyledTypeFace(ApplicationController.GetTypeFace(this.Font), pointSize);
								var letterPrinter = new TypeFacePrinter(letter.ToString(), style)
								{
									ResolutionScale = 10
								};
								var scaledLetterPrinter = new VertexSourceApplyTransform(letterPrinter, Affine.NewScaling(mmPerPoint));

								if (letter == '\n')
								{
									leterNumber = 0;
									lineNumber++;
									offset.X = 0;
									offset.Y -= style.EmSizeInPoints * mmPerPoint * 1.4;
									lineObject = new Object3D()
									{
										Matrix = Matrix4X4.CreateTranslation(0, offset.Y, 0),
										Name = "Line {0}".Localize().FormatWith(lineNumber)
									};
									list.Add(lineObject);
								}
								else
								{
									Object3D letterObject = null;
									switch (letter)
									{
										case ' ':
											offset.X += letterPrinter.GetSize(" ").X * mmPerPoint;
											break;

										case '\t':
											offset.X += letterPrinter.GetSize("    ").X * mmPerPoint;
											break;

										default:
											letterObject = new Object3D()
											{
												Mesh = VertexSourceToMesh.Extrude(scaledLetterPrinter, extrusionHeight),
												Matrix = Matrix4X4.CreateTranslation(offset.X, 0, 0),
												Name = leterNumber.ToString("000") + " - '" + letter.ToString() + "'"
											};
											if (Output == OutputDimensions.Output2D)
											{
												var pathObject = this as PathContainerObject3D;
                                                pathObject.VertexStorage = new VertexStorage(
													new VertexSourceApplyTransform(
														new VertexStorage(scaledLetterPrinter), Affine.NewTranslation(offset.X, offset.Y)));
											}
											offset.X += letterPrinter.GetSize(letter.ToString()).X * mmPerPoint;
											break;
									}

									if (letterObject?.Mesh.Faces.Count > 0)
									{
										lineObject.Children.Add(letterObject);
										leterNumber++;
									}
								}
							}

							for (var i = list.Count - 1; i >= 0; i--)
							{
								if (list[i].Children.Count == 0)
								{
									list.RemoveAt(i);
								}
							}

							if (list.Count > 1 && Alignment != TextAlign.Left)
							{
								var widest = 0.0;
								for (var i = 0; i < list.Count; i++)
								{
									widest = Math.Max(widest, list[i].GetAxisAlignedBoundingBox().XSize);
									if (list[i].Children.Count == 0)
									{
										list.RemoveAt(i);
									}
								}

								for (var i = 0; i < list.Count; i++)
								{
									var delta = widest - list[i].GetAxisAlignedBoundingBox().XSize;
									// apply any alignment to the lines
									switch (Alignment)
									{
										case TextAlign.Center:
											list[i].Matrix *= Matrix4X4.CreateTranslation(delta / 2, 0, 0);
											break;

										case TextAlign.Right:
											list[i].Matrix *= Matrix4X4.CreateTranslation(delta, 0, 0);
											break;
									}
								}
							}
						});
					}
				}

				UiThread.RunOnIdle(() =>
				{
					rebuildLock.Dispose();
					Invalidate(InvalidateType.DisplayValues);
					this.CancelAllParentBuilding();
					Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Children));
                    if (refreshToolBar)
                    {
						this.RefreshToolBar();
					}
				});
			});
		}

        public void UpdateControls(PublicPropertyChange change)
        {
			change.SetRowVisible(nameof(MultiLineText), () => MultiLine);
			change.SetRowVisible(nameof(Alignment), () => MultiLine);
			change.SetRowVisible(nameof(NameToWrite), () => !MultiLine);
			change.SetRowVisible(nameof(Height), () => Output == OutputDimensions.Output3D);
			change.SetRowVisible(nameof(WrappingWidth), () => WrapLines);
            change.SetRowVisible(nameof(WrappingIndent), () => MultiLine && WrapLines);
            if (change.PropertyChanged == nameof(Output))
            {
				refreshToolBar = true;
            }
		}

        public void DrawEditor(Object3DControlsLayer object3DControlLayer, DrawEventArgs e)
        {
			this.DrawPath();
		}

		public AxisAlignedBoundingBox GetEditorWorldspaceAABB(Object3DControlsLayer layer)
        {
			return this.GetWorldspaceAabbOfDrawPath();
		}

        public IEnumerable<SceneOperation> GetOperations()
        {
            if (Output == OutputDimensions.Output2D)
            {
                return PathContainerObject3D.GetOperations(this.GetType());
            }

            // return no enumerations
            return Enumerable.Empty<SceneOperation>();
        }
    }
}