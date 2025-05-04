using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

using RandoLib = CrystalisMusicRandoLib.RandomizerInterface;

namespace CrystalisMusicRandoLibWeb;

[SupportedOSPlatform("browser")]
public static partial class RandomizerInterfaceWeb
{
    static string _defaultErrorJson = RandoLib.CreateResultJson("failed to serialize error message JSON");

    [JSExport]
    public static string TestInterop()
    {
        return "Success";
    }

    [JSExport]
    public static string RandomizeRom(
        byte[] baseRom,
        int[] freeBanks,
        int seed,
        int numRetries = 8,
        bool includeBuiltin = true,
        bool includeStandardLibrary = true,
        bool includeUnsafe = false,
        string[]? extraLibraries = null)
    {
        try
        {
            var (rom, banksLeft, log) = RandoLib.RandomizeRom(
                baseRom,
                freeBanks,
                seed,
                numRetries,
                includeBuiltin,
                includeStandardLibrary,
                includeUnsafe,
                extraLibraries);

            return RandoLib.CreateResultJson(rom, banksLeft.ToArray(), log);
        }
        catch (Exception ex)
        {
            try
            {
                return RandoLib.CreateResultJson(ex.ToString());
            }
            catch
            {
                return _defaultErrorJson;
            }
        }
    }
}
