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
using Windows.Storage.Pickers;
using Windows.Storage;
using Compressor.Algorithms;


namespace Compressor
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class TextPage : Page
    {
        String inputPath = null;
        /*String[] outputPaths = null;*/
        String outputPath = null;
        String outputFileName = "output.huf";

        StorageFile inputFile = null;
        StorageFolder outputFolder = null;
        public TextPage()
        {
            this.InitializeComponent();
        }

        private async void SelectPath_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker filePicker = new FileOpenPicker();
            filePicker.ViewMode = PickerViewMode.List;
            filePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            filePicker.FileTypeFilter.Add("*");

            StorageFile file = await filePicker.PickSingleFileAsync();
            if (file != null)
            {
                // 显示选择的文件路径
                ToolTipService.SetToolTip(showInputPath, file.Path);
                showInputPath.Text = file.Path;
                inputPath = (file.Path);
                inputFile = (file);
            }
            else
            {
                // 用户取消选择文件，或者未选择任何文件
                ToolTipService.SetToolTip(showInputPath, "No file selected");
                showInputPath.Text = "No file selected";
            }
        }

        private async void SelectOutputPathBtn_Click(object sender, RoutedEventArgs e)
        {
            FolderPicker pathPicker = new FolderPicker();
            pathPicker.ViewMode = PickerViewMode.List;
            pathPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            pathPicker.FileTypeFilter.Add("*");

            StorageFolder path = await pathPicker.PickSingleFolderAsync();
            if(path != null)
            {
                ToolTipService.SetToolTip(showOutputPath, path.Path);
                showOutputPath.Text = path.Path;
                outputPath = path.Path;
                outputFolder = path;
            }
            else
            {
                ToolTipService.SetToolTip(showOutputPath, "No path selected");
                showOutputPath.Text = "No path selected";
            }
        }

        public bool IsValidFileName(string fileName)
        {
            if (fileName == null || fileName.Length == 0) { return false; }
            char[] invalidSymbols = Path.GetInvalidFileNameChars();
            if (fileName.Intersect(invalidSymbols).Any()) // include invalid symbol(s)
            {
                return false;
            }
            if (fileName.Length > 260) // too long
            {
                return false;
            }
            return true;
        }

        private async void huffman_Click(object sender, RoutedEventArgs e)
        {
            HuffmanCompression huffmanCompression = new HuffmanCompression();

            await huffmanCompression.Compress(inputFile, outputFolder, outputFileName);
        }

        private void SelectOutputNameBtn_Click(object sender, RoutedEventArgs e)
        {
            if(IsValidFileName(outputName.Text))
            {
                outputFileName = outputName.Text + ".huf";
            }
            else
            {
                outputFileName = "output.huf";
            }
        }

        private async void TextDeCompress_Click(object sender, RoutedEventArgs e)
        {
            HuffmanCompression huffmanCompression = new HuffmanCompression();

            await huffmanCompression.DeCompress(inputFile, outputFolder, outputFileName);
        }
    }
}
