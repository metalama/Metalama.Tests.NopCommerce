using Metalama.Framework.Aspects;
using Metalama.Framework.Code;

namespace Metalama.Aspects;

public class PropertyIntroductionAttribute : TypeAspect
{
    public override void BuildAspect(IAspectBuilder<INamedType> builder)
    {
        if (!builder.Target.IsStatic
            && !IntroductionHelper.IsSkipped(builder.Target))
        {
            if (!builder.Target.AllMembers().Any(m => m.Name == nameof(IntroducedProperty)))
            {
                builder.IntroduceProperty( nameof(IntroducedProperty), whenExists: OverrideStrategy.Ignore);
            }

            if (!builder.Target.AllMembers().Any(m => m.Name == nameof(IntroducedGetOnlyProperty)))
            {
                builder.IntroduceProperty( nameof(IntroducedGetOnlyProperty), whenExists: OverrideStrategy.Ignore);
            }
        }

        if (!builder.Target.AllMembers().Any(m => m.Name == nameof(IntroducedStaticProperty)))
        {
            builder.IntroduceProperty( nameof(IntroducedStaticProperty), whenExists: OverrideStrategy.Ignore);
        }
    }

    [Template]
    public int IntroducedProperty { get; set; } = 42;

    [Template]
    public int IntroducedGetOnlyProperty { get; } = 42;

    [Template]
    public static int IntroducedStaticProperty { get; set; } = 42;
}