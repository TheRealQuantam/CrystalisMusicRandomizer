using System.Reflection;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

using RandoLib = CrystalisMusicRandoLib.RandomizerInterface;

namespace CrystalisMusicRandoLibWeb;

[SupportedOSPlatform("browser")]
public static partial class RandomizerInterfaceWeb
{
    static string _defaultErrorJson = RandoLib.CreateResultJson("failed to serialize error message JSON");

    [JSExport]
    public static string GetVersion()
        => RandoLib.GetVersion();

    [JSExport]
    public static string RandomizeRom(
        byte[] baseRom,
        int[] freeBanks,
        int seed,
        int numRetries = 8,
        bool includeBuiltin = true,
        bool includeStandardLibrary = true,
        bool includeDiverse = false,
        bool includeUnsafe = false,
        string[]? extraLibraries = null)
    {
        try
        {
            var (rom, banksLeft, log) = RandoLib.RandomizeRom(
                baseRom,
                freeBanks,
                seed,
                numRetries: numRetries,
                includeBuiltin: includeBuiltin,
                includeStandardLibrary: includeStandardLibrary,
                includeDiverse: includeDiverse,
                includeUnsafe: includeUnsafe,
                extraLibraries: extraLibraries);

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

    [JSExport]
    public static string TestLibraries(string[] extraLibraries)
    {
        try
        {
            RandoLib.TestLibraries(extraLibraries);

            return "";
        }
        catch (Exception ex)
        {
            return ex.ToString();
        }
    }
}
