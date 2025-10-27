using Metalama.Framework.Aspects;

namespace Metalama.Aspects;

public class OverrideEventAttribute : OverrideEventAspect
{
    public override void OverrideAdd(dynamic value)
    {
        AspectLog.Write("Overridden add.");
        meta.Proceed();
    }

    public override void OverrideRemove(dynamic value)
    {
        AspectLog.Write("Overridden remove.");
        meta.Proceed();
    }
}