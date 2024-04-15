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
using Windows.Storage.Pickers;
using Windows.UI.ViewManagement;
using Compressor.Dependencies;
using Compressor.Algorithms;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace Compressor
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class ImagePage : Page
    {
        string inputPath = null;
        string outputPath = null;
        StorageFile inputFile = null;
        StorageFile outputFile = null;

        //ImageCompressor compressor = new ImageCompressor();

        public ImagePage()
        {
            this.InitializeComponent();
        }

        private async void selectInputImage_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker filePicker = new FileOpenPicker();
            filePicker.ViewMode = PickerViewMode.List;
            filePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            filePicker.FileTypeFilter.Add("*");

            StorageFile file = await filePicker.PickSingleFileAsync();
            if (file != null)
            {
                // file path
                ToolTipService.SetToolTip(inputImagePath, file.Path);
                inputImagePath.Text = file.Path;
                inputPath = (file.Path);
                inputFile = (file);

                // preview
                using (Windows.Storage.Streams.IRandomAccessStream fileStream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read))
                {
                    // Set the image source to the selected bitmap
                    BitmapImage bitmapImage = new BitmapImage();
                    await bitmapImage.SetSourceAsync(fileStream);
                    previewImage.Source = bitmapImage;
                }
            }
            else
            {
                // no file selected
                ToolTipService.SetToolTip(inputImagePath, "No file selected");
                inputImagePath.Text = "No file selected";
            }
        }

        private async void imgCompress_Click(object sender, RoutedEventArgs e)
        {
            ImageCompressor compressor = new ImageCompressor();
            await compressor.Compress(inputFile, outputFile, 0x00);
        }

        private async void selectOutputPath_Click(object sender, RoutedEventArgs e)
        {
            FileSavePicker filePicker = new FileSavePicker();

            filePicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            filePicker.SuggestedFileName = "newImage";
            filePicker.FileTypeChoices.Add("Compressed Image File", new List<string>() { ".hufimg" });

            var file = await filePicker.PickSaveFileAsync();

            if (file != null)
            {
                ToolTipService.SetToolTip(outputImagePath, file.Path);
                outputImagePath.Text = file.Path;
                outputPath = (file.Path);
                outputFile = (file);
            }
            else
            {
                ToolTipService.SetToolTip(outputImagePath, "No file created");
                outputImagePath.Text = "No file created";
            }
        }

        private async void ImgDeCompress_OnClick(object sender, RoutedEventArgs e)
        {
            ImageCompressor compressor = new ImageCompressor();
            await compressor.DeCompress(inputFile, outputFile);
        }

        private async void ImgCompress420_OnClick(object sender, RoutedEventArgs e)
        {
            ImageCompressor compressor = new ImageCompressor();
            await compressor.Compress(inputFile, outputFile, 0x01);
        }

    }
}
