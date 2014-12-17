using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            : base(350* TextWidget.GlobalPointSizeScaleRatio, 500* TextWidget.GlobalPointSizeScaleRatio)
        {
            AlwaysOnTopOfMain = true;
            string connectToPrinterTitle = LocalizedString.Get("MatterControl");
            string connectToPrinterTitleEnd = LocalizedString.Get("Connect to Printer");
			Title = string.Format("{0} - {1}",connectToPrinterTitle,connectToPrinterTitleEnd);      
			      
            if (GetPrinterRecordCount() > 0)
            {
                ChangeToChoosePrinter();
            }
            else
            {
                ChangeToAddPrinter();
            }

            BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

            this.ShowAsSystemWindow();
            MinimumSize = new Vector2(350* TextWidget.GlobalPointSizeScaleRatio, 400* TextWidget.GlobalPointSizeScaleRatio);
        }

        static ConnectionWindow connectionWindow = null;
        static bool connectionWindowIsOpen = false;
        public static void Show()
        {
            if (connectionWindowIsOpen == false)
            {
                connectionWindow = new ConnectionWindow();
                connectionWindowIsOpen = true;
                connectionWindow.Closed += (parentSender, e) =>
                {
                    connectionWindowIsOpen = false;
                    connectionWindow = null;
                };
            }
            else
            {
                if (connectionWindow != null)
                {
                    connectionWindow.BringToFront();
                }
            }
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

        public void ChangedToEditPrinter(Printer activePrinter, object state = null)
        {
            this.activePrinter = activePrinter;
            UiThread.RunOnIdle(DoChangeToEditPrinter, state);
        }

        private void DoChangeToEditPrinter(object state)
        {
            GuiWidget addConnectionWidget = new EditConnectionWidget(this, this, activePrinter, state);
            this.RemoveAllChildren();
            this.AddChild(addConnectionWidget);
            this.Invalidate();

        }

        public void ChangeToChoosePrinter(bool editMode = false)
        {
            this.editMode = editMode;
			//DoChangeToChoosePrinter(null);
			UiThread.RunOnIdle(DoChangeToChoosePrinter, null);
        }

		public void DoChangeToChoosePrinter(object state)
        {
            GuiWidget chooseConnectionWidget = new ChooseConnectionWidget(this, this, this.editMode);
            this.RemoveAllChildren();
            this.AddChild(chooseConnectionWidget);
            this.Invalidate();

        }

        int GetPrinterRecordCount()
        {
            return Datastore.Instance.RecordCount("Printer");
        }
    }
}
