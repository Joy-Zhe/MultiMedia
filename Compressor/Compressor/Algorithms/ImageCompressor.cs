using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Compressor.Dependencies;

namespace Compressor.Algorithms
{
    internal class ImageCompressor
    {
        DataLoader dataLoader;
        ImgData img;

/*        private void ImgPadding(ref ImgData img)
        {
            var paddingWidth = (8 - (img.GetWidth() % 8)) % 8;
            var paddingHeight = (8 - (img.GetHeight() % 8)) % 8;

            var newWidth = paddingWidth + img.GetWidth();
            var newHeight = paddingHeight + img.GetHeight();

            var newPixels = new byte[newWidth * newHeight * 4];
            var newBGRAData = new BGRA[newWidth * newHeight];
            var newYUVData = new YUV[newWidth * newHeight];

            // Copy existing image pixels into the new pixel array with padding
            for (uint h = 0; h < img.GetHeight(); h++)
            {
                for (uint w = 0; w < img.GetWidth(); w++)
                {
                    var oldIndex = (h * img.GetWidth() + w) * 4;
                    var newIndex = (h * newWidth + w) * 4;
                    Array.Copy(img.GetPixels(), oldIndex, newPixels, newIndex, 4);
                }
            }

            // Update ImgData with new dimensions and pixels
            img.SetWH(newWidth, newHeight); // Update width and height
            img.SetPixels(newPixels); // Update pixels and automatically update BGRAData and YUVData
        }*/
        
        private List<YUV[]> GetDCTBlocks() // each item in the list owns 8*8 YUV items
        {
            const int blockSize = 8;
            List<YUV[]> blocks = new List<YUV[]>();

            var width = img.GetWidth();
            var height = img.GetHeight();

            int blockWidthNumber = (int)Math.Ceiling(width / (double)blockSize);
            int blockHeightNumber = (int)Math.Ceiling(height / (double)blockSize);

            for (int blockY = 0; blockY < blockHeightNumber; blockY++)
            {
                for (int blockX = 0; blockX < blockWidthNumber; blockX++)
                {
                    YUV[] block = new YUV[blockSize * blockSize]; // 8*8 block
                    for (int y = 0; y < blockSize; y++)
                    {
                        for (int x = 0; x < blockSize; x++)
                        {
                            int pixelIndex = (blockY * blockSize + y) * (int)width + (blockX * blockSize + x);

                            // Check if the current pixel is outside the bounds of the image
                            if (blockY * blockSize + y >= height || blockX * blockSize + x >= width)
                            {
                                // padding with black pixels
                                block[y * blockSize + x] = new YUV(0, 0, 0);
                            }
                            else
                            {
                                // Copy the YUV data from the image to the block
                                block[y * blockSize + x] = img.YUVData[pixelIndex];
                            }
                        }
                    }
                    blocks.Add(block);
                }
            }

            return blocks;
        }
        public void Compress(ref ImgData img)
        {
            // padding and divide into blocks
            // ImgPadding(ref img);
            var DCTBlocks = GetDCTBlocks();
            List<double[,]> dctBlocks = new List<double[,]>();

            foreach (var block in DCTBlocks)
            {
                var dctCoef = ApplyDCT(block);
                dctBlocks.Add(dctCoef);
            }

            // Quantization
            List<double[,]> quantizedBlocks = new List<double[,]>();
            foreach (var dctBlock in dctBlocks)
            {
                var quantizedBlock = QuantizeBlock(dctBlock); // Implement QuantizeBlock to apply quantization on the DCT coefficients
                quantizedBlocks.Add(quantizedBlock);
            }
        }

        // apply DCT for blocks
        private double[,] ApplyDCT(YUV[] block)
        {
            int blockSize = 8;
            double[,] dctCoefficients = new double[blockSize, blockSize];

            for (int u = 0; u < blockSize; u++)
            {
                for (int v = 0; v < blockSize; v++)
                {
                    double sum = 0.0;
                    for (int x = 0; x < blockSize; x++)
                    {
                        for (int y = 0; y < blockSize; y++)
                        {
                            double pixelValue = block[x * blockSize + y].Y; // Assuming Y is the luminance component
                            sum += pixelValue *
                                   Math.Cos((2 * x + 1) * u * Math.PI / 16) *
                                   Math.Cos((2 * y + 1) * v * Math.PI / 16);
                        }
                    }
                    double cu = (u == 0) ? 1 / Math.Sqrt(2) : 1;
                    double cv = (v == 0) ? 1 / Math.Sqrt(2) : 1;
                    dctCoefficients[u, v] = 0.25 * cu * cv * sum;
                }
            }

            return dctCoefficients;
        }

        private double[,] QuantizeBlock(double[,] dctCoef)
        {
            // Standard JPEG Luminance Quantization Matrix for quality level around 50%
            int[,] quantizationMatrix = new int[,]
            {
                { 16, 11, 10, 16, 24, 40, 51, 61 },
                { 12, 12, 14, 19, 26, 58, 60, 55 },
                { 14, 13, 16, 24, 40, 57, 69, 56 },
                { 14, 17, 22, 29, 51, 87, 80, 62 },
                { 18, 22, 37, 56, 68, 109, 103, 77 },
                { 24, 35, 55, 64, 81, 104, 113, 92 },
                { 49, 64, 78, 87, 103, 121, 120, 101 },
                { 72, 92, 95, 98, 112, 100, 103, 99 }
            };

            double[,] quantizedCoefficients = new double[8, 8];

            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    quantizedCoefficients[i, j] = Math.Round(dctCoef[i, j] / quantizationMatrix[i, j]);
                }
            }

            return quantizedCoefficients;
        }
    }
}
