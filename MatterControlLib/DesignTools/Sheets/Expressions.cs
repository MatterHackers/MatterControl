/*
Copyright (c) 2023, Lars Brubaker, John Lewin
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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.VectorMath;
using Sprache;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using static MatterHackers.MatterControl.DesignTools.SheetObject3D;

namespace MatterHackers.MatterControl.DesignTools
{
    public static class Expressions
    {
        public const BindingFlags OwnedPropertiesOnly = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        private static readonly Regex ConstantFinder = new Regex("(?<=\\[).+?(?=\\])", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Type[] ExpressionTypes =
        {
            typeof(StringOrExpression),
            typeof(DoubleOrExpression),
            typeof(IntOrExpression),
        };

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

        private static Random rand = new Random();

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

        public static T EvaluateExpression<T>(IObject3D owner, string inExpression)
        {
            var inputExpression = inExpression;

            inputExpression = ReplaceConstantsWithValues(owner, inputExpression);

            if (typeof(T) == typeof(Vector3))
            {
                // This can parse a Vector3 from a string like "[1,2,3]" or a DoubleOrExpression that has formula in it.
                return (T)(object)Vector3OrExpression.ParseVector(owner, inExpression);
            }
            
            // check if the expression is an equation (starts with "=")
            if (inputExpression.Length > 0 && inputExpression[0] == '=')
            {
                // If we are an object that has dynamic variables, but we are not a sheet.
                if (owner is IInternalStateResolver internalStateVariableResolver)
                {
                    inputExpression = internalStateVariableResolver.EvaluateExpression(inputExpression);
                }

                // look through all the parents
                var variableContainer = FindFirstVariableContainer(owner);
                if (variableContainer != null)
                {
                    // try to manage the cell into the correct data type
                    string value = variableContainer.EvaluateExpression(inputExpression);
                    return CastResult<T>(value, inputExpression);
                }

                // could not find a sheet, try to evaluate the expression directly
                var evaluator = new ExpressionParser(inputExpression.Substring(1).ToLower());
                if (evaluator.CheckSyntax())
                {
                    Debug.WriteLine(evaluator.GetErrorMessage());
                }

                return CastResult<T>(evaluator.Calculate().ToString(), inputExpression);
            }
            else // not an equation so try to parse it directly
            {
                if (typeof(T) == typeof(string))
                {
                    return (T)(object)inputExpression;
                }

                double.TryParse(inputExpression, out var result);

                if (typeof(T) == typeof(double))
                {
                    return (T)(object)result;
                }
                if (typeof(T) == typeof(int))
                {
                    return (T)(object)(int)Math.Round(result);
                }
                if (typeof(T) == typeof(bool))
                {
                    return (T)(object)(result != 0);
                }

                return (T)(object)0;
            }
        }

        public static IEnumerable<DirectOrExpression> GetActiveExpression(IObject3D item, string checkForString, bool startsWith)
        {
            foreach (var property in PropertyEditor.GetEditablePropreties(item))
            {
                var propertyValue = property.Value;

                if (propertyValue is DirectOrExpression directOrExpression)
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
                        if (directOrExpression.Expression.Contains(checkForString))
                        {
                            yield return directOrExpression;
                        }
                    }
                }
            }
        }

        public static IEnumerable<int> GetComponentExpressions(IComponentObject3D component, string checkForString, bool startsWith)
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

        public static IEnumerable<EditableProperty> GetExpressionPropreties(IObject3D item)
        {
            return item.GetType().GetProperties(OwnedPropertiesOnly)
                .Where(pi => ExpressionTypes.Contains(pi.PropertyType)
                    && pi.GetGetMethod() != null
                    && pi.GetSetMethod() != null)
                .Select(p => new EditableProperty(p, item));
        }

        public static bool HasExpressionWithString(IObject3D itemToCheck, string checkForString, bool startsWith)
        {
            foreach (var item in itemToCheck.DescendantsAndSelf())
            {
                if (GetActiveExpression(item, checkForString, startsWith).Any()
                    || itemToCheck is IComponentObject3D component
                        && GetComponentExpressions(component, checkForString, startsWith).Any())
                {
                    // three is one so return true
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Check if there are any references from the item to the sheet.
        /// </summary>
        /// <param name="itemToCheck">The item to validate editable properties on</param>
        /// <param name="sheetToCheck">The sheet to check if this object references</param>
        /// <returns></returns>
        public static bool NeedRebuild(IObject3D itemToCheck, InvalidateArgs invalidateArgs)
        {
            if (!invalidateArgs.InvalidateType.HasFlag(InvalidateType.SheetUpdated))
            {
                return false;
            }

            if (invalidateArgs.Source is SheetObject3D sheet)
            {
                // Check if the sheet is the first sheet parent of this item (if not it will not change it's data).
                if (FindFirstVariableContainer(itemToCheck) == sheet)
                {
                    return HasExpressionWithString(itemToCheck, "=", true);
                }
            }

            return false;
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

        public static List<UpdateItem> SortAndLockUpdateItems(IObject3D root, Func<IObject3D, bool> includeObject, bool checkForExpression)
        {
            var requiredUpdateItems = new Dictionary<IObject3D, UpdateItem>();
            foreach (var child in root.Descendants())
            {
                if (includeObject(child))
                {
                    var parent = child;
                    var depthToThis = 0;
                    while (parent.Parent != root)
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

        //private static bool HasValuesThatWillChange(IObject3D item)
        //      {
        //	// enumerate public properties on child
        //	foreach (var property in GetExpressionPropreties(item))
        //	{
        //		var propertyValue = property.Value;

        //		if (propertyValue is IDirectOrExpression expression)
        //		{
        //			// return the value
        //			var currentValue =  item.GetType().GetProperty(property.Name).GetValue(child, null).ToString();
        //			var newValue = EvaluateExpression<string>(item, propertyValue.ToString()).ToString();
        //			inExpression = inExpression.Replace("[" + constant + "]", value);
        //		}
        //	}
        //}

        public static bool ContainsExpression(IObject3D item)
        {
            // process depth first
            foreach (var child in item.Children)
            {
                if (ContainsExpression(child))
                {
                    return true;
                }
            }

            return HasExpressionWithString(item, "=", true);
        }

        private static void AddItemsRequiringUpdateToDictionary(IObject3D inItem,
            Dictionary<IObject3D, UpdateItem> updatedItems,
            int inDepth,
            Func<IObject3D, bool> includeObject,
            bool checkForExpression)
        {
            // process depth first
            foreach (var child in inItem.Children)
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

        /// <summary>
        /// Find the sheet that the given item will reference
        /// </summary>
        /// <param name="item">The item to start the search from</param>
        /// <returns></returns>
        private static IVariableResolver FindFirstVariableContainer(IObject3D item)
        {
            // look through all the parents
            foreach (var parent in item.Parents())
            {
                // then each child of any give parent
                foreach (var sibling in parent.Children)
                {
                    // if it is a sheet
                    if (sibling != item
                        && sibling is IVariableResolver variableContainer)
                    {
                        return variableContainer;
                    }
                }
            }

            return null;
        }

        private static ArrayObject3D FindParentArray(IObject3D item, int wantLevel)
        {
            int foundLevel = 0;
            // look through all the parents
            foreach (var parent in item.Parents())
            {
                // if it is a ArrayObject3D
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

        private static string GetDisplayName(PropertyInfo prop)
        {
            var nameAttribute = prop.GetCustomAttributes(true).OfType<DisplayNameAttribute>().FirstOrDefault();
            return nameAttribute?.DisplayName ?? prop.Name.SplitCamelCase();
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
    }
}