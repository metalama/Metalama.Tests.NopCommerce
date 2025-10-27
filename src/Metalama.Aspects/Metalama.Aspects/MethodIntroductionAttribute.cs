using Metalama.Framework.Aspects;
using Metalama.Framework.Code;

namespace Metalama.Aspects;

public class MethodIntroductionAttribute : TypeAspect
{
    public override void BuildAspect(IAspectBuilder<INamedType> builder)
    {
        if (!builder.Target.IsStatic)
        {
            builder.IntroduceMethod( nameof(IntroducedMethod), whenExists: OverrideStrategy.Ignore);
        }

        if (builder.Target is { IsStatic: false, IsSealed: false } and not { TypeKind: TypeKind.Struct })
        {
            builder.IntroduceMethod( nameof(IntroducedMethod_Virtual), whenExists: OverrideStrategy.Ignore);
        }

        builder.IntroduceMethod( nameof(IntroducedMethod_Static), whenExists: OverrideStrategy.Ignore);
    }

    [Template]
    public T IntroducedMethod<T>(T x)
    {
        AspectLog.Write("This is introduced method.");
        return meta.Proceed();
    }

    [Template]
    public static int IntroducedMethod_Static()
    {
        AspectLog.Write("This is introduced method.");

        return meta.Proceed();
    }

    [Template]
    public int IntroducedMethod_Virtual()
    {
        AspectLog.Write("This is introduced method.");

        return meta.Proceed();
    }
}