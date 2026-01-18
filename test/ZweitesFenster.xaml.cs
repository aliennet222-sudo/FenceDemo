using System.Windows;
using System.Windows.Input;

namespace FenceDemo
{
    public partial class ZweitesFenster : Window
    {
        public ZweitesFenster()
        {
            InitializeComponent();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }
    }
}