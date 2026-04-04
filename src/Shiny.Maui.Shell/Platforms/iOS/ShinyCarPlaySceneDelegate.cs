using CarPlay;
using Foundation;
using UIKit;

namespace Shiny;

[Register("ShinyCarPlaySceneDelegate")]
public abstract class ShinyCarPlaySceneDelegate : CPTemplateApplicationSceneDelegate
{
    protected CPInterfaceController? InterfaceController { get; private set; }

    public override void DidConnect(CPTemplateApplicationScene templateApplicationScene, CPInterfaceController interfaceController)
    {
        this.InterfaceController = interfaceController;
        this.GetCarPlayContext()?.SetConnected(true);
        this.OnCarPlayConnected(templateApplicationScene, interfaceController);
    }

    public override void DidDisconnect(CPTemplateApplicationScene templateApplicationScene, CPInterfaceController interfaceController)
    {
        this.OnCarPlayDisconnected(templateApplicationScene, interfaceController);
        this.GetCarPlayContext()?.SetConnected(false);
        this.InterfaceController = null;
    }

    protected abstract void OnCarPlayConnected(CPTemplateApplicationScene scene, CPInterfaceController interfaceController);

    protected virtual void OnCarPlayDisconnected(CPTemplateApplicationScene scene, CPInterfaceController interfaceController) { }

    CarPlayContext? GetCarPlayContext()
        => IPlatformApplication.Current?.Services?.GetService<ICarPlayContext>() as CarPlayContext;
}
