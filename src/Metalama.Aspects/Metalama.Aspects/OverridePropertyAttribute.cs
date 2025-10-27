using Metalama.Framework.Aspects;

namespace Metalama.Aspects;

public class OverridePropertyAttribute : OverrideFieldOrPropertyAspect
{
    public override dynamic? OverrideProperty
    {
        get
        {
            AspectLog.Write("This is the overridden getter.");
            return meta.Proceed();
        }

        set
        {
            AspectLog.Write($"This is the overridden setter.");
            meta.Proceed();
        }
    }
}