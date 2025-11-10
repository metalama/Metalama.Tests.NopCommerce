#if !BENCHMARK

using Metalama.Framework.Aspects;

namespace Metalama.Aspects;

public class ParameterContract : ContractAspect
{
    public override void Validate(dynamic? value)
    {
        AspectLog.Write($"Contract on {meta.Target.Parameter.Name}");
    }
}

#endif