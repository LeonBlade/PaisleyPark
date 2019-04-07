using System;

namespace PaisleyPark.Common
{
	public static class ExtensionMethods
	{
		public static string AsHex(this ulong s) => string.Format("0x{0:X}", s);
		public static ulong ToUint64(this IntPtr ptr) => (ulong)ptr.ToInt64();
        public static string VersionString(this Version v) => string.Format("v{0}.{1}.{2}", v.Major, v.Minor, v.Build);
	}
}
