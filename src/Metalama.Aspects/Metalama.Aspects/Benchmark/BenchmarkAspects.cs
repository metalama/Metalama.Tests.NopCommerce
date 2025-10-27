#if BENCHMARK

using Metalama.Framework.Aspects;
using Metalama.Framework.Code;
using Metalama.Framework.Code.SyntaxBuilders;
using Metalama.Framework.Fabrics;

namespace Metalama.Aspects;

internal class NotNullFabric : TransitiveProjectFabric
{
    public override void AmendProject(IProjectAmender amender)
    {
        static IEnumerable<IMethod> getMethods(IMember member) => member switch
        {
            IMethod method => [method],
            IHasAccessors hasAccessors => hasAccessors.Accessors,
            IConstructor => [],
            _ => throw new NotSupportedException($"Unexpected member '{member}'")
        };

        if (!amender.Project.TryGetProperty("BenchmarkedTypesFractionInverse", out var benchmarkedTypesFractionInverseString)
            || !int.TryParse(benchmarkedTypesFractionInverseString, out var benchmarkedTypesFractionInverse))
        {
            benchmarkedTypesFractionInverse = 1;
        }

        if (!amender.Project.TryGetProperty("BenchmarkedMembersFractionInverse", out var benchmarkedMembersFractionInverseString)
            || !int.TryParse(benchmarkedMembersFractionInverseString, out var benchmarkedMembersFractionInverse))
        {
            benchmarkedMembersFractionInverse = 1;
        }

        amender
            .SelectMany(p => p.AllTypes)
            .Where(t => GetStringHashCode(t.ToDisplayString(CodeDisplayFormat.FullyQualified)) % benchmarkedTypesFractionInverse == 0)
            .SelectMany(t => t.Members())
            .SelectMany(getMethods)
            .Where(m => GetStringHashCode(m.ToDisplayString(CodeDisplayFormat.FullyQualified)) % benchmarkedMembersFractionInverse == 0)
            .AddAspectIfEligible<LogAspect>();
    }

    // From https://stackoverflow.com/a/36846609/41071.
    private static int GetStringHashCode(string s)
    {
        unchecked
        {
            int hash1 = 5381;
            int hash2 = hash1;

            for (int i = 0; i < s.Length && s[i] != '\0'; i += 2)
            {
                hash1 = ((hash1 << 5) + hash1) ^ s[i];
                if (i == s.Length - 1 || s[i + 1] == '\0')
                    break;
                hash2 = ((hash2 << 5) + hash2) ^ s[i + 1];
            }

            return hash1 + (hash2 * 1566083941);
        }
    }
}

internal class LogAspect : OverrideMethodAspect
{
    public override dynamic? OverrideMethod()
    {
        var entryMessage = BuildInterpolatedString();
        entryMessage.AddText(" called.");
        Console.WriteLine(entryMessage.ToValue());

        return meta.Proceed();
    }

    private static InterpolatedStringBuilder BuildInterpolatedString()
    {
        var stringBuilder = new InterpolatedStringBuilder();

        stringBuilder.AddText(meta.Target.Type.ToDisplayString(CodeDisplayFormat.MinimallyQualified));
        stringBuilder.AddText(".");
        stringBuilder.AddText(meta.Target.Method.Name);
        stringBuilder.AddText("(");
        var first = true;

        foreach (var p in meta.Target.Parameters)
        {
            var comma = first ? "" : ", ";

            if (p.RefKind == RefKind.Out)
            {
                stringBuilder.AddText($"{comma}out {p.Name}");
            }
            else
            {
                stringBuilder.AddText($"{comma}{p.Name}: ");
                stringBuilder.AddExpression(p);
            }

            first = false;
        }

        stringBuilder.AddText(")");

        return stringBuilder;
    }
}

#endif