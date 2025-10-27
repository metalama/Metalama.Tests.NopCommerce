using Metalama.Framework.Aspects;
using Metalama.Framework.Code;
using Metalama.Framework.Fabrics;

namespace Metalama.Aspects;

public class IntroductionFabric : TransitiveProjectFabric
{
    public override void AmendProject(IProjectAmender amender)
    {
        amender.SelectMany(p =>
                p.Types
                    .Where(t => t.TypeKind is not (TypeKind.Interface or TypeKind.Enum or TypeKind.Delegate) && !t.IsImplicitlyDeclared))
            .AddAspect<FieldIntroductionAttribute>();

        amender.SelectMany(p =>
                p.Types
                    .Where(t => t.TypeKind is not (TypeKind.Interface or TypeKind.Enum or TypeKind.Delegate) && !t.IsImplicitlyDeclared))
            .AddAspect<MethodIntroductionAttribute>();

        amender.SelectMany(p =>
                p.Types
                    .Where(t => t.TypeKind is not (TypeKind.Interface or TypeKind.Enum or TypeKind.Delegate) && !t.IsImplicitlyDeclared))
            .AddAspect<PropertyIntroductionAttribute>();

        amender.SelectMany(p =>
                p.Types
                    .Where(t => t.TypeKind is not (TypeKind.Interface or TypeKind.Enum or TypeKind.Delegate) && !t.IsImplicitlyDeclared))
            .AddAspect<EventIntroductionAttribute>();
    }
}