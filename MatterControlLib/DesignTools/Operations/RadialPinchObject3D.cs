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

using CsvHelper.Configuration.Attributes;
using Matter_CAD_Lib.DesignTools.Interfaces;
using Matter_CAD_Lib.DesignTools.Objects3D;
using MatterControlLib.DesignTools.Operations.Path;
using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Typography.OpenFont;

namespace MatterHackers.MatterControl.DesignTools
{
    public class RadialPinchObject3D : OperationSourceContainerObject3D, IPropertyGridModifier, IEditorDraw, IPathEditorDraw
    {
        private Dictionary<string, bool> changeSet = new Dictionary<string, bool>();

        public RadialPinchObject3D()
        {
            // make sure the path editor is registered
            PropertyEditor.RegisterEditor(typeof(PathEditorFactory.EditableVertexStorage), new PathEditorFactory());

            Name = "Radial Pinch".Localize();
        }

        public enum PinchType
        { Radial, XAxis, YAxis }

        [PathEditorFactory.ShowOrigin]
        public PathEditorFactory.EditableVertexStorage PathForHorizontalOffsets { get; set; } = new PathEditorFactory.EditableVertexStorage();

        [Description("Specifies the number of vertical cuts required to ensure the part can be pinched well.")]
        [Slider(0, 50, snapDistance: 1)]
        public IntOrExpression PinchSlices { get; set; } = 20;

        [EnumDisplay(Mode = EnumDisplayAttribute.PresentationMode.Buttons)]
        [Description("Enable advanced features.")]
        public bool Advanced { get; set; } = false;

        [Name("Pinch Type")]
        public PinchType PinchTypeValue { get; set; } = PinchType.Radial;

        [Description("Allows for the repositioning of the rotation origin")]
        public Vector2 RotationOffset { get; set; }

        private Vector2 GetRotationOffset()
        {
            if (Advanced)
            {
                return RotationOffset;
            }

            return Vector2.Zero;
        }

        public IRadiusProvider RadiusProvider
        {
            get
            {
                if (this.SourceContainer.Children.Count == 1
                        && this.SourceContainer.Children.First() is IRadiusProvider radiusProvider)
                {
                    return radiusProvider;
                }

                return null;
            }
        }

        public void DrawEditor(Object3DControlsLayer layer, DrawEventArgs e)
        {
            var sourceAabb = this.SourceContainer.GetAxisAlignedBoundingBox();
            var rotationCenter = SourceContainer.GetSmallestEnclosingCircleAlongZ().Center + GetRotationOffset();

            var center = new Vector3(rotationCenter.X, rotationCenter.Y, sourceAabb.Center.Z);

            // render the top and bottom rings
            layer.World.RenderCylinderOutline(this.WorldMatrix(), center, 1, sourceAabb.ZSize, 15, Color.Red, Color.Red, 5);

            // turn the lighting back on
            GL.Enable(EnableCap.Lighting);
        }

        public AxisAlignedBoundingBox GetEditorWorldspaceAABB(Object3DControlsLayer layer)
        {
            var sourceAabb = this.SourceContainer.GetAxisAlignedBoundingBox();
            var rotationCenter = SourceContainer.GetSmallestEnclosingCircleAlongZ().Center + GetRotationOffset();
            var center = new Vector3(rotationCenter.X, rotationCenter.Y, sourceAabb.Center.Z);
            return AxisAlignedBoundingBox.CenteredBox(new Vector3(1, 1, sourceAabb.ZSize), center).NewTransformed(this.WorldMatrix());
        }

