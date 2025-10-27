namespace Metalama.Aspects;

public interface IIntroducedInterface
{
    int InterfaceMethod();

    event EventHandler InterfaceEvent;

    event EventHandler? InterfaceEventField;

    int Property { get; set; }

    string? AutoProperty { get; set; }
}