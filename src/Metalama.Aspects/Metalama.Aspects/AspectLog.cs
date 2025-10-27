namespace Metalama.Aspects;

public static class AspectLog
{
    public static void Write(string s)
    {
        // This makes sure the aspect code does not write to console, which seems to retain memory in tests.
    }
}