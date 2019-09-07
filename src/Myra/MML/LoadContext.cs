using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Myra.Utility;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace Myra.MML
{
	internal class LoadContext: BaseContext
	{
		public ConcurrentDictionary<string, string> LegacyNames = new ConcurrentDictionary<string, string>();
		public Func<Type, object> ObjectCreator = (type) => Activator.CreateInstance(type);
		public string Namespace;
		public Assembly Assembly = typeof(Widget).Assembly;

		public void Load(object obj, XElement el)
		{
			var type = obj.GetType();

			List<PropertyInfo> complexProperties, simpleProperties;
			ParseProperties(type, out complexProperties, out simpleProperties);

			foreach (var attr in el.Attributes())
			{
				var property = (from p in simpleProperties where p.Name == attr.Name select p).FirstOrDefault();

				if (property != null)
				{
					object value = null;

					if (property.PropertyType.IsEnum)
					{
						value = Enum.Parse(property.PropertyType, attr.Value);
					}
					else if (property.PropertyType == typeof(Color) ||
						property.PropertyType == typeof(Color?))
					{
						value = attr.Value.FromName();
					}
					else
					{
						var type2 = property.PropertyType;
						if (property.PropertyType.IsNullablePrimitive())
						{
							type2 = property.PropertyType.GetNullableType();
						}

						value = Convert.ChangeType(attr.Value, type2, CultureInfo.InvariantCulture);
					}
					property.SetValue(obj, value);
				}
			}

			foreach (var child in el.Elements())
			{
				// Find property
				var property = (from p in complexProperties where p.Name == child.Name select p).FirstOrDefault();
				if (property != null)
				{
					if (property.SetMethod == null)
					{
						// Readonly property
						var value = property.GetValue(obj);
						var asCollection = value as IList;
						if (asCollection != null)
						{
							foreach (var child2 in child.Elements())
							{
								var item = ObjectCreator(property.PropertyType.GenericTypeArguments[0]);
								Load(item, child2);
								asCollection.Add(item);
							}
						}
						else
						{
							Load(value, child);
						}
					}
					else
					{
						var value = ObjectCreator(property.PropertyType);
						Load(value, child);
						property.SetValue(obj, value);
					}
				}
				else
				{
					// Property not found
					// Should be widget class name then
					var widgetName = child.Name.ToString();
					string newName;
					if (LegacyNames.TryGetValue(widgetName, out newName))
					{
						widgetName = newName;
					}

					var itemType = Assembly.GetType(Namespace + "." + widgetName);
					if (itemType != null)
					{
						var item = (IItemWithId)ObjectCreator(itemType);
						Load(item, child);

						if (obj is ComboBox)
						{
							((ComboBox)obj).Items.Add((ListItem)item);
						}
						else
						if (obj is ListBox)
						{
							((ListBox)obj).Items.Add((ListItem)item);
						}
						else
						if (obj is TabControl)
						{
							((TabControl)obj).Items.Add((TabItem)item);
						}
						else
						if (obj is MenuItem)
						{
							((MenuItem)obj).Items.Add((IMenuItem)item);
						}
						else if (obj is Menu)
						{
							((Menu)obj).Items.Add((IMenuItem)item);
						}
						else if (obj is IContent)
						{
							((IContent)obj).Content = (Widget)item;
						}
						else if (obj is MultipleItemsContainer)
						{
							((MultipleItemsContainer)obj).Widgets.Add((Widget)item);
						}
						else if (obj is SplitPane)
						{
							((SplitPane)obj).Widgets.Add((Widget)item);
						}
						else if (obj is Project)
						{
							((Project)obj).Root = (Widget)item;
						}
					}
					else
					{
						throw new Exception(string.Format("Could not resolve tag '{0}'", widgetName));
					}
				}
			}
		}
	}
}
