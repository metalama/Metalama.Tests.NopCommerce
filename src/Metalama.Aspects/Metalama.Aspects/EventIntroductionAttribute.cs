#if !BENCHMARK

using Metalama.Framework.Aspects;
using Metalama.Framework.Code;

namespace Metalama.Aspects;

public class EventIntroductionAttribute : TypeAspect
{
    public override void BuildAspect(IAspectBuilder<INamedType> builder)
    {
        if (!builder.Target.IsStatic)
        {
            if (!builder.Target.AllMembers().Any(m => m.Name == nameof(IntroducedEvent)))
            {
                builder.IntroduceEvent(nameof(IntroducedEvent), whenExists: OverrideStrategy.Ignore);
            }

            if (!builder.Target.AllMembers().Any(m => m.Name == nameof(IntroducedEventField)))
            {
                builder.IntroduceEvent(nameof(IntroducedEventField), whenExists: OverrideStrategy.Ignore);
            }
        }
    }

    [Template]
    public event EventHandler IntroducedEvent
    {
        add
        {
        }

        remove
        {
        }
    }

    [Template]
    public event EventHandler IntroducedEventField;
}

#endif