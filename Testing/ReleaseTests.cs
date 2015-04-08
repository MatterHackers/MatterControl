using System;

namespace MatterHackers.MatterControl.Testing
{
	public static class ReleaseTests
	{
		public static void AssertDebugNotDefined()
		{
#if DEBUG
			throw new Exception("DEBUG is defined and should not be!");
#endif
		}
	}
}