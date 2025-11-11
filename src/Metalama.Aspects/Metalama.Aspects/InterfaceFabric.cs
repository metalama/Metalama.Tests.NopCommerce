#if !BENCHMARK

using Metalama.Framework.Aspects;
using Metalama.Framework.Code;
using Metalama.Framework.Fabrics;

namespace Metalama.Aspects;

public class InterfaceFabric : TransitiveProjectFabric
{
    public override void AmendProject(IProjectAmender amender)
    {
        #region fixed and/or working
        /* Fixed and working tests */


        // FIXED - CSC : error LAMA0001: Unexpected exception occurred in Metalama: Exception of type 'Metalama.Framework.Engine.AssertionFailedException' was thrown.
        amender.SelectMany(p =>
                p.Types
                    .Where(t => !t.IsStatic)
                    .Where(t => t is not { TypeKind: TypeKind.Enum or TypeKind.Interface or TypeKind.Delegate })
                    .Where(t => t.Name != "Program")) // This suppresses global statements.
            .AddAspect<InterfaceIntroductionAttribute>();

        #endregion

        #region errors
        /* Tests causing errors */

        // CSC : error LAMA0001: Unexpected exception occurred in Metalama: Exception of type 'Metalama.Framework.Engine.AssertionFailedException' was thrown.
        // (caused by global statements)
        //amender.With(p => 
        //    p.Types
        //    .Where(t => !t.IsStatic)
        //    .Where(t => t is not { TypeKind: TypeKind.Enum or TypeKind.Interface or TypeKind.Delegate } ) 
        //    .Where(t => t.Name == "Program" )) // This suppresses global statements.
        //.AddAspect<InterfaceIntroductionAttribute>();

        #endregion
    }
}

#endif