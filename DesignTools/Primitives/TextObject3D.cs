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

using System.ComponentModel;
using MatterHackers.Agg.Font;
using MatterHackers.DataConverters3D;

namespace MatterHackers.MatterControl.DesignTools
{
	public enum NamedTypeFace { Liberation_Sans, Liberation_Sans_Bold, Liberation_Mono, Titillium, Damion };

	public static class NamedTypeFaceCache
	{
		public static TypeFace GetTypeFace(NamedTypeFace Name)
		{
			switch (Name)
			{
				case NamedTypeFace.Liberation_Sans:
					return LiberationSansFont.Instance;

				case NamedTypeFace.Liberation_Sans_Bold:
					return LiberationSansBoldFont.Instance;

				case NamedTypeFace.Liberation_Mono:
					return ApplicationController.MonoSpacedTypeFace;

				case NamedTypeFace.Titillium:
					return ApplicationController.TitilliumTypeFace;

				case NamedTypeFace.Damion:
					return ApplicationController.DamionTypeFace;

				default:
					return LiberationSansFont.Instance;
			}
		}
	}

	public class TextObject3D : Object3D, IRebuildable
	{
		public TextObject3D()
		{
		}

		public static TextObject3D Create()
		{
			var item = new TextObject3D();

			item.Rebuild();
			return item;
		}

		public override string ActiveEditor => "PublicPropertyEditor";

		[DisplayName("Name")]
		public string NameToWrite { get; set; } = "Text";

		public double PointSize { get; set; } = 24;

		public double Height { get; set; } = 5;

		public NamedTypeFace Font { get; set; } = new NamedTypeFace();

		public void Rebuild()
		{
			var letterPrinter = new TypeFacePrinter(NameToWrite, new StyledTypeFace(NamedTypeFaceCache.GetTypeFace(Font), PointSize * 0.352778));

			IObject3D nameMesh = new Object3D()
			{
				Mesh = VertexSourceToMesh.Extrude(letterPrinter, Height)
			};

			// output two meshes for card holder and text
			this.Children.Modify(list =>
			{
				list.Clear();
				list.Add(nameMesh);
			});
		}
	}
}