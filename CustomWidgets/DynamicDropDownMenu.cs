using System;
using MatterHackers.MatterControl;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg.UI
{
	public class DynamicDropDownMenu:DropDownMenu
	{
		private TupleList<string, Func<bool>> menuItems;
		bool hasText;

		public DynamicDropDownMenu (string topMenuText, GuiWidget buttonView, Direction direction = Direction.Down, double pointSize = 12)
			:base(topMenuText,buttonView,direction,pointSize)
		{
			menuItems = new TupleList<string, Func<bool>> ();
			hasText = topMenuText != "";
			TextColor = RGBA_Bytes.Black;
			NormalArrowColor = RGBA_Bytes.Black;
			HoverArrowColor = RGBA_Bytes.Black;

			BorderWidth = 1;
			BorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);


			this.SelectionChanged += new EventHandler (AltChoices_SelectionChanged);
		}

		public DynamicDropDownMenu (string topMenuText, Direction direction = Direction.Down, double pointSize = 12)
			:base(topMenuText,direction,pointSize)
		{
			menuItems = new TupleList<string, Func<bool>> ();

			NormalColor = RGBA_Bytes.White;
			TextColor = RGBA_Bytes.Black;
			NormalArrowColor = RGBA_Bytes.Black;
			HoverArrowColor = RGBA_Bytes.Black;
			HoverColor = new RGBA_Bytes (255, 255, 255, 200);
			BorderWidth = 1;
			BorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);

			this.SelectionChanged += new EventHandler (AltChoices_SelectionChanged);
		}

		public override void OnDraw (Graphics2D graphics2D)
		{

			base.OnDraw (graphics2D);
		}

		protected override void DrawDirectionalArrow(Graphics2D graphics2D)
		{
			PathStorage littleArrow = new PathStorage();
			if (this.MenuDirection == Direction.Down)
			{
				littleArrow.MoveTo(-4, 0);
				littleArrow.LineTo(4, 0);
				littleArrow.LineTo(0, -5);
			}
			else if (this.MenuDirection == Direction.Up)
			{
				littleArrow.MoveTo(-4, -5);
				littleArrow.LineTo(4, -5);
				littleArrow.LineTo(0, 0);
			}
			else
			{
				throw new NotImplementedException("Pulldown direction has not been implemented");
			}

			if(!hasText)
			{
				if (UnderMouseState != UI.UnderMouseState.NotUnderMouse)
				{
					graphics2D.Render(littleArrow, LocalBounds.Right/2, LocalBounds.Bottom + Height/2 + 4, NormalArrowColor);
				}
				else
				{
					graphics2D.Render(littleArrow, LocalBounds.Right/2, LocalBounds.Bottom + Height/2  + 4, HoverArrowColor);
				}
			}
			else
			{
				base.DrawDirectionalArrow (graphics2D);
			}

		}

		public void addItem(string name, Func<bool> clickFunction) 
		{
			this.AddItem (name);
			menuItems.Add (name, clickFunction);
		}

		private void AltChoices_SelectionChanged(object sender, EventArgs e)
		{
			menuItems [((DropDownMenu)sender).SelectedIndex].Item2 ();
		}
	}
}

