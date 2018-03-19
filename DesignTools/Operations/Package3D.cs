using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.VectorMath;
using MatterHackers.PolygonMesh;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	public class Package3D : Object3D, IRebuildable
	{
		public string NameToWrite { get; set; }
		public Package3D()
		{

		}

		public static Package3D Create(IObject3D itemToPackage)
		{
			Package3D package = new Package3D();

			var text = itemToPackage.Descendants<TextObject3D>().FirstOrDefault();
			if (text != null)
			{
				package.NameToWrite = text.NameToWrite;
			}

			package.Children.Add(itemToPackage);

			return package;
		}

		public void Rebuild(UndoBuffer undoBuffer)
		{
			var text = this.Descendants<TextObject3D>().FirstOrDefault();
			if (text != null)
			{
				text.NameToWrite = this.NameToWrite;
				text.Rebuild(null);
			}

			var fit = this.Descendants<FitToBounds3D>().FirstOrDefault();
			if (fit != null)
			{
				fit.Rebuild(null);
			}

			var align = this.Descendants<Align3D>().FirstOrDefault();
			if (align != null)
			{
				align.Rebuild(null);
			}

			return;
		}
	}
}
