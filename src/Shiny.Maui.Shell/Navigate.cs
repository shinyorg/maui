namespace Shiny;

public sealed class NavigationParameters : List<NavigationParameter>
{
}


public sealed class NavigationParameter : BindableObject
{
    public static readonly BindableProperty KeyProperty = BindableProperty.Create(
        nameof(Key),
        typeof(string),
        typeof(NavigationParameter),
        null
    );

    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value),
        typeof(object),
        typeof(NavigationParameter),
        null
    );

    public string? Key
    {
        get => (string?)this.GetValue(KeyProperty);
        set => this.SetValue(KeyProperty, value);
    }

    public object? Value
    {
        get => this.GetValue(ValueProperty);
        set => this.SetValue(ValueProperty, value);
    }
}


public static class Navigate
{
    static readonly Dictionary<Type, (Action<BindableObject, EventHandler> Attach, Action<BindableObject, EventHandler> Detach)> invokers = new();

    public static void RegisterInvoker<T>(
        Action<T, EventHandler> attach,
        Action<T, EventHandler> detach
    ) where T : BindableObject
        => invokers[typeof(T)] = (
            (b, h) => attach((T)b, h),
            (b, h) => detach((T)b, h)
        );

    public static bool UnregisterInvoker<T>() where T : BindableObject
        => invokers.Remove(typeof(T));


    static readonly BindableProperty ClickHandlerProperty = BindableProperty.CreateAttached(
        "ClickHandler",
        typeof(EventHandler),
        typeof(Navigate),
        null
    );

    static readonly BindableProperty NavigateGestureProperty = BindableProperty.CreateAttached(
        "NavigateGesture",
        typeof(TapGestureRecognizer),
        typeof(Navigate),
        null
    );


    public static readonly BindableProperty RouteProperty = BindableProperty.CreateAttached(
        "Route",
        typeof(string),
        typeof(Navigate),
        null,
        propertyChanged: OnRouteChanged
    );

    public static readonly BindableProperty RelativeNavigationProperty = BindableProperty.CreateAttached(
        "RelativeNavigation",
        typeof(bool),
        typeof(Navigate),
        true
    );

    public static readonly BindableProperty ParameterKeyProperty = BindableProperty.CreateAttached(
        "ParameterKey",
        typeof(string),
        typeof(Navigate),
        null
    );

    public static readonly BindableProperty ParameterValueProperty = BindableProperty.CreateAttached(
        "ParameterValue",
        typeof(object),
        typeof(Navigate),
        null
    );

    public static readonly BindableProperty ParametersProperty = BindableProperty.CreateAttached(
        "Parameters",
        typeof(NavigationParameters),
        typeof(Navigate),
        null
    );


    public static string? GetRoute(BindableObject bindable) => (string?)bindable.GetValue(RouteProperty);
    public static void SetRoute(BindableObject bindable, string? value) => bindable.SetValue(RouteProperty, value);

    public static bool GetRelativeNavigation(BindableObject bindable) => (bool)bindable.GetValue(RelativeNavigationProperty);
    public static void SetRelativeNavigation(BindableObject bindable, bool value) => bindable.SetValue(RelativeNavigationProperty, value);

    public static string? GetParameterKey(BindableObject bindable) => (string?)bindable.GetValue(ParameterKeyProperty);
    public static void SetParameterKey(BindableObject bindable, string? value) => bindable.SetValue(ParameterKeyProperty, value);

    public static object? GetParameterValue(BindableObject bindable) => bindable.GetValue(ParameterValueProperty);
    public static void SetParameterValue(BindableObject bindable, object? value) => bindable.SetValue(ParameterValueProperty, value);

    public static NavigationParameters? GetParameters(BindableObject bindable) => (NavigationParameters?)bindable.GetValue(ParametersProperty);
    public static void SetParameters(BindableObject bindable, NavigationParameters? value) => bindable.SetValue(ParametersProperty, value);


    static void OnRouteChanged(BindableObject bindable, object? _, object? newValue)
    {
        Detach(bindable);

        if (!String.IsNullOrWhiteSpace((string?)newValue))
            Attach(bindable);
    }


