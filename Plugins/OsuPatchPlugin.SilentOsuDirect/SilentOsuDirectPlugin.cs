﻿using dnlib.DotNet;
using osu_patch;
using osu_patch.Plugins;
using System.Collections.Generic;

namespace OsuPatchPlugin.SilentOsuDirect
{
	public class SilentOsuDirectPlugin : IOsuPatchPlugin
	{
		public IEnumerable<Patch> GetPatches() => new Patch[]
		{
			/*
			new Patch("UpdateStatus patch", true, (patch, exp) =>
			{
				var editor = exp["osu.Online.BanchoClient"]["UpdateStatus"].Editor;

				var loc = editor.Locate(new[]
				{
					OpCodes.Ldc_I4_S,
					OpCodes.Starg_S,
					OpCodes.Br_S,
					OpCodes.Call,
					OpCodes.Brfalse_S,
				});

				return new PatchResult(patch, PatchStatus.Success);
			})
			*/
		};

		public void Load(ModuleDef originalObfOsuModule) { }
	}
}