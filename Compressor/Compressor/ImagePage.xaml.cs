using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Streams;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace Compressor
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class ImagePage : Page
    {
        public ImagePage()
        {
            this.InitializeComponent();
        }

        private async void ImageDrop(object sender, DragEventArgs e)
        {
            // 检查拖入的数据是否包含文件
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                // 获取拖入的文件
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Count > 0 && items[0] is StorageFile)
                {
                    // 获取拖入的第一个文件
                    var file = items[0] as StorageFile;
                    if (file.ContentType.StartsWith("image/"))
                    {
                        // 加载并显示图像文件
                        BitmapImage bitmapImage = new BitmapImage();
                        using (IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.Read))
                        {
                            await bitmapImage.SetSourceAsync(fileStream);
                        }
                        previewImage.Source = bitmapImage;
                    }
                }
            }
        }

        private void selectInputImage_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
