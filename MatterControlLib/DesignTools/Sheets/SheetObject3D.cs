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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.PolygonMesh;
using org.mariuszgromada.math.mxparser;

namespace MatterHackers.MatterControl.DesignTools
{
    [HideChildrenFromTreeView]
	[HideMeterialAndColor]
	[WebPageLink("Documentation", "Open", "https://www.matterhackers.com/support/mattercontrol-variable-support")]
	[MarkDownDescription("[BETA] - Experimental support for variables and equations with a sheets like interface.")]
	public class SheetObject3D : Object3D, IStaticThumbnail
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

		public string ThumbnailName => "Sheet";


		private static object loadLock = new object();
		private static IObject3D sheetObject;

		public override Mesh Mesh
		{
			get
			{
				if (this.Children.Count == 0)
				{
					lock (loadLock)
					{
						if (sheetObject == null)
						{
							sheetObject = MeshContentProvider.LoadMCX(StaticData.Instance.OpenStream(Path.Combine("Stls", "sheet_icon.mcx")));
						}

						this.Children.Modify((list) =>
						{
							list.Clear();

							list.Add(sheetObject.Clone());
						});
					}
				}

				return null;
			}

			set => base.Mesh = value; 
		}

		public SheetObject3D()
		{
		}

		public override bool Printable => false;

		public class UpdateItem
		{
			internal int depth;
			internal IObject3D item;
			internal RebuildLock rebuildLock;

			public override string ToString()
			{
				var state = rebuildLock == null ? "unlocked" : "locked";
				return $"{depth} {state} - {item}";
			}
		}

		public static List<UpdateItem> SortAndLockUpdateItems(IObject3D root, Func<IObject3D, bool> includeObject, bool checkForExpression)
		{
			var requiredUpdateItems = new Dictionary<IObject3D, UpdateItem>();
			foreach (var child in root.Descendants())
			{
				if (includeObject(child))
				{
					var parent = child;
					var depthToThis = 0;
					while(parent.Parent != root)
					{
						depthToThis++;
						parent = parent.Parent;
					}
                    
					AddItemsRequiringUpdateToDictionary(child, requiredUpdateItems, depthToThis, includeObject, checkForExpression);
				}
			}

			var updateItems = requiredUpdateItems.Values.ToList();
			// sort them
			updateItems.Sort((a, b) => a.depth.CompareTo(b.depth));

			// lock everything
			foreach (var depthItem in updateItems)
			{
				depthItem.rebuildLock = depthItem.item.RebuildLock();
			}

			return updateItems;
		}

		private void SendInvalidateToAll(object s, EventArgs e)
		{
			var updateItems = SortAndLockUpdateItems(this.Parent, (item) =>
			{
				if (item == this || item.Parent == this)
				{
					// don't process this
					return false;
				}
				else if (item.Parent is ArrayObject3D arrayObject3D
					&& arrayObject3D.SourceContainer != item)
				{
					// don't process the copied children of an array object
					return false;
				}

				return true;
			}, true);

			SendInvalidateInRebuildOrder(updateItems, InvalidateType.SheetUpdated, this);
		}

		public static RunningInterval SendInvalidateInRebuildOrder(List<UpdateItem> updateItems,
			InvalidateType invalidateType,
			IObject3D sender = null)
		{
			// and send the invalidate
			RunningInterval runningInterval = null;
			void RebuildWhenUnlocked()
			{
				var count = updateItems.Count;
				if (count > 0)
				{
					// get the last item from the list
					var lastIndex = count - 1;
					var lastUpdateItem = updateItems[lastIndex];
					// we start with everything locked, so unlock the last layer and tell it to rebuild
					if (lastUpdateItem.rebuildLock != null)
					{
						// release the lock and rebuild
						// and ask it to update
						var depthToBuild = lastUpdateItem.depth;
						for (int i = 0; i < updateItems.Count; i++)
						{
							var updateItem = updateItems[i];
							if (updateItem.depth == lastUpdateItem.depth)
							{
								updateItem.rebuildLock.Dispose();
								updateItem.rebuildLock = null;
								var updateSender = sender == null ? updateItem.item : sender;
								updateItem.item.Invalidate(new InvalidateArgs(updateSender, invalidateType));
							}
						}
					}
					else if (updateItems.Where(i =>
						{
							return i.depth == lastUpdateItem.depth && i.item.RebuildLocked;
						}).Any())
					{
						// wait for the current rebuild to end (the one we requested above)
						return;
					}
					else
					{
						// now that all the items at this level have rebuilt, remove them from out tracking
						for (int i = updateItems.Count - 1; i >= 0; i--)
						{
							if (updateItems[i].depth == lastUpdateItem.depth)
							{
								updateItems.RemoveAt(i);
							}
						}
					}
				}
				else
				{
					UiThread.ClearInterval(runningInterval);
				}
			}

			// rebuild depth first
			runningInterval = UiThread.SetInterval(RebuildWhenUnlocked, .01);

			return runningInterval;
		}

