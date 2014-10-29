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

        public override bool Enabled
        {
            get
            {
                return this.enabledCallback();
            }

            set 
            { 
                throw new InvalidOperationException("Cannot set Enabled on ConditionalClickWidget"); 
            }
        }
    }
}
