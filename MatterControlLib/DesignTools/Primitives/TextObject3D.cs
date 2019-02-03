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
using System.ComponentModel;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.DataConverters3D.UndoCommands;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Threading.Tasks;
using System;

namespace MatterHackers.MatterControl.DesignTools
{
	public class TextObject3D : Object3D, IVisualLeafNode
	{
		public TextObject3D()
		{
			Name = "Text".Localize();
			Color = Operations.Object3DExtensions.PrimitiveColors["Text"];
		}

		public static async Task<TextObject3D> Create()
		{
			var item = new TextObject3D();

			await item.Rebuild();

			return item;
		}

		[DisplayName("Text")]
		public string NameToWrite { get; set; } = "Text";

		public double PointSize { get; set; } = 24;

		public double Height { get; set; } = 5;

		[Sortable]
		[JsonConverter(typeof(StringEnumConverter))]
		public NamedTypeFace Font { get; set; } = new NamedTypeFace();

		public override bool CanFlatten => true;

		public override void Flatten(UndoBuffer undoBuffer)
		{
			// change this from a text object to a group
			var newContainer = new GroupObject3D();
			newContainer.CopyProperties(this, Object3DPropertyFlags.All);
			foreach (var child in this.Children)
			{
				newContainer.Children.Add(child.Clone());
			}
			undoBuffer.AddAndDo(new ReplaceCommand(new[] { this }, new[] { newContainer }));
		}

		public override async void OnInvalidate(InvalidateArgs invalidateType)
		{
			if ((invalidateType.InvalidateType.HasFlag(InvalidateType.Children)
				|| invalidateType.InvalidateType.HasFlag(InvalidateType.Matrix)
				|| invalidateType.InvalidateType.HasFlag(InvalidateType.Mesh))
				&& invalidateType.Source != this
				&& !RebuildLocked)
			{
				await Rebuild();
			}
			else if (invalidateType.InvalidateType.HasFlag(InvalidateType.Properties)
				&& invalidateType.Source == this)
			{
				await Rebuild();
			}
			else
			{
				base.OnInvalidate(invalidateType);
			}
		}

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");

			var rebuildLock = RebuildLock();

			return ApplicationController.Instance.Tasks.Execute(
				"Mirror".Localize(),
				null,
				(reporter, cancellationToken) =>
				{
					var aabb = (this).GetAxisAlignedBoundingBox();

					this.Children.Modify((List<IObject3D> list) =>
					{
						list.Clear();

						var offest = 0.0;
						double pointsToMm = 0.352778;
						foreach (var letter in NameToWrite.ToCharArray())
						{
							var letterPrinter = new TypeFacePrinter(letter.ToString(), new StyledTypeFace(ApplicationController.GetTypeFace(Font), PointSize))
							{
								ResolutionScale = 10
							};
							var scalledLetterPrinter = new VertexSourceApplyTransform(letterPrinter, Affine.NewScaling(pointsToMm));
							IObject3D letterObject = new Object3D()
							{
								Mesh = VertexSourceToMesh.Extrude(scalledLetterPrinter, Height)
							};

							letterObject.Matrix = Matrix4X4.CreateTranslation(offest, 0, 0);
							list.Add(letterObject);

							offest += letterPrinter.GetSize(letter.ToString()).X * pointsToMm;
						}
					});

					if (aabb.ZSize > 0)
					{
						// If the part was already created and at a height, maintain the height.
						PlatingHelper.PlaceMeshAtHeight(this, (double)aabb.MinXYZ.Z);
					}

					rebuildLock.Dispose();
					Invalidate(InvalidateType.Children);
					return Task.CompletedTask;
				});
		}
	}
}