using Metalama.Framework.Aspects;

namespace Metalama.Aspects;

public class LoggingAspect : OverrideMethodAspect
{
    public override dynamic? OverrideMethod()
    {
        try
        {
            var result = meta.Proceed();

            AspectLog.Write($"Logging: executing");
            var parameters = meta.Target.Parameters;

            if (parameters.Count > 0)
            {
                foreach (var parameter in parameters)
                {
                    AspectLog.Write($"Logging: Method has parameter {parameter.Name} of type {parameter.Type} with {parameter.DefaultValue} default value.");
                }
                return result;
            }
            else
            {
                AspectLog.Write("Logging: parameterless method.");
                return result;
            }
        }
        catch (Exception e)
        {
            AspectLog.Write($"Logging: caught exception {e.Message}");
            throw;
        }
    }
}