        public void BeforePathEditorDraw(Graphics2D graphics2D, PathEditorWidget pathEditorWidget)
        {
            var theme = ApplicationController.Instance.Theme;
            var sourceAabb = SourceContainer.GetAxisAlignedBoundingBox();
            var lineColor = theme.PrimaryAccentColor.WithAlpha(50);

            var leftOrigin = new Vector2(-10000, sourceAabb.ZSize);
            var rightOrigin = new Vector2(10000, sourceAabb.ZSize);
            graphics2D.Line(pathEditorWidget.TotalTransform.Transform(leftOrigin), pathEditorWidget.TotalTransform.Transform(rightOrigin), lineColor);
            graphics2D.DrawString(sourceAabb.ZSize.ToString("0.##"), pathEditorWidget.TotalTransform.Transform(new Vector2(0, sourceAabb.ZSize)) + new Vector2(2, 2), 9, color: lineColor.WithAlpha(150));

            var maxWidthDepth = Math.Max(sourceAabb.XSize / 2 + GetRotationOffset().X, sourceAabb.YSize / 2 + GetRotationOffset().Y);

            var bottomOrigin = new Vector2(maxWidthDepth, -10000);
            var topOrigin = new Vector2(maxWidthDepth, 10000);

            graphics2D.Line(pathEditorWidget.TotalTransform.Transform(bottomOrigin), pathEditorWidget.TotalTransform.Transform(topOrigin), lineColor);
            graphics2D.DrawString(maxWidthDepth.ToString("0.##"), pathEditorWidget.TotalTransform.Transform(new Vector2(maxWidthDepth, 0)) + new Vector2(2, 2), 9, color: lineColor.WithAlpha(150));
        }

