using Metalama.Framework.Aspects;
using Metalama.Framework.Code;

namespace Metalama.Aspects;

public class OverrideFinalizerAttribute : TypeAspect
{
    public override void BuildAspect(IAspectBuilder<INamedType> builder)
    {
        var introductionResult = builder.IntroduceFinalizer( nameof(IntroduceTemplate), whenExists: OverrideStrategy.Override);
    }

    [Template]
    public dynamic? IntroduceTemplate()
    {
        AspectLog.Write("This is the introduction.");
        return meta.Proceed();
    }
}