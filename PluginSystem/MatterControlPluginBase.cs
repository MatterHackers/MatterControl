using MatterHackers.Agg.UI;
using System;

namespace MatterHackers.MatterControl.PluginSystem
{
	public class MatterControlPlugin
	{
		/// <summary>
		/// Each plugin that is found will be instantiated and passed the main application widget.
		/// It is then up to the plugin to do whatever initialization or registration that it requires.
		/// </summary>
		/// <param name="application"></param>
		public virtual void Initialize(GuiWidget application)
		{
		}

		/// <summary>
		/// Return a json string representing plugin information
		/// {
		///     "Name": "MatterHackers Test Plugin",
		///     "UUID": "22cf8c90-66c3-11e3-949a-0800200c9a66",
		///     "About": "This is a sample plugin info that shows some of the expected values that can be present in a plugin info.",
		///     "Developer": "MatterHackers, Inc."
		///     "URL": "https://www.matterhackers.com"
		/// }
		/// </summary>
		/// <returns></returns>
		public virtual string GetPluginInfoJSon()
		{
			return "";
		}

		public static GuiWidget FindNamedWidgetRecursive(GuiWidget root, string name)
		{
			foreach (GuiWidget child in root.Children)
			{
				if (child.Name == name)
				{
					return child;
				}

				GuiWidget foundWidget = FindNamedWidgetRecursive(child, name);
				if (foundWidget != null)
				{
					return foundWidget;
				}
			}

			return null;
		}

		public static void AssertDebugNotDefined()
		{
#if DEBUG
			throw new Exception("DEBUG is defined and should not be!");
#endif
		}
	}
}