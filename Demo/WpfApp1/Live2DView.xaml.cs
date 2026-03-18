using Live2DCSharpSDK.WPF;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfApp1
{
    /// <summary>
    /// Live2DView.xaml 的交互逻辑
    /// </summary>
    public partial class Live2DView : UserControl
    {


        private Live2DImage _live2d;


        public Live2DView()
        {
            InitializeComponent();
            image.Source = _live2d = new Live2DImage();
            string file = Path.Combine(AppContext.BaseDirectory, "model", "FileReferences_Moc_0.model3.json");
            if (File.Exists(file))
            {
                _live2d.LoadModel(Path.Combine(AppContext.BaseDirectory, "model"), "FileReferences_Moc_0");
            }
        }


        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.MouseLeave += Live2DView_MouseLeave;
                window.PreviewMouseMove += Live2DView_PreviewMouseMove;
            }
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            _live2d.SetSize((int)(e.NewSize.Width * dpi.DpiScaleX), (int)(e.NewSize.Height * dpi.DpiScaleY));
        }



        Point _lastMousePos;

        private void UserControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _lastMousePos = e.GetPosition(this);

        }

        private void UserControl_MouseUp(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(this);
            if ((pos - _lastMousePos).Length < 5)
            {
                float x = (float)(pos.X * 2 / ActualWidth - 1);
                float y = (float)(1 - pos.Y * 2 / ActualHeight);
                _live2d.OnTap(x, y);
            }
        }


        protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
        {
            var pos = hitTestParameters.HitPoint;
            var center = new Point(ActualWidth / 2, ActualHeight / 2);
            float x = (float)((pos.X - center.X) * 2 / ActualWidth);
            float y = (float)((center.Y - pos.Y) * 2 / ActualHeight);
            if (_live2d.LApp.Live2dManager.HitAnyDrawable(x, y))
            {
                return new PointHitTestResult(this, pos);
            }
            else
            {
                return null!;
            }
        }



        private void Live2DView_MouseLeave(object sender, MouseEventArgs e)
        {
            _live2d.MouseDragged(0, 0);
        }



        private void Live2DView_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(this);
            var center = new Point(ActualWidth / 2, ActualHeight / 2);
            float x = (float)((pos.X - center.X) * 2 / ActualWidth);
            float y = (float)((center.Y - pos.Y) * 2 / ActualHeight);
            _live2d.MouseDragged(Math.Clamp(x, -0.6f, 0.6f), Math.Clamp(y, -0.6f, 0.6f));
        }


    }
}
