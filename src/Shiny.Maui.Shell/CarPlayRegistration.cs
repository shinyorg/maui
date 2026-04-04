namespace Shiny;

public sealed class CarPlayRegistration(Type navigatorType, Type dialogsType)
{
    public Type NavigatorType { get; } = navigatorType;
    public Type DialogsType { get; } = dialogsType;
}
