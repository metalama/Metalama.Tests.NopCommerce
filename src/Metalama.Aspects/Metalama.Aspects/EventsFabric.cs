#if !BENCHMARK

using Metalama.Framework.Aspects;
using Metalama.Framework.Fabrics;

namespace Metalama.Aspects;

public class EventsFabric : TransitiveProjectFabric
{
    public override void AmendProject(IProjectAmender amender)
    {
        amender.SelectMany(p => p.Types.SelectMany(t => t.Events).Where(e => !e.IsImplicitlyDeclared)).AddAspect<OverrideEventAttribute>();

        // Method aspects on event accessors.
        amender
            .SelectMany(p =>
                p.Types.SelectMany(t => t.Events)
                    .SelectMany(p => new[] { p.AddMethod!, p.RemoveMethod! }.Where(m => m != null))
                    .Where(m => m is { IsAbstract: false, IsImplicitlyDeclared: false }))
            .AddAspects(new LoggingAspect(), new ForcedJumpOverrideAspect(), new UninlineableOverrideAspect());
    }
}

#endif