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

using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	public class OperationSourceObject3D : Object3D
	{
		public OperationSourceObject3D()
		{
			Visible = false;
			Name = "Source".Localize();
		}

		public override bool Visible { get => base.Visible; set => base.Visible = value; }

		/// <summary>
		/// This function will return the source container and if it does not find one will:
		/// <para>find the first child of the parent widget</para>
		/// <para>remove it from parent</item>
		/// <para>create a new OperationSource</para>
		/// <para>add the first child to the OperationSource</para>
		/// <para>add the OperationSource to the parent</para>
		/// </summary>
		/// <param name="parent"></param>
		/// <returns>The existing or created OperationSource</returns>
		public static IObject3D GetOrCreateSourceContainer(IObject3D parent)
		{
			IObject3D sourceContainer;
			using (parent.RebuildLock())
			{
				sourceContainer = parent.Children.FirstOrDefault(c => c is OperationSourceObject3D);
				if (sourceContainer == null)
				{
					sourceContainer = new OperationSourceObject3D();

					// Move first child to sourceContainer
					var firstChild = parent.Children.First();
					parent.Children.Remove(firstChild);
					sourceContainer.Children.Add(firstChild);
				}
			}

			return sourceContainer;
		}

		/// <summary>
		/// Flatten the children of an object that has an OperationSource in it
		/// </summary>
		/// <param name="item"></param>
		public static void Flatten(IObject3D item)
		{
			using (item.RebuildLock())
			{
				// The idea is we leave everything but the source and that is the applied operation
				item.Children.Modify(list =>
				{
					var sourceItem = list.FirstOrDefault(c => c is OperationSourceObject3D);
					if (sourceItem != null)
					{
						list.Remove(sourceItem);
					}

					if (list.Count > 1)
					{
						// wrap the children in an object so they remain a group
						var group = new Object3D();
						group.Children.Modify((groupList) =>
						{
							groupList.AddRange(list);
						});

						list.Clear();
						list.Add(group);
					}
				});
			}
		}

		/// <summary>
		/// Prepare the children of an object that contains an OpperationSource to be removed
		/// </summary>
		/// <param name="parent"></param>
		internal static void Remove(IObject3D parent)
		{
			using (parent.RebuildLock())
			{
				parent.Children.Modify(list =>
				{
					var sourceItem = list.FirstOrDefault(c => c is OperationSourceObject3D);
					if (sourceItem != null)
					{
						IObject3D firstChild = sourceItem.Children.First();
						if (firstChild != null)
						{
							list.Clear();
							list.Add(firstChild);
						}
					}
				});
			}
		}
	}
}