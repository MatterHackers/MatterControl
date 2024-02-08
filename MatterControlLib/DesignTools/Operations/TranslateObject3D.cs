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

using Matter_CAD_Lib.DesignTools.Interfaces;
using Matter_CAD_Lib.DesignTools.Objects3D;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.VectorMath;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
    public class TranslateObject3D : TransformWrapperObject3D
	{
		public TranslateObject3D()
		{
			Name = "Translate".Localize();
		}

		public TranslateObject3D(IObject3D item, double x = 0, double y = 0, double z = 0)
			: this(item, new Vector3(x, y, z))
		{
		}

		public TranslateObject3D(IObject3D itemToTranslate, Vector3 translation)
			: this()
		{
			using (this.RebuildLock())
			{
				WrapItems(new IObject3D[] { itemToTranslate });
				Matrix = Matrix4X4.CreateTranslation(translation);
			}
		}

		public static async Task<TranslateObject3D> Create(IObject3D itemToTranslate)
		{
			var translate = new TranslateObject3D();
			var aabb = itemToTranslate.GetAxisAlignedBoundingBox();

			var translateItem = new Object3D();
			translate.Children.Add(translateItem);
			translateItem.Children.Add(itemToTranslate);

			await translate.Rebuild();

			return translate;
		}

		public Vector3OrExpression Translation { get; set; } = Vector3.Zero;

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
			else if ((invalidateArgs.InvalidateType.HasFlag(InvalidateType.Properties) && invalidateArgs.Source == this))
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

			using (RebuildLock())
			{
				// set the matrix for the inner object
				ItemWithTransform.Matrix = Matrix4X4.CreateTranslation(Translation);
			}

			this.DoRebuildComplete();
			Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Matrix));
			return Task.CompletedTask;
		}
	}
}