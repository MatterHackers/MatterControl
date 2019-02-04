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
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.DataConverters3D;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	public class ChildVisabilityObject3D : SelectedChildContainer
	{
		private SelectedChildren _childToSetVisabilityOn = new SelectedChildren();

		public ChildVisabilityObject3D()
		{
			Name = "Set Visability";
		}

		[ShowAsList]
		[DisplayName("Child")]
		public override SelectedChildren SelectedChild
		{
			get
			{
				if (Children.Count > 0)
				{
					if (_childToSetVisabilityOn.Count != 1)
					{
						_childToSetVisabilityOn.Clear();
						_childToSetVisabilityOn.Add(Children.First().ID);
					}

					if (!this.Children.Any(c => c.ID == _childToSetVisabilityOn[0]))
					{
						// we don't have an id of any of our current children
						_childToSetVisabilityOn.Clear();
						_childToSetVisabilityOn.Add(Children.First().ID);
					}
				}
				else
				{
					_childToSetVisabilityOn.Clear();
				}

				return _childToSetVisabilityOn;
			}
			set => _childToSetVisabilityOn = value;
		}

		[DisplayName("Visible")]
		public bool ChildVisibility { get; set; } = true;

		public override async void OnInvalidate(InvalidateArgs invalidateType)
		{
			if (invalidateType.InvalidateType.HasFlag(InvalidateType.Children)
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
				// and also always pass back the actual type
				base.OnInvalidate(invalidateType);
			}
		}

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");

			using (this.RebuildLock())
			{
				var childToSetVisabilityOn = this.Children.Where(c => c.ID == SelectedChild[0]).FirstOrDefault();
				childToSetVisabilityOn.Visible = ChildVisibility;
			}

			Invalidate(InvalidateType.Children);
			return Task.CompletedTask;
		}
	}
}