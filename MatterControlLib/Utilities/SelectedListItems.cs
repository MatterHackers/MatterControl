using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl
{
	public class SelectedListItems<T> : List<T>
	{
		public event EventHandler OnAdd;

		public event EventHandler OnRemove;

		new public void Add(T item)
		{
			base.Add(item);
			if (null != OnAdd)
			{
				OnAdd(this, null);
			}
		}

		new public void Remove(T item)
		{
			base.Remove(item);
			if (null != OnRemove)
			{
				OnRemove(this, null);
			}
		}

		// Also fire OnRemove on Clear
		new public void Clear()
		{
			base.Clear();
			if (null != OnRemove)
			{
				OnRemove(this, null);
			}
		}
	}
}