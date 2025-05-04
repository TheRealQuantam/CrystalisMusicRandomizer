using CrystalisMusicRandoLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CrystalisMusicRandomizer;

using RandoLib = CrystalisMusicRandoLib.RandomizerInterface;

internal class Program
{
    static void Main(string[] args)
    {
        try
        {
            int argIdx = 0;
            string srcPath = args[argIdx++];

            int seed = (argIdx < args.Length)
                ? int.Parse(args[argIdx++])
                : new Random().Next();

            RandoLib.TestLibraries([]);

            var srcRom = File.ReadAllBytes(srcPath);
            var (tgtRom, freeBanks, logString) = RandoLib.RandomizeRom(
                srcRom,
                Enumerable.Range(0x2b, 0x3c - 0x2b).ToArray(),
                0);

            string? dirPath = Path.GetDirectoryName(srcPath),
                pathStem = Path.GetFileNameWithoutExtension(srcPath),
                pathExt = Path.GetExtension(srcPath);
            string tgtPath = Path.Join(dirPath, $"{pathStem} Music{pathExt}");

            File.WriteAllBytes(tgtPath, tgtRom);

            string json = RandoLib.CreateResultJson(
                tgtRom, freeBanks.ToArray(), logString);
            Console.WriteLine(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
        }
    }
}
