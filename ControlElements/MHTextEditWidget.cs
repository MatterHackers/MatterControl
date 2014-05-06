using System;
using System.Diagnostics;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.OpenGlGui;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using MatterHackers.MatterControl.DataStorage;

namespace MatterHackers.MatterControl
{
    public class MHTextEditWidget : GuiWidget
    {
        Stopwatch timeSinceLastTextChanged = new Stopwatch();
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

            UiThread.RunOnIdle(OnIdle);
        }

        private void SetNoContentFieldDescriptionVisibility()
        {
            if(noContentFieldDescription != null)
            {
                if (Text == "" && !ContainsFocus)
                {
                    noContentFieldDescription.Visible = true;
                }
                else
                {
                    noContentFieldDescription.Visible = false;
                }
            }
        }

        void InternalTextEditWidget_EditComplete(object sender, EventArgs e)
        {
            timeSinceLastTextChanged.Stop();
        }

        public void OnIdle(object state)
        {
            if (timeSinceLastTextChanged.IsRunning && timeSinceLastTextChanged.Elapsed.Seconds > 2)
            {
                if (actuallTextEditWidget.InternalTextEditWidget.TextHasChanged())
                {
                    actuallTextEditWidget.InternalTextEditWidget.OnEditComplete();
                }
                timeSinceLastTextChanged.Stop();
            }

            if (!WidgetHasBeenClosed)
            {
                UiThread.RunOnIdle(OnIdle);
            }
        }

        void internalTextEditWidget_TextChanged(object sender, EventArgs e)
        {
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
    }

    public class MHPasswordTextEditWidget : MHTextEditWidget
    {
        TextEditWidget passwordCoverText;

        public MHPasswordTextEditWidget(string text = "", double x = 0, double y = 0, double pointSize = 12, double pixelWidth = 0, double pixelHeight = 0, bool multiLine = false, int tabIndex = 0, string messageWhenEmptyAndNotSelected = "")
            : base(text, x, y, pointSize, pixelWidth, pixelHeight, multiLine, tabIndex, messageWhenEmptyAndNotSelected)
        {
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

            RemoveChild(noContentFieldDescription);
            AddChild(noContentFieldDescription);
        }
    }

    public class MHNumberEdit : GuiWidget
    {
        Stopwatch timeSinceLastTextChanged = new Stopwatch();
        NumberEdit actuallNumberEdit;
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

            UiThread.RunOnIdle(OnIdle);
        }

        void InternalTextEditWidget_EditComplete(object sender, EventArgs e)
        {
            timeSinceLastTextChanged.Stop();
        }


        public void OnIdle(object state)
        {
            if (timeSinceLastTextChanged.IsRunning && timeSinceLastTextChanged.Elapsed.Seconds > 2)
            {
                actuallNumberEdit.InternalNumberEdit.OnEditComplete();
                timeSinceLastTextChanged.Stop();
            }

            if (!WidgetHasBeenClosed)
            {
                UiThread.RunOnIdle(OnIdle);
            }
        }

        void internalNumberEdit_TextChanged(object sender, EventArgs e)
        {
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
    }
}
