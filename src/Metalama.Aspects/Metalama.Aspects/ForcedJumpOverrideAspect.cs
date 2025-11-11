#if !BENCHMARK

using Metalama.Framework.Aspects;

namespace Metalama.Aspects;

public class ForcedJumpOverrideAspect : OverrideMethodAspect
{
    public override dynamic? OverrideMethod()
    {
        var x = meta.Proceed();

        if (meta.RunTime(Random.Shared.Next()) == 0)
        {
            AspectLog.Write($"ForcedJump: randomly");
            return x;
        }

        AspectLog.Write($"ForcedJump: normally");
        return x;
    }
}

#endif