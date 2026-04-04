using System.Diagnostics.CodeAnalysis;

namespace Shiny;


public sealed class ShinyAppBuilder(MauiAppBuilder builder)
{
    readonly Dictionary<string, (bool RegisterRoute, Type PageType, Type ViewModelType)> typeMap = new();

    /// <summary>
    /// Maps the Page <=> ViewModel and optionally registers the route
    /// </summary>
    /// <typeparam name="TPage">The page type</typeparam>
    /// <typeparam name="TViewModel">The viewmodel type</typeparam>
    /// <param name="route">Optional - uses page name otherwise</param>
    /// <param name="registerRoute">If you have datatemplate item configured in your Shell XAML, pass false here</param>
    /// <returns></returns>
    public ShinyAppBuilder Add<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPage, 
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TViewModel
    >(string? route = null, bool registerRoute = true)
        where TPage : Page
        where TViewModel : class, INotifyPropertyChanged
    {
        route ??= typeof(TPage).Name;
        this.typeMap[route] = (registerRoute, typeof(TPage), typeof(TViewModel));
        return this;
    }


    /// <summary>
    /// Sets the dialog provider you want to use
    /// </summary>
    /// <typeparam name="TDialog"></typeparam>
    /// <returns></returns>
    public ShinyAppBuilder UseDialogs<TDialog>() where TDialog : class, IDialogs
    {
        builder.Services.AddSingleton<IDialogs, TDialog>();
        return this;
    }


    /// <summary>
    /// Enables CarPlay support with consumer-provided navigator and dialog implementations.
    /// When a CarPlay session connects, INavigator and IDialogs will automatically delegate to these types.
    /// </summary>
    /// <typeparam name="TNavigator">The CarPlay navigator implementation</typeparam>
    /// <typeparam name="TDialogs">The CarPlay dialogs implementation</typeparam>
    public ShinyAppBuilder UseCarPlay<TNavigator, TDialogs>()
        where TNavigator : class, INavigator
        where TDialogs : class, IDialogs
    {
        this.CarPlayRegistration = new CarPlayRegistration(typeof(TNavigator), typeof(TDialogs));
        builder.Services.AddSingleton<TNavigator>();
        builder.Services.AddSingleton<TDialogs>();
        builder.Services.AddSingleton(this.CarPlayRegistration);
        return this;
    }

    internal CarPlayRegistration? CarPlayRegistration { get; private set; }


    /// <summary>
    /// Gets the ViewModel type for a given page type
    /// </summary>
    /// <param name="page"></param>
    /// <returns></returns>
    public Type? GetViewModelTypeForPage(Page page)
    {
        var pageType = page.GetType();
        foreach (var pair in this.typeMap)
        {
            if (pair.Value.PageType == pageType) 
                return pair.Value.ViewModelType;
        }
        return null;
    }


    /// <summary>
    /// Gets the route for a given ViewModel type
    /// </summary>
    /// <param name="viewModelType"></param>
    /// <returns></returns>
    public string? GetRouteForViewModel(Type viewModelType)
    {
        foreach (var pair in this.typeMap)
        {
            if (pair.Value.ViewModelType == viewModelType)
                return pair.Key;
        }

        return null;
    }
    
    
    internal void RegisterDependencies()
    {
        foreach (var pair in this.typeMap)
        {
            builder.Services.AddTransient(pair.Value.PageType);
            builder.Services.AddTransient(pair.Value.ViewModelType);
            
            if (pair.Value.RegisterRoute)
            {
                Routing.RegisterRoute(
                    pair.Key,
                    new ShinyRouteFactory(
                        pair.Value.PageType,
                        pair.Value.ViewModelType
                    )
                );
            }
        }
    }
}