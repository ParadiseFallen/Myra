﻿using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Myra.Attributes;
using Myra.Graphics2D.UI.Styles;
using Myra.Utility;
using static Myra.Graphics2D.UI.Grid;
using System.Xml.Serialization;

#if !XENKO
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
#else
using Xenko.Core.Mathematics;
using Xenko.Input;
#endif

namespace Myra.Graphics2D.UI
{
	public abstract class Menu : SingleItemContainer<Grid>, IMenuItemsContainer
	{
		private ObservableCollection<IMenuItem> _items;

		[HiddenInEditor]
		[XmlIgnore]
		public abstract Orientation Orientation { get; }

		[HiddenInEditor]
		[XmlIgnore]
		public MenuItemStyle MenuItemStyle { get; private set; }

		[HiddenInEditor]
		[XmlIgnore]
		public SeparatorStyle SeparatorStyle { get; private set; }

		[HiddenInEditor]
		[XmlIgnore]
		internal MenuItemButton OpenMenuItem { get; private set; }

		[HiddenInEditor]
		[XmlIgnore]
		public bool IsOpen
		{
			get
			{
				return OpenMenuItem != null;
			}
		}

		[HiddenInEditor]
		[Content]
		public ObservableCollection<IMenuItem> Items
		{
			get { return _items; }

			internal set
			{
				if (_items == value)
				{
					return;
				}

				if (_items != null)
				{
					_items.CollectionChanged -= ItemsOnCollectionChanged;

					foreach (var menuItem in _items)
					{
						RemoveItem(menuItem);
					}
				}

				_items = value;

				if (_items != null)
				{
					_items.CollectionChanged += ItemsOnCollectionChanged;

					foreach (var menuItem in _items)
					{
						AddItem(menuItem);
					}
				}
			}
		}

		public override Desktop Desktop
		{
			get
			{
				return base.Desktop;
			}
			set
			{
				if (Desktop != null)
				{
					Desktop.ContextMenuClosing -= DesktopOnContextMenuClosing;
					Desktop.ContextMenuClosed -= DesktopOnContextMenuClosed;
				}

				base.Desktop = value;

				if (Desktop != null)
				{
					Desktop.ContextMenuClosing += DesktopOnContextMenuClosing;
					Desktop.ContextMenuClosed += DesktopOnContextMenuClosed;
				}
			}
		}


		[HiddenInEditor]
		[XmlIgnore]
		public int? HoverIndex
		{
			get
			{
				int? hoverIndex = null;

				for (var i = 0; i < InternalChild.Widgets.Count; ++i)
				{
					if (InternalChild.Widgets[i].IsMouseOver)
					{
						hoverIndex = i;
						break;
					}
				}

				return hoverIndex;
			}

			set
			{
				var oldIndex = HoverIndex;
				if (value == oldIndex)
				{
					return;
				}

				if (oldIndex != null)
				{
					var menuItemButton = InternalChild.Widgets[oldIndex.Value] as MenuItemButton;
					if (menuItemButton != null)
					{
						menuItemButton.IsMouseOver = false;
					}
				}

				if (value != null)
				{
					var menuItemButton = InternalChild.Widgets[value.Value] as MenuItemButton;
					if (menuItemButton != null)
					{
						menuItemButton.IsMouseOver = true;
					}
				}
			}
		}

		protected Menu(MenuStyle style)
		{
			InternalChild = new Grid();

			OpenMenuItem = null;

			HorizontalAlignment = HorizontalAlignment.Stretch;
			VerticalAlignment = VerticalAlignment.Stretch;

			if (style != null)
			{
				ApplyMenuStyle(style);
			}

			Items = new ObservableCollection<IMenuItem>();
		}

		private void ItemsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
		{
			if (args.Action == NotifyCollectionChangedAction.Add)
			{
				var index = args.NewStartingIndex;
				foreach (IMenuItem item in args.NewItems)
				{
					InsertItem(item, index);
					++index;
				}
			}
			else if (args.Action == NotifyCollectionChangedAction.Remove)
			{
				foreach (IMenuItem item in args.OldItems)
				{
					RemoveItem(item);
				}
			}
		}

		/// <summary>
		/// Recursively search for the menu item by id
		/// </summary>
		/// <param name="id"></param>
		/// <returns>null if not found</returns>
		public MenuItem FindMenuItemById(string id)
		{
			foreach (var item in _items)
			{
				var asMenuItem = item as MenuItem;
				if (asMenuItem == null)
				{
					continue;
				}

				var result = asMenuItem.FindMenuItemById(id);
				if (result != null)
				{
					return result;
				}
			}

			return null;
		}