    static void Attach(BindableObject bindable)
    {
        EventHandler handler = async (_, _) => await ExecuteNavigation(bindable);
        bindable.SetValue(ClickHandlerProperty, handler);

        if (TryGetInvoker(bindable, out var invoker))
        {
            invoker.Attach(bindable, handler);
            return;
        }

        switch (bindable)
        {
            case Button button:
                button.Clicked += handler;
                return;

            case ToolbarItem toolbarItem:
                toolbarItem.Clicked += handler;
                return;

            case MenuItem menuItem:
                menuItem.Clicked += handler;
                return;

            case View view:
                var gesture = new TapGestureRecognizer();
                gesture.Tapped += (s, _) => handler(s, EventArgs.Empty);
                view.GestureRecognizers.Add(gesture);
                bindable.SetValue(NavigateGestureProperty, gesture);
                return;
        }

        throw new InvalidOperationException(
            $"Navigate.Route is not supported on {bindable.GetType().FullName}. " +
            "Targets must be a Button, MenuItem, ToolbarItem, a View, or a type registered via Navigate.RegisterInvoker<T>."
        );
    }


    static void Detach(BindableObject bindable)
    {
        if (bindable.GetValue(ClickHandlerProperty) is not EventHandler handler)
            return;

        if (TryGetInvoker(bindable, out var invoker))
        {
            invoker.Detach(bindable, handler);
        }
        else
        {
            switch (bindable)
            {
                case Button button:
                    button.Clicked -= handler;
                    break;

                case ToolbarItem toolbarItem:
                    toolbarItem.Clicked -= handler;
                    break;

                case MenuItem menuItem:
                    menuItem.Clicked -= handler;
                    break;
            }

            if (bindable is View view && bindable.GetValue(NavigateGestureProperty) is TapGestureRecognizer gesture)
            {
                view.GestureRecognizers.Remove(gesture);
                bindable.ClearValue(NavigateGestureProperty);
            }
        }

        bindable.RemoveBinding(ClickHandlerProperty);
        bindable.ClearValue(ClickHandlerProperty);
    }


    static bool TryGetInvoker(
        BindableObject bindable,
        out (Action<BindableObject, EventHandler> Attach, Action<BindableObject, EventHandler> Detach) invoker
    )
    {
        for (var type = bindable.GetType(); type != null && type != typeof(BindableObject); type = type.BaseType)
        {
            if (invokers.TryGetValue(type, out invoker))
                return true;
        }
        invoker = default;
        return false;
    }


    static Task ExecuteNavigation(BindableObject bindable)
    {
        var route = GetRoute(bindable);
        if (String.IsNullOrWhiteSpace(route))
            throw new InvalidOperationException("Navigate.Route must be set before navigation can occur");

        var services = (bindable as Element)?.Handler?.MauiContext?.Services
            ?? IPlatformApplication.Current?.Services
            ?? throw new InvalidOperationException("Unable to resolve MAUI services for XAML navigation");

        var navigator = services.GetRequiredService<INavigator>();
        var parameters = BuildParameters(bindable);
        return navigator.NavigateTo(route, GetRelativeNavigation(bindable), args: parameters);
    }


    static IEnumerable<(string Key, object Value)> BuildParameters(BindableObject bindable)
    {
        var args = new List<(string Key, object Value)>();

        var parameterKey = GetParameterKey(bindable);
        if (!String.IsNullOrWhiteSpace(parameterKey))
            args.Add((parameterKey, GetParameterValue(bindable)!));

        if (GetParameters(bindable) is { Count: > 0 } parameters)
        {
            foreach (var parameter in parameters)
            {
                if (String.IsNullOrWhiteSpace(parameter.Key))
                    throw new InvalidOperationException("XAML navigation parameters require a non-empty Key");

                parameter.BindingContext = bindable.BindingContext;
                args.Add((parameter.Key, parameter.Value!));
            }
        }
        return args;
    }
}
