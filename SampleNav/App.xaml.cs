namespace SampleNav;

// No window/root-page wiring here - ShinyApplication hands MAUI the page tree that
// UseShinyNavigation built in MauiProgram.
public partial class App : ShinyApplication
{
    public App() => this.InitializeComponent();
}
