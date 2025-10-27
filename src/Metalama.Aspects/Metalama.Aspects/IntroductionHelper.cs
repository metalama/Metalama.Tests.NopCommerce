using Metalama.Framework.Aspects;
using Metalama.Framework.Code;

namespace Metalama.Aspects;

[CompileTime]
public class IntroductionHelper
{
    [CompileTime]
    public static bool IsSkipped(IType type)
    {
        // Introductions should skip these types because there are tests that check fields and properties.
        var skippedTypes =
            new[]
            {
                "Nop.Core.BaseEntity",
                "Nop.Web.Framework.Models.BaseNopModel",
                "Nop.Core.Configuration.ISettings",
                "Nop.Core.Configuration.IConfig"
            }.Select(n =>
                {
                    try
                    {
                        return TypeFactory.GetType(n);
                    }
                    catch
                    {
                        return null;
                    }
                }
            );

        return skippedTypes.Any(t => t == null || type.IsConvertibleTo(t));
    }
}