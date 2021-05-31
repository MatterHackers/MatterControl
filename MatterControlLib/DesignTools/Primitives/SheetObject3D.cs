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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.Platform;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.PolygonMesh;

namespace MatterHackers.MatterControl.DesignTools
{
	[HideChildrenFromTreeView]
	[HideMeterialAndColor]
	public class SheetObject3D : Object3D, IObject3DControlsProvider
	{
		public SheetData SheetData { get; set; }

		public override Mesh Mesh
		{
			get
			{
				if (!this.Children.Where(i => i.VisibleMeshes().Count() > 0).Any())
				{
					// add the amf content
					using (Stream measureAmfStream = StaticData.Instance.OpenStream(Path.Combine("Stls", "description_tool.amf")))
					{
						Children.Modify((list) =>
						{
							list.Clear();
							list.Add(AmfDocument.Load(measureAmfStream, CancellationToken.None));
						});
					}
				}

				return base.Mesh;
			}

			set => base.Mesh = value;
		}

		public static async Task<SheetObject3D> Create()
		{
			var item = new SheetObject3D();
			item.SheetData = new SheetData(5, 5);
			await item.Rebuild();
			return item;
		}

		public override async void OnInvalidate(InvalidateArgs invalidateType)
		{
			if (invalidateType.InvalidateType.HasFlag(InvalidateType.SheetUpdated) && invalidateType.Source == this)
			{
				using (RebuildLock())
				{
					// send a message to all our siblings and their children
					SendInvalidateToAll();
				}
			}
			else
			{
				base.OnInvalidate(invalidateType);
			}
		}

		private void SendInvalidateToAll()
		{
			foreach (var sibling in this.Parent.Children)
			{
				SendInvalidateRecursive(sibling);
			}
		}

		private void SendInvalidateRecursive(IObject3D item)
		{
			// process depth first
			foreach(var child in item.Children)
			{
				SendInvalidateRecursive(child);
			}

			// and send the invalidate
			item.Invalidate(new InvalidateArgs(item, InvalidateType.SheetUpdated));
		}

		public static T FindTableAndValue<T>(IObject3D owner, string cellId)
		{
			// look through all the parents
			foreach (var parent in owner.Parents())
			{
				// then each child of any give parent
				foreach (var sibling in parent.Children)
				{
					var expression = "";
					// if it is a sheet
					if (sibling != owner
						&& sibling is SheetObject3D sheet)
					{
						// try to manage the cell into the correct data type
						expression = sheet.SheetData[cellId]?.Expression;

						if (typeof(T) == typeof(double))
						{
							if (double.TryParse(expression, out double doubleValue))
							{
								return (T)(object)doubleValue;
							}
							// else return an error
							return (T)(object)5.5;
						}
					}
				}
			}

			throw new NotImplementedException();
		}

		public void AddObject3DControls(Object3DControlsLayer object3DControlsLayer)
		{
		}
	}
}