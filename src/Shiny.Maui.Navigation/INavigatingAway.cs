namespace Shiny;

/// <summary>
/// Implemented by a ViewModel that wants a hook just before the user leaves it.
/// </summary>
/// <remarks>
/// This is the typed-navigation counterpart to <c>Shiny.Maui.Shell</c>'s
/// <c>INavigationAware.OnNavigatingFrom(IDictionary&lt;string, object&gt;)</c>. There is no
/// parameter dictionary here because there are no string-keyed parameters in this model -
/// data flows to the destination through the <c>configure</c> callback on
/// <see cref="INavigator.NavigateTo{TViewModel}"/> instead.
/// </remarks>
public interface INavigatingAway
{
    /// <summary>Invoked immediately before navigation away from this viewmodel's page</summary>
    void OnNavigatingAway();
}
