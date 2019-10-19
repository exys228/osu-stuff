using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CacheTraceDecoder
{
	public static class Program
	{
		[STAThread]
		public static int Main(string[] args)
		{
			Application.EnableVisualStyles();
			Application.Run(new MainForm());
			return 0;
		}
	}
}
