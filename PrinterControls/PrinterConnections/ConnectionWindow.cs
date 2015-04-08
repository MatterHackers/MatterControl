using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
	public class ConnectionWindow : SystemWindow
	{
		private Printer activePrinter;
		private bool editMode = false;

		public ConnectionWindow()
			: base(350 * TextWidget.GlobalPointSizeScaleRatio, 500 * TextWidget.GlobalPointSizeScaleRatio)
		{
			AlwaysOnTopOfMain = true;
			string connectToPrinterTitle = LocalizedString.Get("MatterControl");
			string connectToPrinterTitleEnd = LocalizedString.Get("Connect to Printer");
			Title = string.Format("{0} - {1}", connectToPrinterTitle, connectToPrinterTitleEnd);

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
			MinimumSize = new Vector2(350 * TextWidget.GlobalPointSizeScaleRatio, 400 * TextWidget.GlobalPointSizeScaleRatio);
		}

		private static ConnectionWindow connectionWindow = null;
		private static bool connectionWindowIsOpen = false;

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
			UiThread.RunOnIdle(DoChangeToChoosePrinter);
		}

		public void DoChangeToChoosePrinter(object state)
		{
			GuiWidget chooseConnectionWidget = new ChooseConnectionWidget(this, this, this.editMode);
			this.RemoveAllChildren();
			this.AddChild(chooseConnectionWidget);
			this.Invalidate();
		}

		private int GetPrinterRecordCount()
		{
			return Datastore.Instance.RecordCount("Printer");
		}
	}
}