using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Compressor
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            MainFrame.Navigate(typeof(HomePage));
            NavView.ItemInvoked += NavViewItemInvoked;
        }

        private void NavViewItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                MainFrame.Navigate(typeof (SettingsPage));
            }
            else
            {
                switch (args.InvokedItemContainer.Tag.ToString())
                {
                    case "home":
                        MainFrame.Navigate(typeof (HomePage));
                        break;
                    case "image":
                        MainFrame.Navigate(typeof (ImagePage));
                        break;
                    case "imageDeComp":
                        MainFrame.Navigate(typeof (ImageDeCompress)); 
                        break;
                    case "text":
                        MainFrame.Navigate(typeof (TextPage));
                        break;
                    default:
                        break;
                }
            }
        }
    }
}