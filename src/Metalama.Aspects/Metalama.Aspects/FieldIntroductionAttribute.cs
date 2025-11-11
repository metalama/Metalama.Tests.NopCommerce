#if !BENCHMARK

using Metalama.Framework.Aspects;
using Metalama.Framework.Code;

namespace Metalama.Aspects;

public class FieldIntroductionAttribute : TypeAspect
{
    public override void BuildAspect(IAspectBuilder<INamedType> builder)
    {
        if (!builder.Target.IsStatic
            && !builder.Target.AllMembers().Any(m => m.Name == nameof(IntroducedField))
            && !IntroductionHelper.IsSkipped(builder.Target))
        {
            builder.IntroduceField( nameof(IntroducedField), whenExists: OverrideStrategy.Ignore);
        }

        if (!builder.Target.AllMembers().Any(m => m.Name == nameof(IntroducedField_Static)))
        {
            builder.IntroduceField( nameof(IntroducedField_Static), whenExists: OverrideStrategy.Ignore);
        }
    }

    [Template]
    public int IntroducedField = 42;

    [Template]
    public static int IntroducedField_Static = 42;
}

#endif