﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Myra.Attributes;
using Myra.Graphics2D.UI.Styles;
using System.Xml.Serialization;

#if !XENKO
using Microsoft.Xna.Framework;
#else
using Xenko.Core.Mathematics;
#endif

namespace Myra.Graphics2D.UI
{
	public abstract class SplitPane : SingleItemContainer<Grid>
	{
		private readonly ObservableCollection<Widget> _widgets = new ObservableCollection<Widget>();
		private readonly List<ImageButton> _handles = new List<ImageButton>();
		private ImageButton _handleDown;
		private int? _mouseCoord;
		private int _handlesSize;

		[XmlIgnore]
		[HiddenInEditor]
		public abstract Orientation Orientation { get; }

		[HiddenInEditor]
		[Content]
		public ObservableCollection<Widget> Widgets
		{
			get { return _widgets; }
		}

		[XmlIgnore]
		[HiddenInEditor]
		public ButtonStyle HandleStyle { get; private set; }

		public event EventHandler ProportionsChanged;

		[DefaultValue(HorizontalAlignment.Stretch)]
		public override HorizontalAlignment HorizontalAlignment
		{
			get { return base.HorizontalAlignment; }
			set { base.HorizontalAlignment = value; }
		}

		[DefaultValue(VerticalAlignment.Stretch)]
		public override VerticalAlignment VerticalAlignment
		{
			get { return base.VerticalAlignment; }
			set { base.VerticalAlignment = value; }
		}

		protected SplitPane(SplitPaneStyle style)
		{
			HorizontalAlignment = HorizontalAlignment.Stretch;
			VerticalAlignment = VerticalAlignment.Stretch;

			InternalChild = new Grid();

			_widgets.CollectionChanged += WidgetsOnCollectionChanged;

			if (style != null)
			{
				ApplySplitPaneStyle(style);
			}
		}

		public float GetProportion(int widgetIndex)
		{
			if (widgetIndex < 0 || widgetIndex >= Widgets.Count)
			{
				return 0.0f;
			}

			var result = Orientation == Orientation.Horizontal
				? InternalChild.ColumnsProportions[widgetIndex*2].Value
				: InternalChild.RowsProportions[widgetIndex*2].Value;

			return result;
		}

		public override void OnMouseMoved()
		{
			base.OnMouseMoved();

			if (_mouseCoord == null)
			{
				return;
			}

			var bounds = Bounds;
			if (bounds.Width == 0)
			{
				return;
			}

			var handleIndex = InternalChild.Widgets.IndexOf(_handleDown);
			Grid.Proportion firstProportion, secondProportion;
			float fp;

			var position = Desktop.MousePosition;
			if (Orientation == Orientation.Horizontal)
			{
				var firstWidth = position.X - bounds.X - _mouseCoord.Value;

				for (var i = 0; i < handleIndex - 1; ++i)
				{
					firstWidth -= InternalChild.GetColumnWidth(i);
				}

				fp = (float) Widgets.Count*firstWidth/(bounds.Width - _handlesSize);

				firstProportion = InternalChild.ColumnsProportions[handleIndex - 1];
				secondProportion = InternalChild.ColumnsProportions[handleIndex + 1];
			}
			else
			{
				var firstHeight = position.Y - bounds.Y - _mouseCoord.Value;

				for (var i = 0; i < handleIndex - 1; ++i)
				{
					firstHeight -= InternalChild.GetRowHeight(i);
				}

				fp = (float) Widgets.Count*firstHeight/(bounds.Height - _handlesSize);

				firstProportion = InternalChild.RowsProportions[handleIndex - 1];
				secondProportion = InternalChild.RowsProportions[handleIndex + 1];
			}

			if (fp >= 0 && fp <= 2.0f)
			{
				var fp2 = firstProportion.Value + secondProportion.Value - fp;
				firstProportion.Value = fp;
				secondProportion.Value = fp2;
				FireProportionsChanged();
			}
		}

		private void FireProportionsChanged()
		{
			var ev = ProportionsChanged;
			if (ev != null)
			{
				ev(this, EventArgs.Empty);
			}
		}