        public override Task Rebuild()
        {
            this.DebugDepth("Rebuild");

            bool valuesChanged = false;

            var aabb = this.GetAxisAlignedBoundingBox();

            var pinchSlices = PinchSlices.ClampIfNotCalculated(this, 0, 300, ref valuesChanged);
            var rebuildLocks = this.RebuilLockAll();

            return ApplicationController.Instance.Tasks.Execute(
                "Pinch".Localize(),
                null,
                (reporter, cancellationToken) =>
                {
                    var sourceAabb = this.SourceContainer.GetAxisAlignedBoundingBox();

                    var bottom = sourceAabb.MinXYZ.Z;
                    var top = sourceAabb.MaxXYZ.Z;
                    var size = sourceAabb.ZSize;

                    double numberOfCuts = pinchSlices;

                    double cutSize = size / numberOfCuts;
                    var cuts = new List<double>();
                    for (int i = 0; i < numberOfCuts + 1; i++)
                    {
                        var ratio = i / numberOfCuts;
                        cuts.Add(bottom - cutSize + (size * ratio));
                    }

                    // get the rotation from the center of the circumscribed circle of the convex hull
                    var maxWidthDepth = Math.Max(sourceAabb.XSize / 2 + GetRotationOffset().X, sourceAabb.YSize / 2 + GetRotationOffset().Y);
                    var rotationCenter = new Vector2(sourceAabb.Center) + GetRotationOffset();

                    // if there is no path make one
                    if (PathForHorizontalOffsets.Count == 0)
                    {
                        var bottomPoint = new Vector2(maxWidthDepth, bottom * 10);
                        var topPoint = new Vector2(maxWidthDepth, top * 10);
                        var middlePoint = (bottomPoint + topPoint) / 2;
                        middlePoint.X *= 2;

                        var Point1 = new Vector2(maxWidthDepth, bottom);
                        var Point2 = new Vector2(maxWidthDepth, bottom + (top - bottom) * .2);
                        var Point3 = new Vector2(maxWidthDepth * 1.5, bottom + (top - bottom) * .2);
                        var Point4 = new Vector2(maxWidthDepth * 1.5, bottom + (top - bottom) * .5);
                        var Point5 = new Vector2(maxWidthDepth * 1.5, bottom + (top - bottom) * .8);
                        var Point6 = new Vector2(maxWidthDepth, bottom + (top - bottom) * .8);
                        var Point7 = new Vector2(maxWidthDepth, top);

                        PathForHorizontalOffsets.Clear();
                        PathForHorizontalOffsets.MoveTo(Point1);
                        PathForHorizontalOffsets.Curve4(Point2, Point3, Point4);
                        PathForHorizontalOffsets.Curve4(Point5, Point6, Point7);
                    }

                    var horizontalOffset = new FlattenCurves(new VertexSourceApplyTransform(PathForHorizontalOffsets, Affine.NewScaling(10)));

                    var xAtYInterpolator = new XAtYInterpolator(horizontalOffset);

                    var pinchedChildren = new List<IObject3D>();

                    foreach (var sourceItem in SourceContainer.VisibleMeshes())
                    {
                        var originalMesh = sourceItem.Mesh;
                        var status = "Copy Mesh".Localize();
                        reporter?.Invoke(0, status);
                        var transformedMesh = originalMesh.Copy(CancellationToken.None);
                        var itemMatrix = sourceItem.WorldMatrix(SourceContainer);

                        // transform into this space
                        transformedMesh.Transform(itemMatrix);

                        status = "Split Mesh".Localize();
                        reporter?.Invoke(0, status);

                        // split the mesh along the z axis
                        transformedMesh.SplitOnPlanes(Vector3.UnitZ, cuts, cutSize / 8);

                        for (int i = 0; i < transformedMesh.Vertices.Count; i++)
                        {
                            var position = transformedMesh.Vertices[i];

                            var ratio = 1.0;

                            if (position.Z >= bottom
                                && position.Z <= top)
                            {
                                ratio = (position.Z - bottom) / size;
                            }

                            var positionXy = new Vector2(position) - rotationCenter;
                            positionXy *= horizontalOffset.GetXAtY(position.Z * 10) / (maxWidthDepth * 10);
                            if (Advanced)
                            {
                                if (PinchTypeValue == PinchType.XAxis)
                                {
                                    // only use the x value
                                    positionXy.Y = position.Y - rotationCenter.Y;
                                }
                                else if (PinchTypeValue == PinchType.YAxis)
                                {
                                    // only use the y value
                                    positionXy.X = position.X - rotationCenter.X;
                                }
                            }
                            positionXy += rotationCenter;
                            transformedMesh.Vertices[i] = new Vector3Float(positionXy.X, positionXy.Y, position.Z);
                        }

                        // transform back into item local space
                        transformedMesh.Transform(itemMatrix.Inverted);

                        //transformedMesh.MergeVertices(.1);
                        transformedMesh.CalculateNormals();

                        var pinchedChild = new Object3D()
                        {
                            Mesh = transformedMesh
                        };
                        pinchedChild.CopyWorldProperties(sourceItem, SourceContainer, Object3DPropertyFlags.All, false);
                        pinchedChild.Visible = true;

                        pinchedChildren.Add(pinchedChild);
                    }

                    RemoveAllButSource();
                    this.SourceContainer.Visible = false;

                    this.Children.Modify((list) =>
                    {
                        list.AddRange(pinchedChildren);
                    });

                    ApplyHoles(reporter, cancellationToken.Token);

                    UiThread.RunOnIdle(() =>
                    {
                        rebuildLocks.Dispose();
                        this.DoRebuildComplete();
                        Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Children));
                        Invalidate(InvalidateType.DisplayValues);
                    });

                    return Task.CompletedTask;
                });
        }

        public void UpdateControls(PublicPropertyChange change)
        {
            changeSet.Clear();

            changeSet.Add(nameof(RotationOffset), Advanced);
            changeSet.Add(nameof(PinchTypeValue), Advanced);

            // first turn on all the settings we want to see
            foreach (var kvp in changeSet.Where(c => c.Value))
            {
                change.SetRowVisible(kvp.Key, () => kvp.Value);
            }

            // then turn off all the settings we want to hide
            foreach (var kvp in changeSet.Where(c => !c.Value))
            {
                change.SetRowVisible(kvp.Key, () => kvp.Value);
            }
        }
    }

    internal class XAtYInterpolator
    {
        private double bottom;
        private int numberOfSegments;
        private double[] offsetAtY;
        private double top;

        public XAtYInterpolator(IVertexSource inputCurveIn)
        {
            var inputCurve = new VertexStorage(inputCurveIn);

            var bounds = inputCurve.GetBounds();
            bottom = bounds.Bottom;
            top = bounds.Top;
            numberOfSegments = 100;
            offsetAtY = new double[numberOfSegments + 1];
            for (int i = 0; i < numberOfSegments + 1; i++)
            {
                var y = bottom + (top - bottom) * i / numberOfSegments;
                offsetAtY[i] = inputCurve.GetXAtY(y);
            }
        }

        internal double Get(double y)
        {
            // check if we are bellow the bottom
            if (y <= bottom)
            {
                return offsetAtY[0];
            }

            // check if we are above the top
            if (y >= top)
            {
                return offsetAtY[numberOfSegments];
            }

            // find the segment we are in
            var segment = (int)((y - bottom) / (top - bottom) * numberOfSegments);
            // lerp between the two points
            var ratio = (y - bottom) / (top - bottom) * numberOfSegments - segment;
            return offsetAtY[segment] * (1 - ratio) + offsetAtY[segment + 1] * ratio;
        }
    }
}