/*
Copyright (c) 2019, John Lewin
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
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.DataConverters3D;

namespace MatterHackers.MatterControl.Library
{
	public class NodeOperation
	{
		/// <summary>
		/// Gets or sets the unlocalized string identifier for an operation
		/// </summary>
		public string OperationID { get; set; }

		public string Title { get; set; }

		public IEnumerable<Type> MappedTypes { get; set; }

		public Func<IObject3D, InteractiveScene, Task> Operation { get; set; }

		public Func<IObject3D, bool> IsEnabled { get; set; }

		public Func<IObject3D, bool> IsVisible { get; set; }

		public Func<bool, ImageBuffer> IconCollector { get; set; }

		public Type ResultType { get; set; }
	}

	public class GraphConfig
	{
		private ApplicationController applicationController;

		public Dictionary<string, NodeOperation> Operations { get; } = new Dictionary<string, NodeOperation>();

		public Dictionary<Type, List<NodeOperation>> PrimaryOperations { get; } = new Dictionary<Type, List<NodeOperation>>();

		public GraphConfig(ApplicationController applicationController)
		{
			this.applicationController = applicationController;
		}

		public NodeOperation RegisterOperation(NodeOperation nodeOperation)
		{
			var thumbnails = applicationController.Thumbnails;

			var resultType = nodeOperation.ResultType;

			if (!thumbnails.OperationIcons.ContainsKey(resultType))
			{
				thumbnails.OperationIcons.Add(resultType, nodeOperation.IconCollector);
			}

			this.Operations.Add(nodeOperation.OperationID, nodeOperation);

			return nodeOperation;
		}
	}
}
