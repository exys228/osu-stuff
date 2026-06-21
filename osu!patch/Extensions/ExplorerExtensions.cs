using osu_patch.Explorers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace osu_patch.Extensions
{
	public static class ExplorerExtensions
	{
		public static ModuleExplorer GetRoot(this IExplorerParent explorerParent)
		{
			while (!(explorerParent is ModuleExplorer))
				explorerParent = explorerParent.GetParent();

			return (ModuleExplorer)explorerParent;
		}
	}
}
