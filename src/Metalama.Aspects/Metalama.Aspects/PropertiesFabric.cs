using Metalama.Framework.Aspects;
using Metalama.Framework.Code;
using Metalama.Framework.Fabrics;

namespace Metalama.Aspects;

public class PropertiesFabric : TransitiveProjectFabric
{
    public override void AmendProject(IProjectAmender amender)
    {
        #region fixed and/or working
        /* Fixed and working tests */

        // Overriding this property seems to break the ORM (specifically, the Nop.Tests.Nop.Web.Tests.Public.Factories.ProductModelFactoryTests.CanPreparePriceModel test).
        static bool isNotDiscountMappingId(IProperty property) => property is not { Name: "Id", DeclaringType.Name: "DiscountMapping" };

        amender.SelectMany(p =>
                p.Types.SelectMany(t => t.Properties)
                    .Where(isNotDiscountMappingId)
                    .Where(it => it is not { IsOverride: true, OverriddenProperty: { IsAbstract: true, GetMethod: not null, SetMethod: null } })
                    .Where(it => it.DeclaringType is not { TypeKind: TypeKind.Interface })
                    .Where(it => it is { IsAbstract: false, IsImplicitlyDeclared: false }))
            .AddAspect<OverridePropertyAttribute>();

        // FIXED 
        amender.SelectMany(p =>
                p.Types.SelectMany(t => t.Properties)
                    .Where(it => it is { IsOverride: true, OverriddenProperty: { IsAbstract: true, GetMethod: not null, SetMethod: null } })
                    .Where(it => it.DeclaringType is not { TypeKind: TypeKind.Interface })
                    .Where(it => it is { IsAbstract: false, IsImplicitlyDeclared: false }))
            .AddAspect<OverridePropertyAttribute>();

        // FIXED
        amender.SelectMany(p =>
                p.Types.SelectMany(t => t.Properties)
                    .Where(it => it is { IsAbstract: false, IsImplicitlyDeclared: false })
                    .Where(it => it.DeclaringType is { TypeKind: TypeKind.Interface }))
            .AddAspect<OverridePropertyAttribute>();

        // Method aspects on property accessors.
        amender
            .SelectMany(p =>
                p.Types.SelectMany(t => t.Properties)
                    .Where(isNotDiscountMappingId)
                    .SelectMany(p => new[] { p.GetMethod!, p.SetMethod! }.Where(m => m != null))
                    .Where(m => m is { IsAbstract: false, IsImplicitlyDeclared: false }))
            .AddAspects(new LoggingAspect(), new ForcedJumpOverrideAspect(), new UninlineableOverrideAspect());

        // Contracts on properties
        amender
            .SelectMany(p =>
                p.Types.SelectMany(t => t.Properties)
                    .Where(isNotDiscountMappingId)
                    .Where(m => m is { IsAbstract: false, IsImplicitlyDeclared: false }))
            .Where(it => it.GetMethod == null || it.GetMethod.GetIteratorInfo().EnumerableKind == EnumerableKind.None)
            .AddAspect<FieldOrPropertyContract>();

        #endregion

        #region errors
        /* Tests causing errors */
        #endregion
    }
}