		private void UpdateGridPositions()
		{
			for (var i = 0; i < InternalChild.Widgets.Count; ++i)
			{
				var widget = InternalChild.Widgets[i];
				if (Orientation == Orientation.Horizontal)
				{
					widget.GridColumn = i;
				}
				else
				{
					widget.GridRow = i;
				}
			}
		}

		private void AddItem(Widget menuItem, int index)
		{
			if (Orientation == Orientation.Horizontal)
			{
				InternalChild.ColumnsProportions.Add(new Proportion(ProportionType.Auto));
			}
			else
			{
				menuItem.HorizontalAlignment = HorizontalAlignment.Stretch;
				menuItem.VerticalAlignment = VerticalAlignment.Stretch;
				InternalChild.RowsProportions.Add(new Proportion(ProportionType.Auto));
			}

			InternalChild.Widgets.Insert(index, menuItem);

			UpdateGridPositions();
		}

		private void MenuItemOnChanged(object sender, EventArgs eventArgs)
		{
			var menuItem = (MenuItem)sender;

			var asMenuItemButton = menuItem.Widget as MenuItemButton;
			if (asMenuItemButton == null)
			{
				return;
			}

			asMenuItemButton.Text = menuItem.Text;
			asMenuItemButton.TextColor = menuItem.Color ?? MenuItemStyle.LabelStyle.TextColor;
		}

		private void InsertItem(IMenuItem iMenuItem, int index)
		{
			var menuItem = iMenuItem as MenuItem;
			Widget widget;
			if (menuItem != null)
			{
				menuItem.Changed += MenuItemOnChanged;
				var menuItemButton = new MenuItemButton(this, MenuItemStyle)
				{
					Text = menuItem.Text,
					Tag = menuItem
				};

				if (menuItem.Color.HasValue)
				{
					menuItemButton.TextColor = menuItem.Color.Value;
				}

				menuItemButton.MouseEntered += MouseOnEntered;
				menuItemButton.MouseLeft += MouseOnLeft;
				menuItemButton.SubMenu.Items = menuItem.Items;
				menuItemButton.Toggleable = menuItem.Items.Count > 0;

				if (menuItemButton.Toggleable)
				{
					menuItemButton.PressedChanged += ButtonOnPressedChanged;
				}
				else
				{
					menuItemButton.Click += ButtonOnClick;
				}

				widget = menuItemButton;
			}
			else
			{
				if (Orientation == Orientation.Horizontal)
				{
					widget = new VerticalSeparator(SeparatorStyle);
				} else
				{
					widget = new HorizontalSeparator(SeparatorStyle);
				}
			}

			iMenuItem.Menu = this;
			iMenuItem.Widget = widget;

			AddItem(widget, index);
			UpdateGridPositions();
		}

		private void AddItem(IMenuItem item)
		{
			InsertItem(item, InternalChild.Widgets.Count);
		}

		private void RemoveItem(IMenuItem iMenuItem)
		{
			var menuItem = iMenuItem as MenuItem;
			if (menuItem != null)
			{
				menuItem.Changed -= MenuItemOnChanged;
			}

			var widget = iMenuItem.Widget;
			if (widget == null)
			{
				return;
			}

			var asMenuItemButton = widget as MenuItemButton;
			if (asMenuItemButton != null)
			{
				asMenuItemButton.PressedChanged -= ButtonOnPressedChanged;
				asMenuItemButton.Click -= ButtonOnClick;
				asMenuItemButton.MouseEntered -= MouseOnEntered;
				asMenuItemButton.MouseLeft -= MouseOnLeft;
			}

			var index = InternalChild.Widgets.IndexOf(widget);
			if (Orientation == Orientation.Horizontal)
			{
				InternalChild.ColumnsProportions.RemoveAt(index);
			}
			else
			{
				InternalChild.RowsProportions.RemoveAt(index);
			}

			InternalChild.Widgets.RemoveAt(index);
			UpdateGridPositions();
		}

		public void Close()
		{
			Desktop.HideContextMenu();
		}

