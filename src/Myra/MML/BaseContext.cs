﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra.Graphics2D;
using Myra.Utility;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml.Serialization;

namespace Myra.MML
{
	internal class BaseContext
	{
		public const string IdName = "Id";

		protected static void ParseProperties(Type type, 
			out List<PropertyInfo> complexProperties, 
			out List<PropertyInfo> simpleProperties)
		{
			complexProperties = new List<PropertyInfo>();
			simpleProperties = new List<PropertyInfo>();

			var allProperties = type.GetRuntimeProperties();
			foreach (var property in allProperties)
			{
				if (property.GetMethod == null ||
					!property.GetMethod.IsPublic ||
					property.GetMethod.IsStatic)
				{
					continue;
				}

				var attr = property.FindAttribute<XmlIgnoreAttribute>();
				if (attr != null)
				{
					continue;
				}

				var propertyType = property.PropertyType;
				if (propertyType.IsPrimitive || 
					propertyType.IsNullablePrimitive() ||
					propertyType.IsEnum || 
					propertyType == typeof(string) ||
					propertyType == typeof(Color) ||
					propertyType == typeof(Color?) ||
					propertyType == typeof(IRenderable) ||
					propertyType == typeof(SpriteFont))
				{
					simpleProperties.Add(property);
				} else
				{
					complexProperties.Add(property);
				}
			}
		}
	}
}
