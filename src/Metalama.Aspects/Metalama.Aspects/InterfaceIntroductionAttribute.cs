using Metalama.Framework.Aspects;
using Metalama.Framework.Code;

namespace Metalama.Aspects;

public class InterfaceIntroductionAttribute : TypeAspect
{
    public override void BuildAspect(IAspectBuilder<INamedType> builder)
    {
        builder.ImplementInterface(typeof(IIntroducedInterface), whenExists: OverrideStrategy.Ignore);
    }

    [InterfaceMember(IsExplicit = true)]
    public int InterfaceMethod()
    {
        AspectLog.Write("This is introduced interface member.");
        return meta.Proceed();
    }

    [InterfaceMember(IsExplicit = true)]
    public event EventHandler? InterfaceEvent
    {
        add
        {
            AspectLog.Write("This is introduced interface member.");
            meta.Proceed();
        }

        remove
        {
            AspectLog.Write("This is introduced interface member.");
            meta.Proceed();
        }
    }

    [InterfaceMember(IsExplicit = true)]
    public event EventHandler? InterfaceEventField = default;

    [InterfaceMember(IsExplicit = true)]
    public int Property
    {
        get
        {
            AspectLog.Write("This is introduced interface member.");

            return meta.Proceed();
        }

        set
        {
            AspectLog.Write("This is introduced interface member.");
            meta.Proceed();
        }
    }

    [InterfaceMember(IsExplicit = true)]
    public string? AutoProperty { get; set; } = default;
}