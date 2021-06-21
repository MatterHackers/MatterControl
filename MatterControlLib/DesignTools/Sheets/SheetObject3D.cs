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
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;
using org.mariuszgromada.math.mxparser;

namespace MatterHackers.MatterControl.DesignTools
{
	[HideChildrenFromTreeView]
	[HideMeterialAndColor]
	[WebPageLink("Documentation", "Open", "https://matterhackers.com/support/mattercontrol-variable-support")]
	[MarkDownDescription("[BETA] - Experimental support for variables and equations with a sheets like interface.")]
	public class SheetObject3D : Object3D, IObject3DControlsProvider
	{
		private SheetData _sheetData;
		public SheetData SheetData
		{
			get => _sheetData;

			set
			{
				if (_sheetData != value)
				{
					if (_sheetData != null)
					{
						_sheetData.Recalculated -= SendInvalidateToAll;
					}

					_sheetData = value;
					_sheetData.Recalculated += SendInvalidateToAll;
				}
			}
		}

		public static async Task<SheetObject3D> Create()
		{
			var item = new SheetObject3D
			{
				SheetData = new SheetData(5, 5)
			};
			await item.Rebuild();
			return item;
		}

		public override Mesh Mesh
		{
			get
			{
				if (this.Children.Count == 0 
					|| this.Children.Where(i => i.Mesh == null).Any())
				{
					this.Children.Modify((list) =>
					{
						list.Clear();
						Mesh border;
						using (Stream stlStream = StaticData.Instance.OpenStream(Path.Combine("Stls", "sheet_border.stl")))
						{
							border = StlProcessing.Load(stlStream, CancellationToken.None);
						}
						list.Add(new Object3D()
						{
							Mesh = border,
							Color = new Color("#9D9D9D")
						});
						Mesh boxes;
						using (Stream stlStream = StaticData.Instance.OpenStream(Path.Combine("Stls", "sheet_boxes.stl")))
						{
							boxes = StlProcessing.Load(stlStream, CancellationToken.None);
						}
						list.Add(new Object3D()
						{
							Mesh = boxes,
							Color = new Color("#117c43")
						});

						var aabb = border.GetAxisAlignedBoundingBox();
						this.Matrix *= Matrix4X4.CreateScale(20 / aabb.XSize);
					});
				}

				return null;
			}

			set => base.Mesh = value; 
		}

		public SheetObject3D()
		{
		}

		public override bool Persistable => false;

		internal class UpdateItem
		{
			internal int depth;
			internal IObject3D item;
			internal RebuildLock rebuildLock;
		}
		private void SendInvalidateToAll(object s, EventArgs e)
		{
			var updateItems = new List<UpdateItem>();
			foreach (var sibling in this.Parent.Children)
			{
				if (sibling != this)
				{
					AddItemsToList(sibling, updateItems, 0);
				}
			}
			if (updateItems.Count == 0)
			{
				return;
			}

			// sort them
			updateItems.Sort((a, b) => a.depth.CompareTo(b.depth));
			// lock everything
			foreach (var depthItem in updateItems)
			{
				depthItem.rebuildLock = depthItem.item.RebuildLock();
			}

			// and send the invalidate
			RunningInterval runningInterval = null;
			void RebuildWhenUnlocked()
			{
				// get the last item from the list
				var index = updateItems.Count - 1;
				var updateItem = updateItems[index];
				// if it is locked from above
				if (updateItem.rebuildLock != null)
				{
					// release the lock and rebuild
					// and ask it to update
					var depthToBuild = updateItem.depth;
					for (int i = 0; i < updateItems.Count; i++)
					{
						if (updateItems[i].depth == updateItem.depth)
						{
							updateItems[i].rebuildLock.Dispose();
							updateItems[i].rebuildLock = null;
							updateItems[i].item.Invalidate(new InvalidateArgs(this, InvalidateType.SheetUpdated));
						}
					}
				}
				else if (updateItems.Where(i => i.depth == updateItem.depth && i.item.RebuildLocked).Any())
				{
					// wait for the current rebuild to end
					return;
				}
				else
				{
					// remove all items at this level
					for (int i = updateItems.Count - 1; i >= 0; i--)
					{
						if (updateItems[i].depth == updateItem.depth)
						{
							updateItems.RemoveAt(i);
						}
					}
				}

				if (updateItems.Count == 0)
				{
					UiThread.ClearInterval(runningInterval);
				}
			}

			// rebuild depth first
			runningInterval = UiThread.SetInterval(RebuildWhenUnlocked, .01);
		}