		private static void AddItemsRequiringUpdateToDictionary(IObject3D inItem,
			Dictionary<IObject3D, UpdateItem> updatedItems,
			int inDepth,
			Func<IObject3D, bool> includeObject,
			bool checkForExpression)
		{
			// process depth first
			foreach(var child in inItem.Children)
			{
				AddItemsRequiringUpdateToDictionary(child, updatedItems, inDepth + 1, includeObject, checkForExpression);
			}

			var depth2 = inDepth;
			if (includeObject(inItem)
				&& (!checkForExpression || HasExpressionWithString(inItem, "=", true)))
			{
				var itemToAdd = inItem;
				while (itemToAdd != null
					&& depth2 >= 0)
				{
					updatedItems[itemToAdd] = new UpdateItem()
					{
						depth = depth2,
						item = itemToAdd
					};
					depth2--;
					itemToAdd = itemToAdd?.Parent;
				}
			}
		}

		private static readonly Regex ConstantFinder = new Regex("(?<=\\[).+?(?=\\])", RegexOptions.CultureInvariant | RegexOptions.Compiled);
		private static Random rand = new Random();

		private static Dictionary<string, Func<IObject3D, double>> constants = new Dictionary<string, Func<IObject3D, double>>()
		{
			// length
			["cm"] = (owner) => 10,
			["m"] = (owner) => 1000,
			["inch"] = (owner) => 25.4,
			["ft"] = (owner) => 304.8,
			// math constant
			["pi"] = (owner) => Math.PI,
			["tau"] = (owner) => Math.PI * 2,
			["e"] = (owner) => Math.E,
			// functions
			["rand"] = (owner) => rand.NextDouble(),
			// array function
			["index"] = (owner) => RetrieveArrayIndex(owner, 0),
			["index0"] = (owner) => RetrieveArrayIndex(owner, 0),
			["index1"] = (owner) => RetrieveArrayIndex(owner, 1),
			["index2"] = (owner) => RetrieveArrayIndex(owner, 2),
		};

		private static ArrayObject3D FindParentArray(IObject3D item, int wantLevel)
		{
			int foundLevel = 0;
			// look through all the parents
			foreach (var parent in item.Parents())
			{
				// if it is a sheet
				if (parent is ArrayObject3D arrayObject)
				{
					if (foundLevel == wantLevel)
					{
						return arrayObject;
					}

					foundLevel++;
				}
			}

			return null;
		}

		public static int RetrieveArrayIndex(IObject3D item, int level)
		{
			var arrayObject = FindParentArray(item, level);

			if (arrayObject != null)
			{
				int index = 0;
				foreach (var child in arrayObject.Children)
				{
					if (!(child is OperationSourceObject3D))
					{
						if (child.DescendantsAndSelf().Where(i => i == item).Any())
						{
							return index;
						}

						index++;
					}
				}
			}

			return 0;
		}

		private static string ReplaceConstantsWithValues(IObject3D owner, string stringWithConstants)
		{
			string Replace(string inputString, string setting)
			{
				if (constants.ContainsKey(setting))
				{
					var value = constants[setting];

					// braces then brackets replacement
					inputString = inputString.Replace("[" + setting + "]", value(owner).ToString());
				}

				return inputString;
			}

			MatchCollection matches = ConstantFinder.Matches(stringWithConstants);

			for (int i = 0; i < matches.Count; i++)
			{
				var replacementTerm = matches[i].Value;
				stringWithConstants = Replace(stringWithConstants, replacementTerm);
			}

			return stringWithConstants;
		}

		private static string GetDisplayName(PropertyInfo prop)
		{
			var nameAttribute = prop.GetCustomAttributes(true).OfType<DisplayNameAttribute>().FirstOrDefault();
			return nameAttribute?.DisplayName ?? prop.Name.SplitCamelCase();
		}

