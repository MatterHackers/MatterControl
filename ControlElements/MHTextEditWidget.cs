﻿using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using System;
using System.Diagnostics;

namespace MatterHackers.MatterControl
{
	public class MHTextEditWidget : GuiWidget
	{
		private Stopwatch timeSinceLastTextChanged = new Stopwatch();
		protected TextEditWidget actuallTextEditWidget;
		protected TextWidget noContentFieldDescription = null;

		public TextEditWidget ActualTextEditWidget
		{
			get { return actuallTextEditWidget; }
		}

		public MHTextEditWidget(string text = "", double x = 0, double y = 0, double pointSize = 12, double pixelWidth = 0, double pixelHeight = 0, bool multiLine = false, int tabIndex = 0, string messageWhenEmptyAndNotSelected = "")
		{
			Padding = new BorderDouble(3);
			actuallTextEditWidget = new TextEditWidget(text, x, y, pointSize, pixelWidth, pixelHeight, multiLine, tabIndex: tabIndex);
			actuallTextEditWidget.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
			actuallTextEditWidget.MinimumSize = new Vector2(Math.Max(actuallTextEditWidget.MinimumSize.x, pixelWidth), Math.Max(actuallTextEditWidget.MinimumSize.y, pixelHeight));
			actuallTextEditWidget.VAnchor = Agg.UI.VAnchor.ParentBottom;
			AddChild(actuallTextEditWidget);
			BackgroundColor = RGBA_Bytes.White;
			HAnchor = HAnchor.FitToChildren;
			VAnchor = VAnchor.FitToChildren;

			actuallTextEditWidget.TextChanged += new EventHandler(internalTextEditWidget_TextChanged);
			actuallTextEditWidget.InternalTextEditWidget.EditComplete += new EventHandler(InternalTextEditWidget_EditComplete);

			noContentFieldDescription = new TextWidget(messageWhenEmptyAndNotSelected, textColor: RGBA_Bytes.Gray);
			noContentFieldDescription.VAnchor = VAnchor.ParentBottom;
			noContentFieldDescription.AutoExpandBoundsToText = true;
			AddChild(noContentFieldDescription);
			SetNoContentFieldDescriptionVisibility();
		}

		private void SetNoContentFieldDescriptionVisibility()
		{
			if (noContentFieldDescription != null)
			{
				if (Text == "")
				{
					noContentFieldDescription.Visible = true;
				}
				else
				{
					noContentFieldDescription.Visible = false;
				}
			}
		}

		private void InternalTextEditWidget_EditComplete(object sender, EventArgs e)
		{
			timeSinceLastTextChanged.Stop();
		}

		public void OnIdle(object state)
		{
			if (timeSinceLastTextChanged.IsRunning)
			{
				if (timeSinceLastTextChanged.Elapsed.Seconds > 2)
				{
					if (actuallTextEditWidget.InternalTextEditWidget.TextHasChanged())
					{
						actuallTextEditWidget.InternalTextEditWidget.OnEditComplete(null);
					}
					timeSinceLastTextChanged.Stop();
				}
				if (!WidgetHasBeenClosed)
				{
					UiThread.RunOnIdle(OnIdle, 1);
				}
			}
		}

		private void internalTextEditWidget_TextChanged(object sender, EventArgs e)
		{
			if (!timeSinceLastTextChanged.IsRunning)
			{
				UiThread.RunOnIdle(OnIdle, 1);
			}
			timeSinceLastTextChanged.Restart();
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			SetNoContentFieldDescriptionVisibility();
			base.OnDraw(graphics2D);

			if (ContainsFocus)
			{
				graphics2D.Rectangle(LocalBounds, RGBA_Bytes.Orange);
			}
		}

		public override string Text
		{
			get
			{
				return actuallTextEditWidget.Text;
			}
			set
			{
				actuallTextEditWidget.Text = value;
			}
		}

		public override void Focus()
		{
			actuallTextEditWidget.Focus();
		}

		public bool SelectAllOnFocus
		{
			get { return actuallTextEditWidget.InternalTextEditWidget.SelectAllOnFocus; }
			set { actuallTextEditWidget.InternalTextEditWidget.SelectAllOnFocus = value; }
		}
	}

	public class MHPasswordTextEditWidget : MHTextEditWidget
	{
		private TextEditWidget passwordCoverText;

