/*
Copyright (c) 2020, Kevin Pope, John Lewin, Lars Brubaker
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
using System.Text.RegularExpressions;

namespace MatterHackers.MatterControl.DesignTools
{
	public static class McxDocument
	{
		public class McxNode
		{
			public List<McxNode> Children { get; set; }

			public string Name { get; set; }

			public bool Visible { get; set; } = true;

			public string TypeName { get; set; }

			public string MeshPath { get; set; }

			private static Regex fileNameNumberMatch = new Regex("\\(\\d+\\)\\s*$", RegexOptions.Compiled);

			public IEnumerable<string> AllNames()
			{
				if (Children?.Count > 0)
				{
					foreach (var child in Children)
					{
						foreach (var name in child.AllNames())
						{
							yield return name;
						}
					}
				}
				else if (!string.IsNullOrWhiteSpace(Name))
				{
					if (Name.Contains("("))
					{
						yield return fileNameNumberMatch.Replace(Name, "").Trim();
					}
					else
					{
						yield return Name;
					}
				}
			}

			public IEnumerable<string> AllVisibleMeshFileNames()
			{
				if (!string.IsNullOrEmpty(MeshPath))
				{
					yield return MeshPath;
				}
				else if (Children?.Count > 0 && Visible)
				{
					foreach (var child in Children)
					{
						foreach (var meshPath in child.AllVisibleMeshFileNames())
						{
							yield return meshPath;
						}
					}
				}
			}
		}
	}
}