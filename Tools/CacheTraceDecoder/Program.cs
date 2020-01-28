using System;
using System.Windows.Forms;

namespace CacheTraceDecoder
{
	public static class Program
	{
		[STAThread]
		public static int Main()
		{
			Application.EnableVisualStyles();
			Application.Run(new MainForm());
			return 0;
		}
	}
}
