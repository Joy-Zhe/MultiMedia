﻿using System;
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
                // 显示选择的文件路径
                ToolTipService.SetToolTip(inputImagePath, file.Path);
                inputImagePath.Text = file.Path;
                inputPath = (file.Path);
                inputFile = (file);
            }
            else
            {
                // 用户取消选择文件，或者未选择任何文件
                ToolTipService.SetToolTip(inputImagePath, "No file selected");
                inputImagePath.Text = "No file selected";
            }
        }

        private void imgCompress_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void test_Click(object sender, RoutedEventArgs e)
        {
            DataLoader ds = new DataLoader();
            await ds.LoadImagePixels(inputFile);


            await ds.SaveImagePixels(ds.GetPixels(), outputFile, ds.GetImgWidth(), ds.GetImgHeight());
        }

        private async void selectOutputPath_Click(object sender, RoutedEventArgs e)
        {
            FileSavePicker filePicker = new FileSavePicker();

            filePicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            filePicker.SuggestedFileName = "newImage";
            filePicker.FileTypeChoices.Add("PNG Image", new List<string>() { ".png" });
        
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
    }
}
