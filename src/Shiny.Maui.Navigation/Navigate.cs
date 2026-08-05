namespace Shiny;

/// <summary>How a <see cref="Navigate.ViewModel"/> target should be reached</summary>
public enum NavigateMode
{
    /// <summary>Push onto the active stack (default)</summary>
    Push,

    /// <summary>Replace the active stack with this page as its new root</summary>
    Root,

    /// <summary>Push onto the modal stack</summary>
    Modal,

    /// <summary>Switch to the tab hosting this ViewModel</summary>
    Tab
}


/// <summary>A navigation that needs no target ViewModel</summary>
public enum NavigateAction
{
    None,
    GoBack,
    PopToRoot,
    PopModal,
    OpenFlyout,
    CloseFlyout,
    ToggleFlyout
}


/// <summary>
/// Attached properties for navigating straight from XAML, without a command on the ViewModel.
/// Works on <see cref="Button"/>, <see cref="MenuItem"/>, <see cref="ToolbarItem"/>, any
/// <see cref="View"/> (via tap), and anything registered through
/// <see cref="RegisterInvoker{T}"/>.
/// </summary>
/// <example>
/// <code>
/// &lt;Button Text="Details" shiny:Navigate.ViewModel="{x:Type vm:DetailViewModel}" /&gt;
/// &lt;Button Text="Close"   shiny:Navigate.Action="GoBack" /&gt;
/// </code>
/// </example>
public static class Navigate
{
    static readonly Dictionary<Type, (Action<BindableObject, EventHandler> Attach, Action<BindableObject, EventHandler> Detach)> invokers = new();

    /// <summary>
    /// Teaches the attached properties how to hook a control type they don't know about.
    /// </summary>
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


    /// <summary>The ViewModel type to navigate to</summary>
    public static readonly BindableProperty ViewModelProperty = BindableProperty.CreateAttached(
        "ViewModel",
        typeof(Type),
        typeof(Navigate),
        null,
        propertyChanged: OnTriggerChanged
    );

    /// <summary>How to reach the <see cref="ViewModelProperty"/> target. Defaults to Push.</summary>
    public static readonly BindableProperty ModeProperty = BindableProperty.CreateAttached(
        "Mode",
        typeof(NavigateMode),
        typeof(Navigate),
        NavigateMode.Push
    );

    /// <summary>A navigation that needs no target - back, pop to root, flyout open/close</summary>
    public static readonly BindableProperty ActionProperty = BindableProperty.CreateAttached(
        "Action",
        typeof(NavigateAction),
        typeof(Navigate),
        NavigateAction.None,
        propertyChanged: OnTriggerChanged
    );


    public static Type? GetViewModel(BindableObject bindable) => (Type?)bindable.GetValue(ViewModelProperty);
    public static void SetViewModel(BindableObject bindable, Type? value) => bindable.SetValue(ViewModelProperty, value);

    public static NavigateMode GetMode(BindableObject bindable) => (NavigateMode)bindable.GetValue(ModeProperty);
    public static void SetMode(BindableObject bindable, NavigateMode value) => bindable.SetValue(ModeProperty, value);

    public static NavigateAction GetAction(BindableObject bindable) => (NavigateAction)bindable.GetValue(ActionProperty);
    public static void SetAction(BindableObject bindable, NavigateAction value) => bindable.SetValue(ActionProperty, value);


    static void OnTriggerChanged(BindableObject bindable, object? _, object? __)
    {
        Detach(bindable);

        if (GetViewModel(bindable) != null || GetAction(bindable) != NavigateAction.None)
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
            $"Navigate is not supported on {bindable.GetType().FullName}. " +
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
        var services = (bindable as Element)?.Handler?.MauiContext?.Services
            ?? IPlatformApplication.Current?.Services
            ?? throw new InvalidOperationException("Unable to resolve MAUI services for XAML navigation");

        var navigator = services.GetRequiredService<INavigator>();

        var action = GetAction(bindable);
        if (action != NavigateAction.None)
        {
            return action switch
            {
                NavigateAction.GoBack => navigator.GoBack(),
                NavigateAction.PopToRoot => navigator.PopToRoot(),
                NavigateAction.PopModal => navigator.PopModal(),
                NavigateAction.OpenFlyout => navigator.OpenFlyout(),
                NavigateAction.CloseFlyout => navigator.CloseFlyout(),
                NavigateAction.ToggleFlyout => ToggleFlyout(navigator),
                _ => Task.CompletedTask
            };
        }

        var viewModelType = GetViewModel(bindable)
            ?? throw new InvalidOperationException("Navigate.ViewModel or Navigate.Action must be set before navigation can occur");

        return GetMode(bindable) switch
        {
            NavigateMode.Root => navigator.NavigateToRoot(viewModelType),
            NavigateMode.Modal => navigator.PushModal(viewModelType),
            NavigateMode.Tab => navigator.SelectTab(viewModelType),
            _ => navigator.NavigateTo(viewModelType)
        };
    }


    static Task ToggleFlyout(INavigator navigator)
    {
        if (navigator is not ShinyNavigator shiny)
            return navigator.OpenFlyout();

        return shiny.Host.Flyout?.IsPresented == true
            ? navigator.CloseFlyout()
            : navigator.OpenFlyout();
    }
}
