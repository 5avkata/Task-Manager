using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace TaskManager_WinUI;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed partial class MainWindow : Window
{
    private bool adjustingSize;
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint window);
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");
        uint dpi=GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this));
        int Scale(int value)=>(int)Math.Round(value*dpi/96d);
        AppWindow.Resize(new Windows.Graphics.SizeInt32(Scale(1280), Scale(820)));
        AppWindow.Changed += (_, args) =>
        {
            if (!args.DidSizeChange || adjustingSize) return;
            uint currentDpi=GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this));
            int minWidth=(int)Math.Round(980*currentDpi/96d),minHeight=(int)Math.Round(680*currentDpi/96d);
            int width = Math.Max(minWidth, AppWindow.Size.Width), height = Math.Max(minHeight, AppWindow.Size.Height);
            if (width == AppWindow.Size.Width && height == AppWindow.Size.Height) return;
            adjustingSize = true; AppWindow.Resize(new Windows.Graphics.SizeInt32(width, height)); adjustingSize = false;
        };

        RootFrame.Navigate(typeof(MainPage));
    }
}