		public MHPasswordTextEditWidget(string text = "", double x = 0, double y = 0, double pointSize = 12, double pixelWidth = 0, double pixelHeight = 0, bool multiLine = false, int tabIndex = 0, string messageWhenEmptyAndNotSelected = "")
			: base(text, x, y, pointSize, pixelWidth, pixelHeight, multiLine, tabIndex, messageWhenEmptyAndNotSelected)
		{
			// remove this so that we can have other content first (the hiden letters)
			RemoveChild(noContentFieldDescription);

			passwordCoverText = new TextEditWidget(text, x, y, pointSize, pixelWidth, pixelHeight, multiLine);
			passwordCoverText.Selectable = false;
			passwordCoverText.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
			passwordCoverText.MinimumSize = new Vector2(Math.Max(passwordCoverText.MinimumSize.x, pixelWidth), Math.Max(passwordCoverText.MinimumSize.y, pixelHeight));
			passwordCoverText.VAnchor = Agg.UI.VAnchor.ParentBottom;
			AddChild(passwordCoverText);

			actuallTextEditWidget.TextChanged += (sender, e) =>
			{
				passwordCoverText.Text = new string('●', actuallTextEditWidget.Text.Length);
			};

			// put in back in after the hidden text
			noContentFieldDescription.ClearRemovedFlag();
			AddChild(noContentFieldDescription);
		}
	}

	public class MHNumberEdit : GuiWidget
	{
		private Stopwatch timeSinceLastTextChanged = new Stopwatch();
		private NumberEdit actuallNumberEdit;

		public NumberEdit ActuallNumberEdit
		{
			get { return actuallNumberEdit; }
		}

		public MHNumberEdit(double startingValue,
			double x = 0, double y = 0, double pointSize = 12,
			double pixelWidth = 0, double pixelHeight = 0,
			bool allowNegatives = false, bool allowDecimals = false,
			double minValue = int.MinValue,
			double maxValue = int.MaxValue,
			double increment = 1,
			int tabIndex = 0)
		{
			Padding = new BorderDouble(3);
			actuallNumberEdit = new NumberEdit(startingValue, x, y, pointSize, pixelWidth, pixelHeight,
				allowNegatives, allowDecimals, minValue, maxValue, increment, tabIndex);
			actuallNumberEdit.VAnchor = Agg.UI.VAnchor.ParentBottom;
			AddChild(actuallNumberEdit);
			BackgroundColor = RGBA_Bytes.White;
			HAnchor = HAnchor.FitToChildren;
			VAnchor = VAnchor.FitToChildren;

			actuallNumberEdit.TextChanged += new EventHandler(internalNumberEdit_TextChanged);
			actuallNumberEdit.InternalTextEditWidget.EditComplete += new EventHandler(InternalTextEditWidget_EditComplete);
		}

		public override int TabIndex
		{
			get
			{
				return base.TabIndex;
			}
			set
			{
				actuallNumberEdit.TabIndex = value;
			}
		}

		private void InternalTextEditWidget_EditComplete(object sender, EventArgs e)
		{
			timeSinceLastTextChanged.Stop();
		}

		public void OnIdle(object state)
		{
			if (timeSinceLastTextChanged.IsRunning)
			{
				if (timeSinceLastTextChanged.Elapsed.Seconds > 2)
				{
					if (actuallNumberEdit.InternalTextEditWidget.TextHasChanged())
					{
						actuallNumberEdit.InternalTextEditWidget.OnEditComplete(null);
					}
					timeSinceLastTextChanged.Stop();
				}
				if (!WidgetHasBeenClosed)
				{
					UiThread.RunOnIdle(OnIdle, 1);
				}
			}
		}

		private void internalNumberEdit_TextChanged(object sender, EventArgs e)
		{
			if (!timeSinceLastTextChanged.IsRunning)
			{
				UiThread.RunOnIdle(OnIdle, 1);
			}
			timeSinceLastTextChanged.Restart();
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);
			if (ContainsFocus)
			{
				graphics2D.Rectangle(LocalBounds, RGBA_Bytes.Orange);
			}
		}

		public override string Text
		{
			get
			{
				return actuallNumberEdit.Text;
			}
			set
			{
				actuallNumberEdit.Text = value;
			}
		}

		public bool SelectAllOnFocus
		{
			get { return ActuallNumberEdit.InternalNumberEdit.SelectAllOnFocus; }
			set { ActuallNumberEdit.InternalNumberEdit.SelectAllOnFocus = value; }
		}
	}
}