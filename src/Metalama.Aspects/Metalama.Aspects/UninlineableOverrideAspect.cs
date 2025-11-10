#if !BENCHMARK

using Metalama.Framework.Aspects;

namespace Metalama.Aspects;

public class UninlineableOverrideAspect : OverrideMethodAspect
{
    public override dynamic? OverrideMethod()
    {
        if (meta.RunTime(Random.Shared.Next()) == 0)
        {
            AspectLog.Write($"Uninlineable: randomly");
            return meta.Proceed();
        }
        else
        {
            AspectLog.Write($"Uninlineable: normally");
            return meta.Proceed();
        }
    }
}

#endif