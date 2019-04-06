using System;
using System.Runtime.InteropServices;

namespace PaisleyPark.Common
{
	public static class AsmjitCSharp
    {
		[DllImport("AsmjitCSharp.dll", EntryPoint = "Assembler")]
		private static extern void Native_Assembler(string input, out IntPtr addr, out int size);

		/// <summary>
		/// Assembles input for injection.
		/// </summary>
		/// <param name="input">String of assembly split by new line characters.</param>
		/// <returns>Array of bytes.</returns>
		public static byte[] Assemble(string input)
		{
			Native_Assembler(input, out IntPtr addr, out int size);
			byte[] bytes = new byte[size];
			for (var i = 0; i < size; i++)
				bytes[i] = Marshal.ReadByte(addr, i);
			return bytes;
		}

		/// <summary>
		/// Assembles input for injection.
		/// </summary>
		/// <param name="input">Array of assembly instructions.</param>
		/// <returns>Array of bytes.</returns>
		public static byte[] Assemble(string[] input) => Assemble(string.Join("\n", input));
    }
}
