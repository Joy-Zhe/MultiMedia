using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Compressor.Dependencies;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls.Primitives;

namespace Compressor.Algorithms
{
    internal class ImageCompressor
    {
        DataLoader dataLoader = new DataLoader();
        int blockCount = 0;
        ImgData img;
        HuffmanCompression _huffmanCompression = new HuffmanCompression();
        Dictionary<char, string> _encodingDcTable = new Dictionary<char, string>();
        Dictionary<char, string> _encodingAcTable = new Dictionary<char, string>();
        private List<String> AC_RLE_List = new List<string>();
        List<StringBuilder> _encodedAcBlocks = new List<StringBuilder>();
        //List<Dictionary<char, string>> encodingTableList = new List<Dictionary<char, string>>();
        StringBuilder encodedDCPM = new StringBuilder();

        public async Task Compress(StorageFile input, StorageFile output)
        {
            await dataLoader.LoadImagePixels(input);
            img = dataLoader.GetImgData();
            DoCompress(ref img);
            await EncodingImage(output);
        }

        public async Task DeCompress(StorageFile input, StorageFile output)
        {
            ImgData DecodedImage = await DecodingImage(input);
            dataLoader.SaveImagePixels(DecodedImage.BGRAData, output, DecodedImage.GetWidth(), DecodedImage.GetHeight());
        }

        private string DecodeBits(byte[] bitStream, Dictionary<string, char> table, int additionalBitsLen)
        {
            StringBuilder bitString = new StringBuilder();
            StringBuilder result = new StringBuilder();

            foreach (var b in bitStream)
            {
                string bit = Convert.ToString(b, 2).PadLeft(8, '0');
                bitString.Append(bit);
            }

            bitString.Remove(bitString.Length - additionalBitsLen, additionalBitsLen);
            while (bitString.Length > 0)
            {
                StringBuilder code = new StringBuilder();
                while (bitString.Length > 0)
                {
                    code.Append(bitString[0]); // read one bit
                    bitString.Remove(0, 1); // remove the bit read
                    if (table.ContainsKey(code.ToString()))
                    {
                        result.Append(table[code.ToString()]);
                        code.Clear();
                        break;
                    }
                }
            }

            return result.ToString();
        }

        private List<int> ReadDC(BinaryReader reader, Dictionary<string, char> dcHuffmanTable)
        {
            List<int> dcList = new List<int>();
            
            // DCPM
            char type = reader.ReadChar();
            // AC/DC
            byte if_AC = reader.ReadByte(); // DC:0 AC:1
            // add bits len
            byte additionalBitsLen = reader.ReadByte();
            // bits len(2B)
            ushort length = reader.ReadUInt16();
            byte[] compressedData = reader.ReadBytes(length);
            string data = DecodeBits(compressedData, dcHuffmanTable, additionalBitsLen);
            int now_dcpm = 0;
            dcList.Clear();
            // 重新构建dc
            foreach (var dcpm in data)
            {
                int delta = 0;
                delta |= dcpm & 0x7fff;
                if ((dcpm & 0x8000) != 0)
                {
                    delta = -delta;
                }
                now_dcpm += delta;
                dcList.Add(now_dcpm);
            }

            return dcList;
        }

