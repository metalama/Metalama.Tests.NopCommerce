#if !BENCHMARK

using Metalama.Framework.Aspects;

namespace Metalama.Aspects;

public class FieldOrPropertyContract : ContractAspect
{
    public override void Validate(dynamic? value)
    {
        AspectLog.Write($"Contract on {meta.Target.FieldOrPropertyOrIndexer.Name}");
    }
}

#endif