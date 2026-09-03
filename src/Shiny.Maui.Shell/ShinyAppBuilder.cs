using System.Diagnostics.CodeAnalysis;

namespace Shiny;


public sealed class ShinyAppBuilder(MauiAppBuilder builder)
{
    public MauiAppBuilder MauiBuilder => builder;
    
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
    public ShinyAppBuilder UseDialogs<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TDialog
    >() where TDialog : class, IDialogs
    {
        builder.Services.AddSingleton<IDialogs, TDialog>();
        return this;
    }


    /// <summary>
    /// Sets the presenter used to display dialog ViewModels shown with
    /// <see cref="INavigator.ShowDialog{TViewModel, T}"/>. Defaults to
    /// <see cref="ShellModalDialogPresenter"/>, which pushes the page onto Shell's modal stack.
    /// </summary>
    /// <typeparam name="TPresenter"></typeparam>
    /// <returns></returns>
    public ShinyAppBuilder UseDialogPresenter<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPresenter
    >() where TPresenter : class, IDialogPresenter
    {
        builder.Services.AddSingleton<IDialogPresenter, TPresenter>();
        return this;
    }


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


    /// <summary>
    /// Gets the Page type mapped to a given ViewModel type
    /// </summary>
    /// <param name="viewModelType"></param>
    /// <returns></returns>
    public Type? GetPageTypeForViewModel(Type viewModelType)
    {
        foreach (var pair in this.typeMap)
        {
            if (pair.Value.ViewModelType == viewModelType)
                return pair.Value.PageType;
        }

        return null;
    }


    /// <summary>
    /// Gets the Page type registered for a given route
    /// </summary>
    /// <param name="route"></param>
    /// <returns></returns>
    public Type? GetPageTypeForRoute(string route)
        => this.typeMap.TryGetValue(route, out var entry) ? entry.PageType : null;
    
    
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