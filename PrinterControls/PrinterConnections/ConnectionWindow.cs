using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;
using System.Diagnostics;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.OpenGlGui;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
    public class ConnectionWindow : SystemWindow
    {
        Printer activePrinter;
        bool editMode = false;

        public ConnectionWindow()
            : base(350, 600)
        {                     
			string connectToPrinterTitle = new LocalizedString("MatterControl").Translated;
			string connectToPrinterTitleEnd = new LocalizedString ("Connect to Printer").Translated;
			Title = string.Format("{0} - {1}",connectToPrinterTitle,connectToPrinterTitleEnd);      
			      
            if (GetPrinterRecordCount() > 0)
            {
                ChangeToChoosePrinter();
            }
            else
            {
                ChangeToAddPrinter();
            }

            this.ShowAsSystemWindow();
            MinimumSize = new Vector2(350, 400);
            
        }

        public override void OnMouseUp(MouseEventArgs mouseEvent)
        {
            base.OnMouseUp(mouseEvent);
        }

        private void DoNotChangeWindow()
        {
            //Empty function used as default callback for changeToWindowCallback
        }

        public void ChangeToAddPrinter()
        {
            this.activePrinter = null;
            UiThread.RunOnIdle(DoChangeToAddPrinter);
        }

		private void DoChangeToAddPrinter(object state)
        {
            GuiWidget addConnectionWidget = new SetupStepMakeModelName(this, this);
            this.RemoveAllChildren();
            this.AddChild(addConnectionWidget);
            this.Invalidate();
        }

        public void ChangedToEditPrinter(Printer activePrinter)
        {
            this.activePrinter = activePrinter;
            UiThread.RunOnIdle(DoChangeToEditPrinter);
        }

        public void ChangeToChoosePrinter(bool editMode = false)
        {
            this.editMode = editMode;
			UiThread.RunOnIdle(DoChangeToChoosePrinter);
        }

		public void DoChangeToChoosePrinter(object state)
        {
            GuiWidget chooseConnectionWidget = new ChooseConnectionWidget(this, this, this.editMode);
            this.RemoveAllChildren();
            this.AddChild(chooseConnectionWidget);
            this.Invalidate();
        }

		private void DoChangeToEditPrinter(object state)
        {
            GuiWidget addConnectionWidget = new EditConnectionWidget(this, this, activePrinter);
            this.RemoveAllChildren();
            this.AddChild(addConnectionWidget);
            this.Invalidate();
            
        }

        int GetPrinterRecordCount()
        {
            return Datastore.Instance.RecordCount("Printer");
        }
    }
}
