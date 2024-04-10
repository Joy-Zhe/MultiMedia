using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Compressor.Dependencies;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Compressor.Algorithms
{
    internal class ImageCompressor
    {
        DataLoader dataLoader = new DataLoader();
        int blockCount = 0;
        ImgData img;
        HuffmanCompression huffmanCompression = new HuffmanCompression();
        Dictionary<char, string> encodingDCTable = new Dictionary<char, string>();
        List<StringBuilder> encodedACblocks = new List<StringBuilder>();
        List<Dictionary<char, string>> encodingTableList = new List<Dictionary<char, string>>();
        StringBuilder encodedDCPM = new StringBuilder();

        public async Task Compress(StorageFile input, StorageFile saveimg)
        {
            await dataLoader.LoadImagePixels(input);
            img = dataLoader.GetImgData();
            DoCompress(ref img);
            await encodingImage(saveimg);
        }

        private List<YUV[]> GetDCTBlocks() // each item in the list owns 8*8 YUV items
        {
            const int blockSize = 8;
            blockCount = 0;
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

            blockCount = blocks.Count;

            return blocks;
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
                            double pixelValue = block[x * blockSize + y].Y; // get the luminance as pixel value
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

        private List<int> ZigzagOrdering(double[,] block)
        {
            // char in .NET is 16-bit
            int[] order = new int[]
            {
                0, 1, 8, 16, 9, 2, 3, 10,
                17, 24, 32, 25, 18, 11, 4, 5,
                12, 19, 26, 33, 40, 48, 41, 34,
                27, 20, 13, 6, 7, 14, 21, 28,
                35, 42, 49, 56, 57, 50, 43, 36,
                29, 22, 15, 23, 30, 37, 44, 51,
                58, 59, 52, 45, 38, 31, 39, 46,
                53, 60, 61, 54, 47, 55, 62, 63
            };
            List<int> zigzag = new List<int>();

            foreach (var index in order)
            {
                int i = index / 8;
                int j = index % 8;
                zigzag.Add((char)block[i, j]);
            }

            return zigzag;
        }

        private string ZigzagToString(List<int> zigzag)
        {
            return string.Concat(zigzag.Select(i => i.ToString()));
            // return new string(zigzag.ToArray());
        }

        private void WriteBitString(BinaryWriter writer, string bitString)
        {
            int offset = 0;
            while (offset + 8 <= bitString.Length)
            {
                writer.Write(Convert.ToByte(bitString.Substring(offset, 8), 2));
                offset += 8;
            }
            if (offset < bitString.Length)
            {
                writer.Write(Convert.ToByte(bitString.Substring(offset).PadRight(8, '0'), 2));
            }
        }

        private async Task encodingImage(StorageFile saveImage)
        {
            if (saveImage != null)
            {
                using (var stream = await saveImage.OpenAsync(FileAccessMode.ReadWrite))
                {
                    using (var outputStream = stream.AsStreamForWrite())
                    {
                        using (var writer = new BinaryWriter(outputStream))
                        {
                            ushort n_w = (ushort)img.GetWidth();
                            writer.Write(n_w); // 写入图像宽度
                            writer.Write((ushort)img.GetHeight()); // 写入图像高度
                            // 先写入DC差分信息的huffman结果
                            // Y
                            // Y header
                            writer.Write((ushort)encodingDCTable.Count); // DCPM字典大小
                            writer.Write((char)'Y'); // block type
                            writer.Write((byte)0); // 0 stand for DC
                            int addBitsLen = (8 - (encodedDCPM.Length - 1) % 8 + 1);
                            writer.Write((byte)addBitsLen); // 补齐至整个字节
                            writer.Write((ushort)(encodedDCPM.Length / 8 + (addBitsLen > 0 ? 1 : 0))); // 数据流占用的字节数
                            // Y dictionary
                            foreach (var entry in encodingDCTable)
                            {
                                writer.Write(entry.Key);
                                writer.Write(entry.Value);
                            }
                            // Y data
                            WriteBitString(writer, encodedDCPM.ToString());
                            // U
                            // U header
                            writer.Write((ushort)encodingDCTable.Count); // DCPM字典大小
                            writer.Write((char)'U'); // block type
                            writer.Write((byte)0); // 0 stand for DC
                            addBitsLen = (8 - (encodedDCPM.Length - 1) % 8 + 1);
                            writer.Write((byte)addBitsLen); // 补齐至整个字节
                            writer.Write((ushort)(encodedDCPM.Length / 8 + (addBitsLen > 0 ? 1 : 0))); // 数据流占用的字节数
                            // U dictionary
                            foreach (var entry in encodingDCTable)
                            {
                                writer.Write(entry.Key);
                                writer.Write(entry.Value);
                            }
                            // U data
                            WriteBitString(writer, encodedDCPM.ToString());
                            // V
                            // V header
                            writer.Write((ushort)encodingDCTable.Count); // DCPM字典大小
                            writer.Write((char)'V'); // block type
                            writer.Write((byte)0); // 0 stand for DC
                            addBitsLen = (8 - (encodedDCPM.Length - 1) % 8 + 1);
                            writer.Write((byte)addBitsLen); // 补齐至整个字节
                            writer.Write((ushort)(encodedDCPM.Length / 8 + (addBitsLen > 0 ? 1 : 0))); // 数据流占用的字节数
                            // V dictionary
                            foreach (var entry in encodingDCTable)
                            {
                                writer.Write(entry.Key);
                                writer.Write(entry.Value);
                            }
                            // V data
                            WriteBitString(writer, encodedDCPM.ToString());
                            // 写入每个块的AC huffman结果
                            for (int i = 0; i < blockCount; i++)
                            {
                                // Y
                                // Y header
                                writer.Write((ushort)encodingTableList[i].Count); // DCPM字典大小
                                writer.Write((char)'Y'); // block type
                                writer.Write((byte)1); // 1 stand for AC
                                addBitsLen = (8 - (encodedACblocks[i].Length - 1) % 8 + 1);
                                writer.Write((byte)addBitsLen); // 补齐至整个字节
                                writer.Write((ushort)(encodedACblocks[i].Length / 8 + (addBitsLen > 0 ? 1 : 0))); // 数据流占用的字节数
                                                                                                           // Y dictionary
                                foreach (var entry in encodingTableList[i])
                                {
                                    writer.Write(entry.Key);
                                    writer.Write(entry.Value);
                                }
                                // Y data
                                WriteBitString(writer, encodedACblocks[i].ToString());
                                // U
                                // U header
                                writer.Write((ushort)encodingTableList[i].Count); // DCPM字典大小
                                writer.Write((char)'U'); // block type
                                writer.Write((byte)1); // 1 stand for AC
                                addBitsLen = (8 - (encodedACblocks[i].Length - 1) % 8 + 1);
                                writer.Write((byte)addBitsLen); // 补齐至整个字节
                                writer.Write((ushort)(encodedACblocks[i].Length / 8 + (addBitsLen > 0 ? 1 : 0))); // 数据流占用的字节数
                                                                                                           // U dictionary
                                foreach (var entry in encodingTableList[i])
                                {
                                    writer.Write(entry.Key);
                                    writer.Write(entry.Value);
                                }
                                // U data
                                WriteBitString(writer, encodedACblocks[i].ToString());
                                // V
                                // V header
                                writer.Write((ushort)encodingTableList[i].Count); // DCPM字典大小
                                writer.Write((char)'V'); // block type
                                writer.Write((byte)0); // 1 stand for AC
                                addBitsLen = (8 - (encodedACblocks[i].Length - 1) % 8 + 1);
                                writer.Write((byte)addBitsLen); // 补齐至整个字节
                                writer.Write((ushort)(encodedACblocks[i].Length / 8 + (addBitsLen > 0 ? 1 : 0))); // 数据流占用的字节数
                                                                                                           // V dictionary
                                foreach (var entry in encodingTableList[i])
                                {
                                    writer.Write(entry.Key);
                                    writer.Write(entry.Value);
                                }
                                // V data
                                WriteBitString(writer, encodedACblocks[i].ToString());
                            }
                        }
                    }
                } 
            }
        }

        private void DoCompress(ref ImgData img)
        {
            // padding and divide into blocks
            // ImgPadding(ref img);
            var DCTBlocks = GetDCTBlocks();
            List<double[,]> dctBlocks = new List<double[,]>();

            // DCT
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

            int prevDC = 0;
            // List<char> DCPMs = new List<char>(); // after get all the DCPMs, apply Huffman to the list
            // List<char> AC_RLE = new List<char>(); // AC_RLE need apply Huffman to each block
            string AC_RLE = string.Empty;
            string DCPMs = string.Empty;
            encodingDCTable.Clear();
            encodedACblocks.Clear();
            encodingTableList.Clear();
            encodedDCPM.Clear();
            foreach (var block in quantizedBlocks)
            {
                AC_RLE = string.Empty;
                var zigzag = ZigzagOrdering(block);
                var zigzagString = ZigzagToString(zigzag);
                // DCPM
                int DCPM = zigzag[0] - prevDC;
                prevDC = zigzag[0];
                if (DCPM < 0) // mark minus
                {
                    DCPM |= 0x8000;
                }
                // DCPMs.Add((char)DCPM);
                DCPMs += (char)DCPM;

                // AC part, RLE
                int zeroCount = 0;
                for (int i = 1; i < 64; i++)
                {
                    if (zigzag[i] == 0)
                    {
                        zeroCount++;
                    }
                    else
                    {
                        int CombinedPair = (zeroCount << 8) | (zigzag[i] & 0xff);
                        if (zigzag[i] < 0) // mark minus
                        {
                            CombinedPair |= 0x80; // 0b1000 0000
                        }
                        // AC_RLE.Add((char)CombinedPair);
                        AC_RLE += (char)CombinedPair;
                        zeroCount = 0; // reset zeroCount
                    }
                }

                // AC_RLE.Add((char)0); // pair (0, 0) stand for end
                AC_RLE += (char)0;

                // zigzag[0] is the DC Coef, zigzag[1]-zigzag[63] are the AC Coef
                var encodedACNums = huffmanCompression.HuffmanComp(AC_RLE);
                encodedACblocks.Add(encodedACNums);
                encodingTableList.Add(huffmanCompression.encodingTable);
            }

            // after processing all the blocks, apply Huffman compressing
            var encodedDCNums = huffmanCompression.HuffmanComp(DCPMs);
            encodingDCTable = huffmanCompression.encodingTable;
        }
    }
}
