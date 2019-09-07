using Myra.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;

namespace Myra.MML
{
	internal class BaseContext
	{
		public Type[] SerializableTypes = new Type[0];

		protected void ParseProperties(Type type, out List<PropertyInfo> complexProperties, out List<PropertyInfo> simpleProperties)
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

				if ((from t in SerializableTypes where t.IsAssignableFrom(property.PropertyType) select t).FirstOrDefault() != null)
				{
					complexProperties.Add(property);
				}
				else
				{
					var propertyType = property.PropertyType;
					if ((typeof(IList).IsAssignableFrom(propertyType) ||
						typeof(IDictionary).IsAssignableFrom(propertyType)) && 
						propertyType.IsGenericType &&
						(from t in SerializableTypes where t.IsAssignableFrom(propertyType.GenericTypeArguments[0]) select t).FirstOrDefault() != null)
					{
						complexProperties.Add(property);
					}
					else
					{
						simpleProperties.Add(property);
					}
				}
			}
		}
	}
}
