using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrystalisMusicRandoLib;

public class JsonSuccessResult
{
    public required byte[] rom { get; init; }
    public required int[] freeBanks { get; init; }
    public required string log { get; init; }
}

public class JsonFailureResult
{
    public required string errorMessage { get; init; }
}
