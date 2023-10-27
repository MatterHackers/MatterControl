/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using MatterHackers.Agg;
using MatterHackers.DataConverters3D;

namespace MatterHackers.MatterControl.DesignTools
{
	public class EditableProperty
	{
		/// <summary>
		/// The object that contains the property. If this were an Object3D, this would be the Object3D that has the property.
		/// </summary>
		public object Source { get; private set; }

		public PropertyInfo PropertyInfo { get; private set; }

		public EditableProperty(PropertyInfo propertyInfo, object source)
		{
			this.Source = source;
			this.PropertyInfo = propertyInfo;
		}

		private string GetDescription(PropertyInfo prop)
		{
			if (prop.GetCustomAttributes(true).OfType<DescriptionAttribute>().FirstOrDefault() is DescriptionAttribute descriptionAttribute)
			{
				var description = descriptionAttribute.Description;

				if (prop.GetCustomAttributes(true).OfType<DescriptionImageAttribute>().FirstOrDefault() is DescriptionImageAttribute descriptionImageAttribute)
				{
					if (descriptionImageAttribute.ImageUrl.Contains("googleusercontent"))
					{
						// the "=w200" scales the image
						description += $"\n\n![{prop.Name} Image]({descriptionImageAttribute.ImageUrl}=w240)";
					}
					else
					{
						description += $"\n\n![{prop.Name} Image]({descriptionImageAttribute.ImageUrl})";
					}
				}

				return description;
			}

			return null;
		}

		public static string GetDisplayName(PropertyInfo prop)
		{
			var nameAttribute = prop.GetCustomAttributes(true).OfType<DisplayNameAttribute>().FirstOrDefault();
			return nameAttribute?.DisplayName ?? prop.Name.SplitCamelCase();
		}

		public object Value => PropertyInfo.GetGetMethod().Invoke(Source, null);

		/// <summary>
		/// Use reflection to set property value.
		/// </summary>
		/// <param name="value">The value to set through reflection.</param>
		public void SetValue(object value)
		{
			this.PropertyInfo.GetSetMethod().Invoke(Source, new object[] { value });
		}

		public string DisplayName => GetDisplayName(PropertyInfo);

		public string Description => GetDescription(PropertyInfo);

		public Type PropertyType => PropertyInfo.PropertyType;
	}
}