        private int[] ReadAC(BinaryReader reader, Dictionary<string, char> acHuffmanTable)
        {
            List<int> ans = new List<int>();

            char type = reader.ReadChar();
            // AC/DC
            byte if_AC = reader.ReadByte(); // DC:0 AC:1
            // add bits len
            byte additionalBitsLen = reader.ReadByte();
            // bits len(2B)
            ushort length = reader.ReadUInt16();
            byte[] compressedData = reader.ReadBytes(length);
            string data = DecodeBits(compressedData, acHuffmanTable, additionalBitsLen);

            foreach (var c in data) // (zero_cnt, value)
            {
                int zero_count = (int)(c >> 8); 
                int value = (int)(c & 0x7F);
                if ((value & 0x8000) == 1)
                {
                    value = -value;
                }

                for (int i = 0; i < zero_count; i++)
                {
                    ans.Add(0);
                }

                ans.Add(value);

                if (value == 0 && zero_count == 0) //EOB(0,0)
                {
                    break;
                }
            }

            var now_cnt = ans.Count;
            if (now_cnt < 63)
            {
                for (int i = 0; i < 63 - now_cnt; i++)
                {
                    ans.Add(0);
                }
            }

            return ans.ToArray();
        }
        private async Task<ImgData> DecodingImage(StorageFile file)
        {
            if (file != null)
            {
                ImgData decodedImage = new ImgData();

                using (var stream = await file.OpenAsync(FileAccessMode.Read)) // Readonly
                {
                    using (var inputStream = stream.AsStreamForRead())
                    {
                        using (var reader = new BinaryReader(inputStream))
                        {
                            ushort width = reader.ReadUInt16();
                            ushort height = reader.ReadUInt16();
                            decodedImage.SetWH(width, height);
                            byte colorEncoding = reader.ReadByte();
                            int dcTableSize = reader.ReadInt32();
                            int acTableSize = reader.ReadInt32();
                            Dictionary<string, char> dcHuffmanTable = new Dictionary<string, char>();
                            for (int i = 0; i < dcTableSize; i++)
                            {
                                char key = reader.ReadChar();
                                string value = reader.ReadString();
                                dcHuffmanTable[value] = key;
                            }

                            Dictionary<string, char> acHuffmaTable = new Dictionary<string, char>();
                            for (int i = 0; i < acTableSize; i++)
                            {
                                char key = reader.ReadChar();
                                string value = reader.ReadString();
                                acHuffmaTable[value] = key;
                            }
                            // 计算block数量
                            const int blockSize = 8;
                            int blockWidthNumber = (int)Math.Ceiling(width / (double)blockSize);
                            int blockHeightNumber = (int)Math.Ceiling(height / (double)blockSize);
                            int blockNumber = blockHeightNumber * blockWidthNumber;

                            // read dc
                            List<int> dcListY = ReadDC(reader, dcHuffmanTable);
                            List<int> dcListU = ReadDC(reader, dcHuffmanTable);
                            List<int> dcListV = ReadDC(reader, dcHuffmanTable);

                            //var test = reader.ReadChar();

                            List<int[]> acListY = new List<int[]>();
                            List<int[]> acListU = new List<int[]>();
                            List<int[]> acListV = new List<int[]>();

                            // AC blocks, 按block进行索引
                            for (int i = 0; i < blockNumber; i++)
                            {
                                acListY.Add(ReadAC(reader, acHuffmaTable));
                                acListU.Add(ReadAC(reader, acHuffmaTable));
                                acListV.Add(ReadAC(reader, acHuffmaTable));
                            }
                            // 以上部分已经完成了元数据的读取
                            var test = acListY[0].Length;
                            // 还原成zigzag表
                            List<List<ushort>> zigzagListY = new List<List<ushort>>();
                            List<List<ushort>> zigzagListU = new List<List<ushort>>();
                            List<List<ushort>> zigzagListV = new List<List<ushort>>();
                            for (int i = 0; i < blockNumber; i++)
                            {
                                List<ushort> zigzagY = new List<ushort>();
                                List<ushort> zigzagU = new List<ushort>();
                                List<ushort> zigzagV = new List<ushort>();

                                zigzagY.Add((ushort)dcListY[i]);
                                zigzagU.Add((ushort)dcListU[i]);
                                zigzagV.Add((ushort)dcListV[i]);
                                for (int j = 1; j < 64; j++)
                                {
                                    zigzagY.Add((ushort)acListY[i][j - 1]);
                                    zigzagU.Add((ushort)acListU[i][j - 1]);
                                    zigzagV.Add((ushort)acListV[i][j - 1]);
                                }

                                zigzagListY.Add(zigzagY);
                                zigzagListU.Add(zigzagU);
                                zigzagListV.Add(zigzagV);
                            }

                            int blockWidth = width / blockSize;
                            int blockHeight = height / blockSize;

                            YUV[] yuvArray = new YUV[width * height];


                            for (int i = 0; i < blockNumber; i++)
                            {
                                // 计算当前块的位置
                                int blockY = (i / blockWidth) * blockSize;
                                int blockX = (i % blockWidth) * blockSize;

                                var blockYValue = ApplyIDCT(DequantizeBlock(UnZigzagOrdering(zigzagListY[i])));
                                var blockUValue = ApplyIDCT(DequantizeBlock(UnZigzagOrdering(zigzagListU[i])));
                                var blockVValue = ApplyIDCT(DequantizeBlock(UnZigzagOrdering(zigzagListV[i])));

                                for (int y = 0; y < blockSize; y++)
                                {
                                    for (int x = 0; x < blockSize; x++)
                                    {
                                        int pixelX = blockX + x;
                                        int pixelY = blockY + y;
                                        int pixelIndex = pixelY * width + pixelX;
                                        if (pixelX < width && pixelY < height)
                                        {
                                            yuvArray[pixelIndex] = new YUV(
                                                blockYValue[y, x],
                                                blockUValue[y, x],
                                                blockVValue[y, x]
                                            );
                                        }
                                    }
                                }

                            }
                            decodedImage.SetYUV(yuvArray);
                        }
                    }
                }

                return decodedImage;
            }

            return new ImgData();
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
        private double[,] ApplyDCT(YUV[] block, char type)
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
                            double pixelValue = 0;
                            switch (type)
                            {
                                case 'Y':
                                    pixelValue = block[x * blockSize + y].Y; // get the luminance as pixel value
                                    break;
                                case 'U':
                                    pixelValue = block[x * blockSize + y].U;
                                    break;
                                case 'V':
                                    pixelValue = block[x * blockSize + y].V;
                                    break;
                                default:
                                    break;
                            }

                            sum += pixelValue *
                                   Math.Cos((2 * x + 1) * u * Math.PI / 16) *
                                   Math.Cos((2 * y + 1) * v * Math.PI / 16);
                        }
                    }
                    double cu = (u == 0) ? 1 / Math.Sqrt(8) : 2 / Math.Sqrt(8);
                    double cv = (v == 0) ? 1 / Math.Sqrt(8) : 2 / Math.Sqrt(8);
                    dctCoefficients[u, v] = cu * cv * sum;
                }
            }

            return dctCoefficients;
        }

        private double[,] ApplyIDCT(double[,] dctCoefficients)
        {
            int blockSize = 8;
            double[,] block = new double[blockSize, blockSize];

            for (int x = 0; x < blockSize; x++)
            {
                for (int y = 0; y < blockSize; y++)
                {
                    double sum = 0.0;

                    for (int u = 0; u < blockSize; u++)
                    {
                        for (int v = 0; v < blockSize; v++)
                        {
                            double cu = (u == 0) ? 1 / Math.Sqrt(2) : 1;
                            double cv = (v == 0) ? 1 / Math.Sqrt(2) : 1;
                            sum += cu * cv * dctCoefficients[u, v] *
                                   Math.Cos((2 * x + 1) * u * Math.PI / 16) *
                                   Math.Cos((2 * y + 1) * v * Math.PI / 16);
                        }
                    }
                    block[x, y] = sum * 0.25;
                }
            }

            return block;
        }

        private double[,] DequantizeBlock(double[,] quantizedCoefficients)
        {
            // 使用与量化相同的量化矩阵
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

            double[,] dequantizedCoefficients = new double[8, 8];

            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    dequantizedCoefficients[i, j] = quantizedCoefficients[i, j] * quantizationMatrix[i, j];
                }
            }

            return dequantizedCoefficients;
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

        private List<ushort> ZigzagOrdering(double[,] block)
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
            List<ushort> zigzag = new List<ushort>();

            foreach (var index in order)
            {
                int i = index / 8;
                int j = index % 8;
                zigzag.Add((char)block[i, j]);
            }

            return zigzag;
        }

        private double[,] UnZigzagOrdering(List<ushort> zigzag)
        {
            double[,] block = new double[8, 8];
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

            if (zigzag.Count != 64)
            {
                throw new ArgumentException("Invalid zigzag list size. Expected 64 elements.");
            }

            for (int k = 0; k < order.Length; k++)
            {
                int i = order[k] / 8; // 计算行
                int j = order[k] % 8; // 计算列
                block[i, j] = zigzag[k]; // 根据Zigzag顺序填充矩阵
            }

            return block;
        }

        private string ZigzagToString(List<ushort> zigzag)
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

        private async Task EncodingImage(StorageFile saveImage)
        {
            if (saveImage != null)
            {
                using (var stream = await saveImage.OpenAsync(FileAccessMode.ReadWrite))
                {
                    using (var outputStream = stream.AsStreamForWrite())
                    {
                        using (var writer = new BinaryWriter(outputStream))
                        {
                            writer.Write((ushort)img.GetWidth()); // 写入图像宽度
                            writer.Write((ushort)img.GetHeight()); // 写入图像高度
                            writer.Write((byte)1); // 4:2:2 or 4:0:2
                            writer.Write((int)_encodingDcTable.Count); // DCPM字典大小
                            writer.Write((int)_encodingAcTable.Count); // ACRLE字典大小
                            // 写入DC字典
                            foreach (var entry in _encodingDcTable)
                            {
                                writer.Write(entry.Key);
                                writer.Write(entry.Value);
                            }
                            // 写入AC字典
                            foreach (var entry in _encodingAcTable)
                            {
                                writer.Write(entry.Key);
                                writer.Write(entry.Value);
                            }
                            // 写入块
                                // 先写入DC差分信息的huffman结果
                            // Y
                                // Y header
                            writer.Write((char)'Y'); // block type
                            writer.Write((byte)0); // 0 stand for DC
                            int addBitsLen = (8 - (encodedDCPM.Length - 1) % 8 - 1);
                            writer.Write((byte)addBitsLen); // 补齐至整个字节
                            writer.Write((ushort)(encodedDCPM.Length / 8 + (addBitsLen > 0 ? 1 : 0))); // 数据流占用的字节数
                                // Y data
                            WriteBitString(writer, encodedDCPM.ToString());
                            // U
                                // U header
                            writer.Write((char)'U'); // block type
                            writer.Write((byte)0); // 0 stand for DC
                            addBitsLen = (8 - (encodedDCPM.Length - 1) % 8 - 1);
                            writer.Write((byte)addBitsLen); // 补齐至整个字节
                            writer.Write((ushort)(encodedDCPM.Length / 8 + (addBitsLen > 0 ? 1 : 0))); // 数据流占用的字节数
                                // U data
                            WriteBitString(writer, encodedDCPM.ToString());
                            // V
                                // V header
                            writer.Write((char)'V'); // block type
                            writer.Write((byte)0); // 0 stand for DC
                            addBitsLen = (8 - (encodedDCPM.Length - 1) % 8 - 1);
                            writer.Write((byte)addBitsLen); // 补齐至整个字节
                            writer.Write((ushort)(encodedDCPM.Length / 8 + (addBitsLen > 0 ? 1 : 0))); // 数据流占用的字节数
                                // V data
                            WriteBitString(writer, encodedDCPM.ToString());
                            // 写入每个块的AC huffman结果
                            //if (blockCount == _encodedAcBlocks.Count)
                            //{
                            //    Debug.Print("equal");
                            //}
                            //for (int i = 0; i < blockCount; i++)
                            foreach (var block in _encodedAcBlocks)
                            {
                                // Y
                                    // Y header
                                writer.Write('Y'); // block type
                                writer.Write((byte)1); // 1 stand for AC
                                addBitsLen = (8 - (block.Length - 1) % 8 - 1);
                                writer.Write((byte)addBitsLen); // 补齐至整个字节
                                writer.Write((ushort)(block.Length / 8 + (addBitsLen > 0 ? 1 : 0))); // 数据流占用的字节数
                                    // Y data
                                WriteBitString(writer, block.ToString());
                                // U
                                    // U header
                                writer.Write('U'); // block type
                                writer.Write((byte)1); // 1 stand for AC
                                addBitsLen = (8 - (block.Length - 1) % 8 - 1);
                                writer.Write((byte)addBitsLen); // 补齐至整个字节
                                writer.Write((ushort)(block.Length / 8 + (addBitsLen > 0 ? 1 : 0))); // 数据流占用的字节数
                                    // U data
                                WriteBitString(writer, block.ToString());
                                // V
                                    // V header
                                writer.Write('V'); // block type
                                writer.Write((byte)0); // 1 stand for AC
                                addBitsLen = (8 - (block.Length - 1) % 8 - 1);
                                writer.Write((byte)addBitsLen); // 补齐至整个字节
                                writer.Write((ushort)(block.Length / 8 + (addBitsLen > 0 ? 1 : 0))); // 数据流占用的字节数
                                    // V data
                                WriteBitString(writer, block.ToString());
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
                var dctCoef = ApplyDCT(block, 'Y');
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
            string total_AC_RLE = string.Empty;
            _encodingDcTable.Clear();
            _encodingAcTable.Clear();
            _encodedAcBlocks.Clear();
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
                DCPMs += (char)(DCPM & 0xffff);

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


                total_AC_RLE += AC_RLE;

                AC_RLE_List.Add(AC_RLE); // 加入到AC RLE编码列表

                // zigzag[0] is the DC Coefficient, zigzag[1]-zigzag[63] are the AC Coefficient
            }

            var DCPMLen = DCPMs.Length;
            // after processing all the blocks, apply Huffman compressing
            HuffmanCompression dc = new HuffmanCompression(); 
            encodedDCPM = dc.HuffmanComp(DCPMs);
            _encodingDcTable = dc.encodingTable;

            HuffmanCompression ac = new HuffmanCompression();
            // get AC huffman coding table
            var encodedAcNums = ac.HuffmanComp(total_AC_RLE);
            _encodingAcTable = ac.encodingTable;
            // encoding each block
            foreach (var srcAC in AC_RLE_List)
            {
                _encodedAcBlocks.Add(ac.EncodingString(srcAC));
            }
        }
    }
}
