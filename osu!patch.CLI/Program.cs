using System;

namespace osu_patch
{
	public class Program
	{
		// Basic wrapper
		public static int Main(string[] args)
		{
			try
			{
				return OsuPatcher.Main(args);
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
				throw;
			}
		}
	}
}