		private static string SearchSiblingProperties(IObject3D owner, string inExpression)
        {
			var parent = owner.Parent;
			if (parent != null)
            {
				var matches = ConstantFinder.Matches(inExpression);

				for (int i = 0; i < matches.Count; i++)
				{
					var constant = matches[i].Value;
					// split inExpression on .
					var splitExpression = constant.Split('.');
					if (splitExpression.Length == 2)
					{
						foreach (var child in parent.Children)
						{
							// skip if owner
							if (child != owner)
							{
								var itemName = splitExpression[0];
								var propertyName = splitExpression[1];
								// if child has the same name as itemName
								if (child.Name == itemName)
								{
									// enumerate public properties on child
									foreach (var property in child.GetType().GetProperties())
									{
										var displayName = GetDisplayName(property);
										// if property name matches propertyName
										if (displayName == propertyName)
										{
											// return the value
											var expression = child.GetType().GetProperty(property.Name).GetValue(child, null).ToString();
											var value = SheetObject3D.EvaluateExpression<double>(child, expression).ToString();
											inExpression = inExpression.Replace("[" + constant + "]", value);
										}
									}
								}
							}
						}
					}
				}
            }
            
            return inExpression;
        }

		public static T EvaluateExpression<T>(IObject3D owner, string inExpression)
		{
			var inputExpression = inExpression;
			var printer = owner.ContainingPrinter();
			if (printer != null)
			{
				inputExpression = printer.Settings.ReplaceSettingsNamesWithValues(inputExpression, false);
			}

			inputExpression = SearchSiblingProperties(owner, inputExpression);

			inputExpression = ReplaceConstantsWithValues(owner, inputExpression);

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
			var sheet = FindFirstSheet(owner);
			if (sheet != null)
			{
				// try to manage the cell into the correct data type
				string value = sheet.SheetData.EvaluateExpression(inputExpression);
				return CastResult<T>(value, inputExpression);
			}

			// could not find a sheet, try to evaluate the expression directly
			var evaluator = new Expression(inputExpression.ToLower());
			if(evaluator.checkSyntax())
			{
				Debug.WriteLine(evaluator.getErrorMessage());
			}

			return CastResult<T>(evaluator.calculate().ToString(), inputExpression);
		}

		/// <summary>
		/// Find the sheet that the given item will reference
		/// </summary>
		/// <param name="item">The item to start the search from</param>
		/// <returns></returns>
		private static SheetObject3D FindFirstSheet(IObject3D item)
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
				if (FindFirstSheet(itemToCheck) == sheet)
				{
					return HasExpressionWithString(itemToCheck, "=", true);
				}
			}

			return false;
		}

		public static IEnumerable<IDirectOrExpression> GetActiveExpressions(IObject3D item, string checkForString, bool startsWith)
        {
			foreach (var property in PublicPropertyEditor.GetEditablePropreties(item))
			{
				var propertyValue = property.Value;

				if (propertyValue is IDirectOrExpression directOrExpression)
				{
					if (startsWith)
					{
						if (directOrExpression.Expression.StartsWith(checkForString))
						{
							// WIP: check if the value has actually changed, this will update every object on any cell change
							yield return directOrExpression;
						}
					}
					else
					{
						if(directOrExpression.Expression.Contains(checkForString))
                        {
							yield return directOrExpression;
						}
					}
				}
			}
		}

		public static IEnumerable<int> GetComponentExpressions(ComponentObject3D component, string checkForString, bool startsWith)
		{
			for (var i = 0; i < component.SurfacedEditors.Count; i++)
			{
				var (cellId, cellData) = component.DecodeContent(i);

				if (cellId != null)
				{
					if (startsWith)
					{
						if (cellData.StartsWith(checkForString))
						{
							// WIP: check if the value has actually changed, this will update every object on any cell change
							yield return i;
						}
					}
					else
					{
						if (cellData.Contains(checkForString))
						{
							yield return i;
						}
					}
				}
			}
		}

		public static bool HasExpressionWithString(IObject3D itemToCheck, string checkForString, bool startsWith)
		{
			foreach (var item in itemToCheck.DescendantsAndSelf())
			{
				if (GetActiveExpressions(item, checkForString, startsWith).Any()
					|| (itemToCheck is ComponentObject3D component
						&& GetComponentExpressions(component, checkForString, startsWith).Any()))
                {
					// three is one so return true
					return true;
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
	}
}