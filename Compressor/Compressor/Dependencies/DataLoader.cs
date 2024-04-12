using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace Compressor.Dependencies
{
    internal class DataLoader
    {
        public byte[] GetPixels()
        {
            return imageData.GetPixels();
        }

        public uint GetImgWidth()
        {
            return imageData.GetWidth();
        }

        public uint GetImgHeight()
        {
            return imageData.GetHeight();
        }

        private ImgData imageData = new ImgData();

        public ImgData GetImgData()
        {
            return imageData;
        }
        
        public async Task<(BitmapPixelFormat, BitmapAlphaMode)> GetImageFormat(StorageFile imgFile)
        {
            using (IRandomAccessStream stream = await imgFile.OpenAsync(FileAccessMode.Read))
            {
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

                BitmapPixelFormat pixelFormat = decoder.BitmapPixelFormat;
                BitmapAlphaMode alphaMode = decoder.BitmapAlphaMode;

                return (pixelFormat, alphaMode);
            }
        }

        public async Task LoadImagePixels(StorageFile inputfile)
        {
            try
            {
                using (IRandomAccessStream stream = await inputfile.OpenAsync(FileAccessMode.Read))
                {
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

                    PixelDataProvider pixelData = await decoder.GetPixelDataAsync();

                    byte[] pixels = pixelData.DetachPixelData();

                    uint width = decoder.PixelWidth;
                    uint height = decoder.PixelHeight;

                    imageData.SetWH(width, height);
                    imageData.SetPixels(pixels);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading image pixels: {ex.Message}");
            }
        }

        public async Task SaveImagePixels(BGRA[] BGRA_pixel, StorageFile imageFile, uint width, uint height)
        {
            byte[] pixels = new byte[BGRA_pixel.Length * 4];
            for (int i = 0; i < BGRA_pixel.Length; i++)
            {
                int index = i * 4;
                pixels[index] = BGRA_pixel[i].blue;
                pixels[index + 1] = BGRA_pixel[i].green;
                pixels[index + 2] = BGRA_pixel[i].red;
                pixels[index + 3] = BGRA_pixel[i].alpha;
            }

            WriteableBitmap bitmap = new WriteableBitmap((int)width, (int)height);
            using (Stream stream = bitmap.PixelBuffer.AsStream())
            {
                await stream.WriteAsync(pixels, 0, pixels.Length);
            }

            using (IRandomAccessStream fileStream = await imageFile.OpenAsync(FileAccessMode.ReadWrite))
            {
                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, fileStream);
                encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels);
                await encoder.FlushAsync();
            }
        }
    }
}