		private void HandleOnPressedChanged(object sender, EventArgs args)
		{
			var handle = (ImageButton)sender;

			if (!handle.IsPressed)
			{
				_handleDown = null;
				_mouseCoord = null;
			}
			else
			{
				_handleDown = (ImageButton)sender;
				_mouseCoord = Orientation == Orientation.Horizontal
					? Desktop.MousePosition.X - _handleDown.Bounds.X
					: Desktop.MousePosition.Y - _handleDown.Bounds.Y;
			}
		}

		private void WidgetsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
		{
			Reset();
		}

		private void GetProportions(int leftWidgetIndex,
			out Grid.Proportion leftProportion,
			out Grid.Proportion rightProportion)
		{
			var baseIndex = leftWidgetIndex*2;
			leftProportion = Orientation == Orientation.Horizontal
				? InternalChild.ColumnsProportions[baseIndex]
				: InternalChild.RowsProportions[baseIndex];
			rightProportion = Orientation == Orientation.Horizontal
				? InternalChild.ColumnsProportions[baseIndex + 2]
				: InternalChild.RowsProportions[baseIndex + 2];
		}

		public float GetSplitterPosition(int leftWidgetIndex)
		{
			Grid.Proportion leftProportion, rightProportion;
			GetProportions(leftWidgetIndex, out leftProportion, out rightProportion);

			var total = leftProportion.Value + rightProportion.Value;

			return leftProportion.Value/total;
		}

		public void SetSplitterPosition(int leftWidgetIndex, float proportion)
		{
			Grid.Proportion leftProportion, rightProportion;
			GetProportions(leftWidgetIndex, out leftProportion, out rightProportion);

			var total = leftProportion.Value + rightProportion.Value;

			var fp = proportion*total;
			var fp2 = total - fp;
			leftProportion.Value = fp;
			rightProportion.Value = fp2;
		}

		public void Reset()
		{
			// Clear
			InternalChild.Widgets.Clear();
			_handles.Clear();
			_handlesSize = 0;

			InternalChild.ColumnsProportions.Clear();
			InternalChild.RowsProportions.Clear();

			var i = 0;

			var handleSize = Orientation == Orientation.Horizontal
				? HandleStyle.Background.Size.X
				: HandleStyle.Background.Size.Y;

			foreach (var w in _widgets)
			{
				Grid.Proportion proportion;
				if (i > 0)
				{
					// Add splitter
					var handle = new ImageButton((ImageButtonStyle)null)
					{
						HorizontalAlignment = HorizontalAlignment.Stretch,
						VerticalAlignment = VerticalAlignment.Stretch,
						ReleaseOnMouseLeft = false
					};

					handle.ApplyButtonStyle(HandleStyle);

					handle.PressedChanged += HandleOnPressedChanged;

					proportion = new Grid.Proportion(Grid.ProportionType.Auto);

					if (Orientation == Orientation.Horizontal)
					{
						_handlesSize += handleSize;
						handle.GridColumn = i*2 - 1;
						InternalChild.ColumnsProportions.Add(proportion);
					}
					else
					{
						_handlesSize += handleSize;
						handle.GridRow = i*2 - 1;
						InternalChild.RowsProportions.Add(proportion);
					}

					InternalChild.Widgets.Add(handle);
					_handles.Add(handle);
				}

				proportion = i < _widgets.Count - 1
					? new Grid.Proportion(Grid.ProportionType.Part, 1.0f)
					: new Grid.Proportion(Grid.ProportionType.Fill, 1.0f);

				// Set grid coord and add widget itself
				if (Orientation == Orientation.Horizontal)
				{
					w.GridColumn = i*2;
					InternalChild.ColumnsProportions.Add(proportion);
				}
				else
				{
					w.GridRow = i*2;
					InternalChild.RowsProportions.Add(proportion);
				}

				InternalChild.Widgets.Add(w);

				++i;
			}

			foreach (var h in _handles)
			{
				if (Orientation == Orientation.Horizontal)
				{
					h.Width = handleSize;
					h.Height = null;
				}
				else
				{
					h.Width = null;
					h.Height = handleSize;
				}
			}

			FireProportionsChanged();
		}

		public void ApplySplitPaneStyle(SplitPaneStyle style)
		{
			ApplyWidgetStyle(style);

			HandleStyle = style.HandleStyle;
			Reset();
		}
	}
}