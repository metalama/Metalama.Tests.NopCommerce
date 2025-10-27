using System.Reflection.Metadata.Ecma335;
using Metalama.Aspects;
using Metalama.Framework.Aspects;
using Metalama.Framework.Code;
using Metalama.Framework.Fabrics;

#if !BENCHMARK

/*
 * IMPORTANT: Templates should not create string literals that depend on names/display strings.
 *            Nop.Web (and Razor in general) is creating lot of literals and runs close to the 16Mb limit of #US PE stream.
 */

[assembly: AspectOrder(
    AspectOrderDirection.RunTime,
    typeof(UninlineableOverrideAspect),
    typeof(ForcedJumpOverrideAspect),
    typeof(LoggingAspect),
    typeof(OverridePropertyAttribute),
    typeof(OverrideFinalizerAttribute),
    typeof(OverrideEventAttribute),
    typeof(MethodIntroductionAttribute),
    typeof(FieldIntroductionAttribute),
    typeof(PropertyIntroductionAttribute),
    typeof(InterfaceIntroductionAttribute))]


namespace Metalama.Aspects
{
  
    [CompileTime]
    public static class HelperExtensions
    {
        public static void AddAspects<T>(this IQuery<T> aspectReceiver, params IAspect<T>[] aspects)
            where T : class, IDeclaration
        {
            foreach (var aspect in aspects)
            {
                aspectReceiver.AddAspect(aspect.GetType(), _ => aspect);
            }
        }
    }
  
    
}

#endif