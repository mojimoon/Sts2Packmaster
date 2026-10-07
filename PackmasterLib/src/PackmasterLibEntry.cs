using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using Sts2Packmaster.Lib.Api;
using Sts2Packmaster.Lib.Core;
using Sts2Packmaster.Lib.Tests;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("VanillaPacks")]

namespace Sts2Packmaster.Lib;

[ModInitializer(nameof(Init))]
public static class PackmasterLibEntry
{
	public static void Init()
	{
		var harmony = new Harmony("sts2.moon.packmasterlib");
		harmony.PatchAll(typeof(PackmasterLibEntry).Assembly);

		LibLoc.RegisterAll();
		Sts2Packmaster.Lib.Ui.PackSetupTrigger.Subscribe();

		Log.Info("[PackmasterLib] initialized (harmony patched, loc injected, API ready).");
	}
}
