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
using System.Threading;
using System.Threading.Tasks;
using MatterControl.Printing;
using MatterHackers.Agg;
using MatterHackers.DataConverters3D;
using MatterHackers.GCodeVisualizer;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public interface ISceneContext
	{
		Vector2 BedCenter { get; }

		string ContentType { get; }

		bool EditableScene { get; }

		EditContext EditContext { get; set; }

		InteractiveScene Scene { get; }

		SceneContextViewState ViewState { get; }

		WorldView World { get; }

		event EventHandler SceneLoaded;

		InsertionGroupObject3D AddToPlate(IEnumerable<ILibraryItem> itemsToAdd);

		InsertionGroupObject3D AddToPlate(IEnumerable<ILibraryItem> itemsToAdd, Vector2 initialPosition, bool moveToOpenPosition);

		List<BoolOption> GetBaseViewOptions();

		void AddToPlate(string[] filesToLoadIncludingZips);

		void ClearPlate();

		Task LoadContent(EditContext editContext);

		void LoadEmptyContent(EditContext editContext);

		Task LoadGCodeContent(Stream stream);

		Task LoadIntoCurrent(EditContext editContext);

		Task LoadLibraryContent(ILibraryItem libraryItem);

		Task LoadPlateFromHistory();

		Task SaveChanges(IProgress<ProgressStatus> progress, CancellationToken cancellationToken);

		// TODO: Isolate printer specifics from ISceneContext

		// *******************************************************
		// ****             Printer specific                  ****
		// *******************************************************
		event EventHandler ActiveLayerChanged;
		event EventHandler LoadedGCodeChanged;

		int ActiveLayerIndex { get; set; }

		GCodeRenderer GCodeRenderer { get; set; }

		GCodeFile LoadedGCode { get; }

		BedShape BedShape { get; }

		double BuildHeight { get; }

		Mesh BuildVolumeMesh { get; }
		Mesh Mesh { get; }
		PrinterConfig Printer { get; set; }
		Mesh PrinterShape { get; }
		View3DConfig RendererOptions { get; }

		GCodeRenderInfo RenderInfo { get; set; }

		void InvalidateBedMesh();

		void LoadGCode(Stream stream, CancellationToken cancellationToken, Action<double, string> progressReporter);
		void LoadActiveSceneGCode(string filePath, CancellationToken cancellationToken, Action<double, string> progressReporter);

		Task StashAndPrint(IEnumerable<ILibraryItem> selectedLibraryItems);

		Task StashAndPrintGCode(ILibraryItem libraryItem);

		Vector3 ViewerVolume { get; }
	}
}