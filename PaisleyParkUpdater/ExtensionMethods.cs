using System;

namespace PaisleyParkUpdater
{
	public static class ExtensionMethods
	{
		public static string VersionString(this Version v) => string.Format("v{0}.{1}.{2}", v.Major, v.Minor, v.Build);
	}
}