		private void AddItemsToList(IObject3D inItem, List<UpdateItem> updatedItems, int inDepth)
		{
			// process depth first
			foreach(var child in inItem.Children)
			{
				AddItemsToList(child, updatedItems, inDepth + 1);
			}

			updatedItems.Add(new UpdateItem()
			{
				depth = inDepth,
				item = inItem
			});
		}

		public static T EvaluateExpression<T>(IObject3D owner, string inputExpression)
		{
			// check if the expression is not an equation (does not start with "=")
			if (inputExpression.Length > 0 && inputExpression[0] != '=')
			{
				if (typeof(T) == typeof(string))
				{
					return (T)(object)inputExpression;
				}

				// not an equation so try to parse it directly
				if (double.TryParse(inputExpression, out var result))
				{
					if (typeof(T) == typeof(double))
					{
						return (T)(object)result;
					}
					if (typeof(T) == typeof(int))
					{
						return (T)(object)(int)Math.Round(result);
					}
				}
				else
				{
					if (typeof(T) == typeof(double))
					{
						return (T)(object)0.0;
					}
					if (typeof(T) == typeof(int))
					{
						return (T)(object)0;
					}
				}
			}
			
			if (inputExpression.Length > 0 && inputExpression[0] == '=')
			{
				inputExpression = inputExpression.Substring(1);
			}

			// look through all the parents
			var sheet = Find(owner);
			if (sheet != null)
			{
				// try to manage the cell into the correct data type
				string value = sheet.SheetData.EvaluateExpression(inputExpression);
				return CastResult<T>(value, inputExpression);
			}

			// could not find a sheet, try to evaluate the expression directly
			var evaluator = new Expression(inputExpression.ToLower());
			return CastResult<T>(evaluator.calculate().ToString(), inputExpression);
		}

		/// <summary>
		/// Find the sheet that the given item will reference
		/// </summary>
		/// <param name="item">The item to start the search from</param>
		/// <returns></returns>
		public static SheetObject3D Find(IObject3D item)
		{
			// look through all the parents
			foreach (var parent in item.Parents())
			{
				// then each child of any give parent
				foreach (var sibling in parent.Children)
				{
					// if it is a sheet
					if (sibling != item
						&& sibling is SheetObject3D sheet)
					{
						return sheet;
					}
				}
			}

			return null;
		}

		/// <summary>
		/// Check if there are any references from the item to the sheet.
		/// </summary>
		/// <param name="itemToCheck">The item to validate editable properties on</param>
		/// <param name="sheetToCheck">The sheet to check if this object references</param>
		/// <returns></returns>
		public static bool NeedsRebuild(IObject3D itemToCheck, InvalidateArgs invalidateArgs)
		{
			if (!invalidateArgs.InvalidateType.HasFlag(InvalidateType.SheetUpdated))
			{
				return false;
			}

			if (invalidateArgs.Source is SheetObject3D sheet)
			{
				// Check if the sheet is the first sheet parent of this item (if not it will not change it's data).
				if (Find(itemToCheck) == sheet)
				{
					foreach (var item in itemToCheck.DescendantsAndSelf())
					{
						// Find all the OrReference properties on this item and check if any start with an '='.
						foreach (var property in PublicPropertyEditor.GetEditablePropreties(item))
						{
							var propertyValue = property.Value;

							if (propertyValue is IDirectOrExpression directOrExpression)
							{
								if (directOrExpression.Expression.StartsWith("="))
								{
									// WIP: check if the value has actually changed, this will update every object on any cell change
									return true;
								}
							}
						}
					}
				}
			}

			return false;
		}

		public static T CastResult<T>(string value, string inputExpression)
		{
			if (typeof(T) == typeof(string))
			{
				// if parsing the equation resulted in NaN as the output
				if (value == "NaN")
				{
					// return the actual expression
					return (T)(object)inputExpression;
				}

				// get the value of the cell
				return (T)(object)value;
			}

			if (typeof(T) == typeof(double))
			{
				if (double.TryParse(value, out double doubleValue)
					&& !double.IsNaN(doubleValue)
					&& !double.IsInfinity(doubleValue))
				{
					return (T)(object)doubleValue;
				}
				// else return an error
				return (T)(object).1;
			}

			if (typeof(T) == typeof(int))
			{
				if (double.TryParse(value, out double doubleValue)
					&& !double.IsNaN(doubleValue)
					&& !double.IsInfinity(doubleValue))
				{
					return (T)(object)(int)Math.Round(doubleValue);
				}
				// else return an error
				return (T)(object)1;
			}
		
			return (T)(object)default(T);
		}

		public void AddObject3DControls(Object3DControlsLayer object3DControlsLayer)
		{
		}
	}
}