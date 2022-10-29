using Avalonia.Controls;
using Avalonia;

namespace YoutubeCrossPlatformPlayer.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        }
    }
}