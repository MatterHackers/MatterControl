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
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.DataConverters3D.UndoCommands;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MatterHackers.MatterControl.DesignTools
{
	[HideChildrenFromTreeView]
	public class TextObject3D : Object3D
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
		public StringOrExpression NameToWrite { get; set; } = "Text";

		public DoubleOrExpression PointSize { get; set; } = 24;

		[MaxDecimalPlaces(2)]
		public DoubleOrExpression Height { get; set; } = 5;

		[Sortable]
		[JsonConverter(typeof(StringEnumConverter))]
		public NamedTypeFace Font { get; set; } = NamedTypeFace.Nunito_Bold;

		public override bool CanFlatten => true;

		public override void Flatten(UndoBuffer undoBuffer)
		{
			// change this from a text object to a group
			var newContainer = new GroupObject3D();
			newContainer.CopyProperties(this, Object3DPropertyFlags.All);
			int index = 0;
			var nameToWrite = NameToWrite.Value(this);
			foreach (var child in this.Children)
			{
				var clone = child.Clone();
				var newName = index < nameToWrite.Length ? nameToWrite[index++].ToString() : "Letter".Localize();
				clone.Name = MapIfSymbol(newName);
				newContainer.Children.Add(clone);
			}

			undoBuffer.AddAndDo(new ReplaceCommand(new[] { this }, new[] { newContainer }));
			newContainer.Name = this.Name + " - " + "Flattened".Localize();
		}

		private string MapIfSymbol(string newName)
		{
			switch (newName)
			{
				case " ":
					return "space";
				default:
					break;
			}

			return newName;
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
					var nameToWrite = NameToWrite.Value(this);
					if (string.IsNullOrWhiteSpace(nameToWrite))
					{
						Mesh = PlatonicSolids.CreateCube(20, 10, height);
					}
					else
					{
						Mesh = null;
						this.Children.Modify(list =>
						{
							list.Clear();

							var offest = 0.0;
							double pointsToMm = 0.352778;

							foreach (var letter in nameToWrite.ToCharArray())
							{
								var style = new StyledTypeFace(ApplicationController.GetTypeFace(this.Font), PointSize.Value(this));
								var letterPrinter = new TypeFacePrinter(letter.ToString(), style)
								{
									ResolutionScale = 10
								};
								var scaledLetterPrinter = new VertexSourceApplyTransform(letterPrinter, Affine.NewScaling(pointsToMm));

								list.Add(new Object3D()
								{
									Mesh = VertexSourceToMesh.Extrude(scaledLetterPrinter, this.Height.Value(this)),
									Matrix = Matrix4X4.CreateTranslation(offest, 0, 0),
									Name = letter.ToString()
								});

								offest += letterPrinter.GetSize(letter.ToString()).X * pointsToMm;
							}
						});
					}
				}

				UiThread.RunOnIdle(() =>
				{
					rebuildLock.Dispose();
					Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Children));
				});
			});
		}
	}
}