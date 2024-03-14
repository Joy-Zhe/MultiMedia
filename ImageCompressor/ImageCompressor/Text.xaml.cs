using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ImageCompressor;

public partial class Text : Page
{

    private HuffmanCompression _huffmanCompression = new HuffmanCompression();
    private string[]? _txtFiles;
    public Text()
    {
        InitializeComponent();
    }
    
    

    private void TextCompress(object sender, RoutedEventArgs e)
    {
        string inputFilePath = _txtFiles[0];
        string outputFilePath = "output.txt";
        
        _huffmanCompression.Compress(inputFilePath, outputFilePath);
    }

    private void TextSave(object sender, RoutedEventArgs e)
    {
        throw new NotImplementedException();
    }

    private void TxtDragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
    }

    private void TxtDrop(object sender, DragEventArgs e)
    {
        if(e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            _txtFiles = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (string file in _txtFiles)
            {
                if (Path.GetExtension(file).Equals(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show($"拖放的txt文件路径: {file}");
                }
                else
                {
                    MessageBox.Show($"只能接受txt文件！");
                }
            }
        }
    }
}