﻿using System.Collections;
using System.Reflection;
using Myra.Attributes;
using Myra.Graphics2D.UI.Styles;
using Myra.Utility;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Collections.Concurrent;
using System;
using Myra.MML;

#if !XENKO
using Microsoft.Xna.Framework;
#else
using Xenko.Core.Mathematics;
#endif

namespace Myra.Graphics2D.UI
{
	public class ExportOptions
	{
		public string Namespace { get; set; }
		public string Class { get; set; }
		public string OutputPath { get; set; }
	}

	public class Project
	{
		private static readonly ConcurrentDictionary<string, string> LegacyNames = new ConcurrentDictionary<string, string>();
		private static readonly Type[] SerializableTypes = new Type[]
		{
			typeof(IItemWithId),
			typeof(ExportOptions),
			typeof(Grid.Proportion)
		};

		private readonly ExportOptions _exportOptions = new ExportOptions();

		[HiddenInEditor]
		public ExportOptions ExportOptions
		{
			get { return _exportOptions; }
		}

		[HiddenInEditor]
		public Widget Root { get; set; }

		[HiddenInEditor]
		public string StylesheetPath
		{
			get; set;
		}

		[HiddenInEditor]
		[XmlIgnore]
		public Stylesheet Stylesheet { get; set; }

		static Project()
		{
			LegacyNames["Button"] = "ImageTextButton";
		}

		public Project()
		{
			Stylesheet = Stylesheet.Current;
		}

		internal static SaveContext CreateSaveContext(Stylesheet stylesheet)
		{
			return new SaveContext
			{
				SerializableTypes = SerializableTypes,
				ShouldSerializeProperty = (o, p) =>
				{
					return !SaveContext.HasDefaultValue(o, p) &&
						(!(o is Widget) ||
						!HasStylesheetValue((Widget)o, p, stylesheet));
				}
			};
		}

		internal SaveContext CreateSaveContext()
		{
			return CreateSaveContext(Stylesheet);
		}

		internal static LoadContext CreateLoadContext(Stylesheet stylesheet)
		{
			return new LoadContext
			{
				SerializableTypes = SerializableTypes,
				ObjectCreator = t => CreateItem(t, stylesheet),
				Namespace = typeof(Widget).Namespace
			};
		}

		internal LoadContext CreateLoadContext()
		{
			return CreateLoadContext(Stylesheet);
		}

		public string Save()
		{
			var saveContext = CreateSaveContext();
			var root = saveContext.Save(this);

			var xDoc = new XDocument(root);

			return xDoc.ToString();
		}

		public static Project LoadFromXml(XDocument xDoc, Stylesheet stylesheet)
		{
			var result = new Project
			{
				Stylesheet = stylesheet
			};

			var loadContext = result.CreateLoadContext();
			loadContext.Load(result, xDoc.Root);

			return result;
		}

		public static Project LoadFromXml(string data, Stylesheet stylesheet)
		{
			return LoadFromXml(XDocument.Parse(data), stylesheet);
		}

		public static Project LoadFromXml(string data)
		{
			return LoadFromXml(data, Stylesheet.Current);
		}

		public static object LoadObjectFromXml(string data, Stylesheet stylesheet)
		{
			XDocument xDoc = XDocument.Parse(data);

			Type itemType;
			if (xDoc.Root.Name != "Proportion")
			{
				var itemNamespace = typeof(Widget).Namespace;

				var widgetName = xDoc.Root.Name.ToString();
				string newName;
				if (LegacyNames.TryGetValue(widgetName, out newName))
				{
					widgetName = newName;
				}

				itemType = typeof(Widget).Assembly.GetType(itemNamespace + "." + widgetName);
			}
			else
			{
				itemType = typeof(Grid.Proportion);
			}

			if (itemType == null)
			{
				return null;
			}

			var item = CreateItem(itemType, stylesheet);
			var loadContext = CreateLoadContext(stylesheet);
			loadContext.Load(item, xDoc.Root);

			return item;
		}

		public static object LoadObjectFromXml(string data)
		{
			return LoadObjectFromXml(data, Stylesheet.Current);
		}

		public string SaveObjectToXml(object obj)
		{
			var saveContext = CreateSaveContext(Stylesheet);
			return saveContext.Save(obj, true).ToString();
		}

		private static object CreateItem(Type type, Stylesheet stylesheet)
		{
			// Check whether item has constructor with stylesheet param
			var acceptsStylesheet = false;
			foreach (var c in type.GetConstructors())
			{
				var p = c.GetParameters();
				if (p != null && p.Length == 1)
				{
					if (p[0].ParameterType == typeof(Stylesheet))
					{
						acceptsStylesheet = true;
						break;
					}
				}
			}

			if (acceptsStylesheet)
			{
				return Activator.CreateInstance(type, stylesheet);
			}

			return Activator.CreateInstance(type);
		}

		public bool ShouldSerializeProperty(object w, PropertyInfo property)
		{
			var value = property.GetValue(w);
			if (property.HasDefaultValue(value))
			{
				return false;
			}

			var asWidget = w as Widget;
			if (asWidget != null && HasStylesheetValue(asWidget, property, Stylesheet))
			{
				return false;
			}

			return true;
		}

		private static bool HasStylesheetValue(Widget w, PropertyInfo property, Stylesheet stylesheet)
		{
			if (stylesheet == null)
			{
				return false;
			}

			var styleName = w.StyleName;
			if (string.IsNullOrEmpty(styleName))
			{
				styleName = Stylesheet.DefaultStyleName;
			}

			// Find styles dict of that widget
			var typeName = w.GetType().Name;
			if (typeName == "ImageTextButton" || typeName == "ImageButton" || typeName == "TextButton")
			{
				// Small hack
				// ImageTextButton styles are stored in Stylesheet.ButtonStyles
				typeName = "Button";
			}

			var stylesDictPropertyName = typeName + "Styles";
			var stylesDictProperty = stylesheet.GetType().GetRuntimeProperty(stylesDictPropertyName);
			if (stylesDictProperty == null)
			{
				return false;
			}

			var stylesDict = (IDictionary)stylesDictProperty.GetValue(stylesheet);
			if (stylesDict == null)
			{
				return false;
			}

			// Fetch style from the dict
			object obj = stylesDict[styleName];

			// Now find corresponding property
			PropertyInfo styleProperty = null;

			var stylePropertyPathAttribute = property.FindAttribute<StylePropertyPathAttribute>();
			if (stylePropertyPathAttribute != null)
			{
				var path = stylePropertyPathAttribute.Name;
				if (path.StartsWith("/"))
				{
					obj = stylesheet;
					path = path.Substring(1);
				}

				var parts = path.Split('/');
				for (var i = 0; i < parts.Length; ++i)
				{
					styleProperty = obj.GetType().GetRuntimeProperty(parts[i]);

					if (i < parts.Length - 1)
					{
						obj = styleProperty.GetValue(obj);
					}
				}
			}
			else
			{
				styleProperty = obj.GetType().GetRuntimeProperty(property.Name);
			}

			if (styleProperty == null)
			{
				return false;
			}

			// Compare values
			var styleValue = styleProperty.GetValue(obj);
			var value = property.GetValue(w);
			if (!Equals(styleValue, value))
			{
				return false;
			}

			return true;
		}
	}
}