namespace Shiny;

public interface INavigator
{
    event EventHandler<NavigationEventArgs>? Navigating;
    event EventHandler<NavigatedEventArgs>? Navigated;

    /// <summary>
    /// Navigates to the specified route and passes the provided arguments to the target page or view model.
    /// </summary>
    /// <remarks>To receive the arguments passed via <paramref name="args"/>, the target page or view model
    /// must implement the <see cref="IQueryAttributable"/> interface.</remarks>
    /// <param name="route">The route to navigate to. This should be a valid route string recognized by the navigation system.</param>
    /// <param name="args">A collection of key-value pairs representing the arguments to pass to the target page or view model. Each key
    /// must be unique.</param>
    /// <returns>A task that represents the asynchronous navigation operation.</returns>
    Task NavigateTo(string route, params IEnumerable<(string Key, object Value)> args);


    /// <summary>
    /// Navigates to a view associated with the specified view model type.
    /// </summary>
    /// <remarks>This method allows for flexible navigation by enabling both view model configuration and the
    /// passing of additional arguments. Ensure that the specified view model type is properly registered and that any
    /// required arguments are provided.</remarks>
    /// <typeparam name="TViewModel">The type of the view model to navigate to. The view model must be registered in the navigation system.</typeparam>
    /// <param name="configure">An optional action to configure the view model before navigation. This can be used to set up properties or
    /// perform initialization.</param>
    /// <param name="args">A collection of key-value pairs representing arguments to pass to the view during navigation. Each key must be
    /// unique.</param>
    /// <returns>A task that represents the asynchronous navigation operation.</returns>
    Task NavigateTo<TViewModel>(
        Action<TViewModel>? configure = null, 
        params IEnumerable<(string Key, object Value)> args
    );
    
    
    /// <summary>
    /// Navigates to a view associated with the specified view model type.
    /// </summary>
    /// <remarks>This method allows for flexible navigation by enabling both view model configuration and the
    /// passing of additional arguments. Ensure that the specified view model type is properly registered and that any
    /// required arguments are provided.</remarks>
    /// <typeparam name="TViewModel">The type of the view model to navigate to. The view model must be registered in the navigation system.</typeparam>
    /// <param name="configure">An optional action to configure the view model before navigation. This can be used to set up properties or
    /// perform initialization.</param>
    /// <param name="args">A collection of key-value pairs representing arguments to pass to the view during navigation. Each key must be
    /// unique.</param>
    /// <returns>A task that represents the asynchronous navigation operation.</returns>
    Task SetRoot<TViewModel>(
        Action<TViewModel>? configure = null, 
        params IEnumerable<(string Key, object Value)> args
    );


    /// <summary>
    /// Returns to the root page regardless of how far up the stack you are
    /// </summary>
    /// <param name="args">A collection of key-value pairs representing parameters to pass to the target view or state.  Each key must be a
    /// unique identifier, and the value represents the associated data.</param>
    /// <returns></returns>
    Task PopToRoot(params IEnumerable<(string Key, object Value)> args);
    
    
    /// <summary>
    /// Navigates back to the previous view or state in the application, optionally passing parameters to the target.
    /// </summary>
    /// <remarks>The behavior of the navigation may depend on the application's navigation stack or state
    /// management. Ensure that the keys and values provided in <paramref name="args"/> are compatible with the target
    /// view or state.</remarks>
    /// <param name="args">A collection of key-value pairs representing parameters to pass to the target view or state.  Each key must be a
    /// unique identifier, and the value represents the associated data.</param>
    /// <returns>A task that represents the asynchronous operation of navigating back.</returns>
    Task GoBack(params IEnumerable<(string Key, object Value)> args);
    
    
    /// <summary>
    /// Navigates back to the previous view or state in the application, optionally passing parameters to the target.
    /// </summary>
    /// <remarks>The behavior of the navigation may depend on the application's navigation stack or state
    /// management. Ensure that the keys and values provided in <paramref name="args"/> are compatible with the target
    /// view or state.</remarks>
    /// <param name="backCount">Allows you to go back 1 or more pages in the navigation stack. Defaults to 1.</param>
    /// <param name="args">A collection of key-value pairs representing parameters to pass to the target view or state.  Each key must be a
    /// unique identifier, and the value represents the associated data.</param>
    /// <returns>A task that represents the asynchronous operation of navigating back.</returns>
    Task GoBack(int backCount = 1, params IEnumerable<(string Key, object Value)> args);

    
    /// <summary>
    /// Displays an alert dialog with a title, message, and an accept button.
    /// </summary>
    /// <remarks>This method is typically used to display informational messages or warnings to the user. The
    /// dialog will not close until the user interacts with the accept button.</remarks>
    /// <param name="title">The title of the alert dialog. Can be <see langword="null"/> to omit the title.</param>
    /// <param name="message">The message to display in the alert dialog. This parameter is required.</param>
    /// <param name="acceptText">The text for the accept button. Defaults to "OK" if not specified.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation of displaying the alert.</returns>
    Task Alert(string? title, string message, string acceptText = "OK");

    
    /// <summary>
    /// Displays a confirmation dialog with the specified title, message, and button text,  and returns the user's
    /// response.
    /// </summary>
    /// <param name="title">The title of the confirmation dialog. Can be <see langword="null"/> to omit the title.</param>
    /// <param name="message">The message displayed in the confirmation dialog. This parameter is required.</param>
    /// <param name="acceptText">The text for the confirmation button. Defaults to "Yes" if not specified.</param>
    /// <param name="cancelText">The text for the cancellation button. Defaults to "No" if not specified.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the user 
    /// selects the confirmation button; otherwise, <see langword="false"/>.</returns>
    Task<bool> Confirm(string? title, string message, string acceptText = "Yes", string cancelText = "No");
    
    /// <summary>
    /// Displays an action sheet dialog, allowing the user to choose from several options.
    /// </summary>
    /// <remarks>Action sheets are used to provide a set of choices related to a specific task or context. 
    /// On iOS, the <paramref name="destructionText"/> button will automatically appear in red.</remarks>
    /// <param name="title">The title of the action sheet. Can be <see langword="null"/> to omit the title.</param>
    /// <param name="cancelText">The text for the cancel button. Defaults to "Cancel" if not specified.</param>
    /// <param name="destructionText">The text for the destruction button (e.g., Delete). Defaults to "Remove" if not specified.</param>
    /// <param name="buttons">An array of additional option buttons for the user to select.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is the text of the button 
    /// selected by the user; otherwise, <see langword="null"/> if the operation was dismissed.</returns>
    Task<string?> Action(string? title, string cancelText = "Cancel", string destructionText = "Remove", params string[] buttons);
}