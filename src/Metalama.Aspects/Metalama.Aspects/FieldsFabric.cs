#if !BENCHMARK

using Metalama.Framework.Aspects;
using Metalama.Framework.Code;
using Metalama.Framework.Fabrics;

namespace Metalama.Aspects;

public class FieldsFabric : TransitiveProjectFabric
{
    public override void AmendProject(IProjectAmender amender)
    {
        #region fixed and/or working
        /* Fixed and working tests */

        // The field on this type is used as an out parameter, so it cannot be overridden.
        static bool isNotUsedAsOut(IField it) => it.DeclaringType.FullName != "Nop.Tests.Nop.Core.Tests.Infrastructure.ConcurrentTrieTests";

        // #35565 - Overriding dynamic fields causes a debug assert.
        static bool isNotDynamic(IField it) => it.Type.TypeKind != TypeKind.Dynamic;

        // FIXED: CSC : error LAMA0001: Unexpected exception occurred in Metalama: Exception of type 'Metalama.Framework.Engine.AssertionFailedException' was thrown.
        amender
            .SelectMany(p =>
                p.Types.SelectMany(t => t.Fields)
                    .Where(isNotUsedAsOut)
                    .Where(isNotDynamic)
                    .Where(it => it is { IsAbstract: false, IsImplicitlyDeclared: false, Writeability: not Writeability.None })
                    .Where(it => it is not IField { Writeability: Writeability.None })
                    .Where(it => it.DeclaringType is not { TypeKind: TypeKind.Enum or TypeKind.Interface }))
            .AddAspect<OverridePropertyAttribute>();

        // This works by chance - see #35547.
        amender
            .SelectMany(p =>
                p.Types.SelectMany(t => t.Fields)
                    .Where(isNotUsedAsOut)
                    .Where(isNotDynamic)
                    .Where(f => f.Writeability is not Writeability.None )
                    .SelectMany(p => new[] { p.GetMethod!, p.SetMethod! }.Where(m => m != null! ))
                    .Where(m => m is { IsAbstract: false, IsImplicitlyDeclared: false })
                    .Where(it => it is not IField { Writeability: Writeability.None })
                    .Where(it => it.DeclaringType is not { TypeKind: TypeKind.Enum or TypeKind.Interface }))
            .AddAspects(new LoggingAspect(), new ForcedJumpOverrideAspect(), new UninlineableOverrideAspect());

        // Contracts on fields
        amender
            .SelectMany(p =>
                p.Types.SelectMany(t => t.Fields)
                    .Where(isNotUsedAsOut)
                    .Where(isNotDynamic)
                    .Where(it => it is { IsAbstract: false, IsImplicitlyDeclared: false, Writeability: not Writeability.None })
                    .Where(it => it is not IField { Writeability: Writeability.None })
                    .Where(it => it.GetMethod!.GetIteratorInfo().EnumerableKind == EnumerableKind.None)
                    .Where(it => it.DeclaringType is not { TypeKind: TypeKind.Enum or TypeKind.Interface }))
            .AddAspect<FieldOrPropertyContract>();

        #endregion
    }
}

#endif