		private void ShowSubMenu(MenuItemButton menuItem)
		{
			if (OpenMenuItem != null)
			{
				OpenMenuItem.IsPressed = false;
			}

			if (menuItem == null || !menuItem.CanOpen)
			{
				return;
			}

			menuItem.SubMenu.HoverIndex = null;
			Desktop.ShowContextMenu(menuItem.SubMenu, new Point(menuItem.Bounds.X, Bounds.Bottom));
			Desktop.ContextMenuClosed += DesktopOnContextMenuClosed;

			OpenMenuItem = menuItem;
		}

		private void DesktopOnContextMenuClosing(object sender, ContextMenuClosingEventArgs args)
		{
			// Prevent closing/opening of the context menu
			if (OpenMenuItem != null && OpenMenuItem.Bounds.Contains(Desktop.MousePosition))
			{
				args.Cancel = true;
			}
		}

		private void DesktopOnContextMenuClosed(object sender, GenericEventArgs<Widget> genericEventArgs)
		{
			if (OpenMenuItem == null) return;

			OpenMenuItem.IsPressed = false;
			OpenMenuItem = null;
		}

		private void MouseOnEntered(object sender, EventArgs eventArgs)
		{
			if (!IsOpen)
			{
				return;
			}

			var menuItemButton = (MenuItemButton)sender;
			if (menuItemButton.CanOpen && OpenMenuItem != menuItemButton)
			{
				menuItemButton.IsPressed = true;
			}
		}

		private void MouseOnLeft(object sender, EventArgs eventArgs)
		{
		}

		private void ButtonOnPressedChanged(object sender, EventArgs eventArgs)
		{
			if (Desktop == null)
			{
				return;
			}

			var menuItemButton = (MenuItemButton)sender;

			if (menuItemButton.IsPressed)
			{
				if (menuItemButton.CanOpen)
				{
					ShowSubMenu(menuItemButton);
				}
			}
			else
			{
				Close();
			}
		}

		private void ButtonOnClick(object sender, EventArgs eventArgs)
		{
			if (Desktop == null)
			{
				return;
			}

			Close();

			var menuItem = (MenuItemButton)sender;
			if (!menuItem.CanOpen)
			{
				((MenuItem)menuItem.Tag).FireSelected();
			}
		}

		private void Click(MenuItemButton menuItemButton)
		{
			if (Desktop == null || !menuItemButton.Enabled)
			{
				return;
			}

			var menuItem = (MenuItem)menuItemButton.Tag;
			if (!menuItemButton.CanOpen)
			{
				Close();
				menuItem.FireSelected();
			}
			else
			{
				menuItemButton.IsMouseOver = true;
				menuItemButton.IsPressed = true;
			}
		}

		public override void OnKeyDown(Keys k)
		{
			if (k == Keys.Enter || k == Keys.Space)
			{
				int? selectedIndex = HoverIndex;
				if (selectedIndex != null)
				{
					var button = InternalChild.Widgets[selectedIndex.Value] as MenuItemButton;
					if (button != null && !button.CanOpen)
					{
						Click(button);
						return;
					}
				}
			}

			if (!MyraEnvironment.ShowUnderscores)
			{
				return;
			}

			var ch = k.ToChar(false);
			foreach (var w in InternalChild.Widgets)
			{
				var button = w as MenuItemButton;
				if (button == null)
				{
					continue;
				}

				if (ch != null && button.UnderscoreChar == ch)
				{
					Click(button);
					return;
				}
			}

			if (OpenMenuItem != null)
			{
				OpenMenuItem.SubMenu.OnKeyDown(k);
			}
		}

		public void MoveHover(int delta)
		{
			if (InternalChild.Widgets.Count == 0)
			{
				return;
			}

			// First step - determine index of currently selected item
			var si = HoverIndex;
			var hoverIndex = si != null?si.Value:-1;
			var oldHover = hoverIndex;

			var iterations = 0;
			while (true)
			{
				if (iterations > InternalChild.Widgets.Count)
				{
					return;
				}

				hoverIndex += delta;

				if (hoverIndex < 0)
				{
					hoverIndex = InternalChild.Widgets.Count - 1;
				}

				if (hoverIndex >= InternalChild.Widgets.Count)
				{
					hoverIndex = 0;
				}

				if (InternalChild.Widgets[hoverIndex] is MenuItemButton)
				{
					break;
				}


				++iterations;
			}

			HoverIndex = hoverIndex;
		}
	
		public void ApplyMenuStyle(MenuStyle style)
		{
			ApplyWidgetStyle(style);

			MenuItemStyle = style.MenuItemStyle;
			SeparatorStyle = style.SeparatorStyle;
		}
	}
}