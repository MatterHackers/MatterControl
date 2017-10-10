/*
Copyright (c) 2017, Lars Brubaker, John Lewin

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
using System.Collections.Specialized;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	public class DropDownMenu : Menu
	{
		public event EventHandler SelectionChanged;

		private GuiWidget mainControlWidget;

		private List<NamedAction> menuActions = null;

		TextWidget textWidget = null;

		public DropDownMenu(string topMenuText, Direction direction = Direction.Down, double pointSize = 12)
			: base(direction)
		{
			textWidget = new TextWidget(topMenuText, pointSize: pointSize)
			{
				TextColor = this.TextColor
			};

			textWidget.AutoExpandBoundsToText = true;
			this.Name = topMenuText + " Menu";

			SetStates(textWidget);
		}

		public DropDownMenu(GuiWidget topMenuContent, Direction direction = Direction.Down, double pointSize = 12)
			: base(direction)
		{
			SetStates(topMenuContent);
		}

		public List<NamedAction> MenuActions
		{
			get => menuActions;
			set
			{
				menuActions = value;
				if (menuActions != null)
				{
					foreach (var action in menuActions)
					{
						this.AddItem(action.Title);
					}
				}
			}
		}

		public bool MenuAsWideAsItems { get; set; } = true;

		public bool DrawDirectionalArrow { get; set; } = true;

		public int BorderWidth { get; set; } = 1;

		public BorderDouble MenuItemsPadding { get; set; }

		public RGBA_Bytes BorderColor { get; set; }

		public RGBA_Bytes NormalArrowColor { get; set; }

		public RGBA_Bytes HoverArrowColor { get; set; }

		public RGBA_Bytes NormalColor { get; set; }

		public RGBA_Bytes HoverColor { get; set; }

		private RGBA_Bytes textColor = RGBA_Bytes.Black;
		public RGBA_Bytes TextColor
		{
			get { return textColor; }
			set
			{
				if (value != textColor)
				{
					textColor = value;
					
					if (textWidget != null)
					{
						textWidget.TextColor = textColor;
					}
				}
			}
		}

		private int selectedIndex = -1;

		public int SelectedIndex
		{
			get { return selectedIndex; }
			set
			{
				selectedIndex = value;
				OnSelectionChanged(null);
				Invalidate();
			}
		}

		public string SelectedValue => GetValue(SelectedIndex);

		public string GetValue(int itemIndex)
		{
			return MenuItems[SelectedIndex].Value;
		}

		private void SetStates(GuiWidget topMenuContent)
		{
			SetDisplayAttributes();

			MenuItems.CollectionChanged += new NotifyCollectionChangedEventHandler(MenuItems_CollectionChanged);
			mainControlWidget = topMenuContent;
			mainControlWidget.VAnchor = UI.VAnchor.Center;
			mainControlWidget.HAnchor = UI.HAnchor.Left;
			AddChild(mainControlWidget);
			HAnchor = HAnchor.Fit;
			VAnchor = VAnchor.Fit;

			//IE Don't show arrow unless color is set explicitly
			NormalArrowColor = new RGBA_Bytes(255, 255, 255, 0);
			HoverArrowColor = TextColor;
		}

		protected override void DropListItems_Closed(object sender, ClosedEventArgs e)
		{
			BackgroundColor = NormalColor;
			base.DropListItems_Closed(sender, e);
		}

		public override void OnMouseLeave(MouseEventArgs mouseEvent)
		{
			if (!this.IsOpen)
			{
				BackgroundColor = NormalColor;
			}

			base.OnMouseLeave(mouseEvent);
		}

		public override void OnMouseEnter(MouseEventArgs mouseEvent)
		{
			BackgroundColor = HoverColor;
			base.OnMouseEnter(mouseEvent);
		}

		private void OnSelectionChanged(EventArgs e)
		{
			if (MenuActions != null
				&& this.selectedIndex <= MenuActions.Count - 1)
			{
				MenuActions[this.selectedIndex].Action?.Invoke();
			}
			else
			{
				SelectionChanged?.Invoke(this, e);
			}
		}

		private void MenuItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			Vector2 minSize = new Vector2(LocalBounds.Width, LocalBounds.Height);
			foreach (MenuItem item in MenuItems)
			{
				minSize.x = Math.Max(minSize.x, item.Width);
			}

			string startText = mainControlWidget.Text;
			foreach (MenuItem item in MenuItems)
			{
				mainControlWidget.Text = item.Text;

				minSize.x = Math.Max(minSize.x, LocalBounds.Width);
				minSize.y = Math.Max(minSize.y, LocalBounds.Height);
			}
			mainControlWidget.Text = startText;

			if (MenuAsWideAsItems)
			{
				this.MinimumSize = minSize;
			}

			foreach (MenuItem item in e.NewItems)
			{
				item.MinimumSize = new Vector2(minSize.x, item.MinimumSize.y);
				// remove it if it is there so we don't have two. It is ok to remove a delegate that is not present.
				item.Selected -= item_Selected;
				item.Selected += item_Selected;
			}
		}

		private void item_Selected(object sender, EventArgs e)
		{
			int newSelectedIndex = 0;
			foreach (MenuItem item in MenuItems)
			{
				if (item == sender)
				{
					break;
				}
				newSelectedIndex++;
			}

			SelectedIndex = newSelectedIndex;
			BackgroundColor = NormalColor;
		}

		private void SetDisplayAttributes()
		{
			this.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			this.NormalColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			this.HoverColor = RGBA_Bytes.Gray;
			this.MenuItemsBorderWidth = 1;
			this.MenuItemsBackgroundColor = RGBA_Bytes.White;
			this.MenuItemsBorderColor = RGBA_Bytes.Gray;
			this.MenuItemsPadding = new BorderDouble(10, 10, 10, 10);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);
			this.DrawBorder(graphics2D);
			if (DrawDirectionalArrow)
			{
				this.DoDrawDirectionalArrow(graphics2D);
			}
		}

		protected virtual void DoDrawDirectionalArrow(Graphics2D graphics2D)
		{
			VertexStorage littleArrow = new VertexStorage();
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
			if (UnderMouseState != UI.UnderMouseState.NotUnderMouse)
			{
				graphics2D.Render(littleArrow, LocalBounds.Right - 10, LocalBounds.Bottom + Height - 4, NormalArrowColor);
			}
			else
			{
				graphics2D.Render(littleArrow, LocalBounds.Right - 10, LocalBounds.Bottom + Height - 4, HoverArrowColor);
			}
		}

		private void DrawBorder(Graphics2D graphics2D)
		{
			RectangleDouble Bounds = LocalBounds;
			if (BorderWidth > 0)
			{
				if (BorderWidth == 1)
				{
					graphics2D.Rectangle(Bounds, BorderColor);
				}
				else
				{
					RoundedRect borderRect = new RoundedRect(this.LocalBounds, 0);
					Stroke strokeRect = new Stroke(borderRect, BorderWidth);
					graphics2D.Render(strokeRect, BorderColor);
				}
			}
		}

		public MenuItem AddItem(string name, string value = null, double pointSize = 12)
		{
			if (value == null)
			{
				value = name;
			}

			if (mainControlWidget.Text != "")
			{
				mainControlWidget.Margin = MenuItemsPadding;
			}

			var menuItem = new MenuItem(
				new MenuItemColorStatesView(name)
				{
					NormalBackgroundColor = MenuItemsBackgroundColor,
					OverBackgroundColor = MenuItemsBackgroundHoverColor,

					NormalTextColor = MenuItemsTextColor,
					OverTextColor = MenuItemsTextHoverColor,
					DisabledTextColor = RGBA_Bytes.Gray,

					PointSize = pointSize,
					Padding = MenuItemsPadding,
				}, 
				value);

			menuItem.Text = name;
			menuItem.Name = name + " Menu Item";
			MenuItems.Add(menuItem);

			return menuItem;
		}
	}
}