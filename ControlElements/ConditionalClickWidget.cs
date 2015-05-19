using System;

namespace MatterHackers.MatterControl
{
	public class ConditionalClickWidget : ClickWidget
	{
		private Func<bool> enabledCallback;

		public ConditionalClickWidget(Func<bool> enabledCallback)
		{
			this.enabledCallback = enabledCallback;
		}

		// The ConditionalClickWidget provides a mechanism that allows the Enable property to be bound
		// to a Delegate that resolves the value. This is a readonly value supplied via the constructor
		// and should not be assigned after construction
		public override bool Enabled
		{
			get
			{
				return this.enabledCallback();
			}

			set
			{
				Console.WriteLine("Attempted to set readonly Enabled property on ConditionalClickWidget");
#if DEBUG
				throw new InvalidOperationException("Cannot set Enabled on ConditionalClickWidget");
#endif
			}
		}
	}
}