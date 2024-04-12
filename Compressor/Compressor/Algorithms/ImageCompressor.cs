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
        ImgData img;

        // new variables
        StringBuilder _encodedDcY = new StringBuilder();
        StringBuilder _encodedDcU = new StringBuilder();
        StringBuilder _encodedDcV = new StringBuilder();
        StringBuilder _encodedAcY = new StringBuilder();
        StringBuilder _encodedAcU = new StringBuilder();
        StringBuilder _encodedAcV = new StringBuilder();
        Dictionary<char, string> _dcTableL = new Dictionary<char, string>();
        Dictionary<char, string> _dcTableC = new Dictionary<char, string>();
        Dictionary<char, string> _acTableL = new Dictionary<char, string>();
        Dictionary<char, string> _acTableC = new Dictionary<char, string>();
        public async Task Compress(StorageFile input, StorageFile output, byte type = 0x00)
        {
            await dataLoader.LoadImagePixels(input);
            img = dataLoader.GetImgData();
            img.SetDownSampleType(type); // set down sample type
            img.DownSampling(); // down sampling
            DoCompress();
            await EncodingImage(output);
        }

        public async Task DeCompress(StorageFile input, StorageFile output)
        {
            ImgData decodedImage = await DecodingImage(input);
            await dataLoader.SaveImagePixels(decodedImage.BGRAData, output, decodedImage.GetWidth(), decodedImage.GetHeight());
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

        private List<int[]> ReadAC(BinaryReader reader, Dictionary<string, char> acHuffmanTable)
        {
            List<int[]> result = new List<int[]>();
            List<int> block = new List<int>();

            char type = reader.ReadChar();
            // AC/DC
            byte ifAc = reader.ReadByte(); // DC:0 AC:1
            if (ifAc != 0x01)
            {
                // error
                
            }
            
            ushort length = reader.ReadUInt16();
            byte[] compressedData = reader.ReadBytes(length);
            string data = DecodeBits(compressedData, acHuffmanTable, 0);
            block.Clear();
            foreach (var c in data) // (zeroCnt, value)
            {
                int zeroCnt = c >> 8;
                int value = c & 0x7F;
                if ((c & 0x0080) == 1)
                {
                    value = -value;
                }
                for (int i = 0; i < zeroCnt; i++)
                {
                    block.Add(0);
                }
                block.Add(0);
                if (value == 0 && zeroCnt == 0) // EOB, end of a block
                {
                    var itemCnt = block.Count;
                    if (itemCnt < 63)
                    {
                        for (int i = 0; i < 63 - itemCnt; i++)
                        {
                            block.Add(0);
                        }
                        result.Add(block.ToArray());
                        block.Clear();
                    }
                    else
                    {
                        result.Add(block.ToArray());
                        block.Clear();
                    }
                }
            }

            return result;
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
                            ushort width = reader.ReadUInt16(); // read width
                            ushort height = reader.ReadUInt16(); // read height
                            decodedImage.SetWH(width, height); // set width and height
                            byte colorEncoding = reader.ReadByte(); // read color encoding
                            decodedImage.SetDownSampleType(colorEncoding); // set color encoding
                            Dictionary<string, char> dcLTable = new Dictionary<string, char>();
                            Dictionary<string, char> dcCTable = new Dictionary<string, char>();
                            Dictionary<string, char> acLTable = new Dictionary<string, char>();
                            Dictionary<string, char> acCTable = new Dictionary<string, char>();
                            // construct dc L table
                            int dcTableLSize = reader.ReadInt32(); // read dc L table size
                            for (int i = 0; i < dcTableLSize; i++)
                            {
                                char key = reader.ReadChar();
                                string value = reader.ReadString();
                                dcLTable[value] = key;
                            }
                            // construct dc C table
                            int dcTableCSize = reader.ReadInt32(); // read dc C table size
                            for (int i = 0; i < dcTableCSize; i++)
                            {
                                char key = reader.ReadChar();
                                string value = reader.ReadString();
                                dcCTable[value] = key;
                            }
                            // construct ac L table
                            int acTableLSize = reader.ReadInt32(); // read ac L table size
                            for (int i = 0; i < acTableLSize; i++)
                            {
                                char key = reader.ReadChar();
                                string value = reader.ReadString();
                                acLTable[value] = key;
                            }
                            // construct ac C table
                            int acTableCSize = reader.ReadInt32(); // read ac C table size
                            for (int i = 0; i < acTableCSize; i++)
                            {
                                char key = reader.ReadChar();
                                string value = reader.ReadString();
                                acCTable[value] = key;
                            }
                            // calculate block number
                            const int blockSize = 8;
                            int blockWidthNumber = (int)Math.Ceiling(width / (double)blockSize);
                            int blockHeightNumber = (int)Math.Ceiling(height / (double)blockSize);
                            int blockNumber = blockHeightNumber * blockWidthNumber;
                            int blockNumberUv = (colorEncoding == 0x00) ? blockNumber : blockNumber / 4;
                            // read dc
                            List<int> dcListY = ReadDC(reader, dcLTable);
                            List<int> dcListU = ReadDC(reader, dcCTable);
                            List<int> dcListV = ReadDC(reader, dcCTable);
                            // read ac, store blocks into a list
                            List<int[]> acListY = ReadAC(reader, acLTable);
                            List<int[]> acListU = ReadAC(reader, acCTable);
                            List<int[]> acListV = ReadAC(reader, acCTable);
                            // get zigzag and decode
                            double[] yArray = new double[width * height];
                            double[] uArray = new double[width * height];
                            double[] vArray = new double[width * height];
                            // Y
                            for (int i = 0; i < blockNumber; i++)
                            {
                                int blockY = (i / blockWidthNumber) * blockSize;
                                int blockX = (i % blockWidthNumber) * blockSize;
                                // get zigzag string
                                List<short> zigzag = new List<short>();
                                zigzag.Add((short)dcListY[i]);
                                for (int j = 1; j < 64; j++)
                                {
                                    zigzag.Add((short)acListY[i][j - 1]);
                                }

                                var block = ApplyIDCT(DequantizeBlock(UnZigzagOrdering(zigzag)));
                                for (int y = 0; y < blockSize; y++)
                                {
                                    for (int x = 0; x < blockSize; x++)
                                    {
                                        int pixelX = blockX + x;
                                        int pixelY = blockY + y;
                                        int pixelIndex = pixelY * width + pixelX;
                                        if (pixelX < width && pixelY < height)
                                        {
                                            yArray[pixelIndex] = block[y, x];
                                        }
                                    }
                                }
                            }
                            // U
                            for (int i = 0; i < blockNumberUv; i++)
                            {
                                int blockY = (i / (blockWidthNumber / 2)) * blockSize;
                                int blockX = (i % (blockWidthNumber / 2)) * blockSize;
                                // get zigzag string for U
                                List<short> zigzagU = new List<short>();
                                zigzagU.Add((short)dcListU[i]);
                                for (int j = 1; j < 64; j++)
                                {
                                    zigzagU.Add((short)acListU[i][j - 1]);
                                }

                                var blockU = ApplyIDCT(DequantizeBlock(UnZigzagOrdering(zigzagU)));
                                for (int y = 0; y < blockSize; y++)
                                {
                                    for (int x = 0; x < blockSize; x++)
                                    {
                                        int pixelX = blockX + x * 2; // Account for down-sampling
                                        int pixelY = blockY + y * 2; // Account for down-sampling
                                        for (int dy = 0; dy < 2; dy++)
                                        {
                                            for (int dx = 0; dx < 2; dx++)
                                            {
                                                int subPixelX = pixelX + dx;
                                                int subPixelY = pixelY + dy;
                                                int pixelIndex = subPixelY * width + subPixelX;
                                                if (subPixelX < width && subPixelY < height)
                                                {
                                                    uArray[pixelIndex] = blockU[y, x];
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            // V
                            for (int i = 0; i < blockNumberUv; i++)
                            {
                                int blockY = (i / (blockWidthNumber / 2)) * blockSize;
                                int blockX = (i % (blockWidthNumber / 2)) * blockSize;
                                // get zigzag string for V
                                List<short> zigzagV = new List<short>();
                                zigzagV.Add((short)dcListV[i]);
                                for (int j = 1; j < 64; j++)
                                {
                                    zigzagV.Add((short)acListV[i][j - 1]);
                                }

                                var blockV = ApplyIDCT(DequantizeBlock(UnZigzagOrdering(zigzagV)));
                                for (int y = 0; y < blockSize; y++)
                                {
                                    for (int x = 0; x < blockSize; x++)
                                    {
                                        int pixelX = blockX + x * 2; // Account for down-sampling
                                        int pixelY = blockY + y * 2; // Account for down-sampling
                                        for (int dy = 0; dy < 2; dy++)
                                        {
                                            for (int dx = 0; dx < 2; dx++)
                                            {
                                                int subPixelX = pixelX + dx;
                                                int subPixelY = pixelY + dy;
                                                int pixelIndex = subPixelY * width + subPixelX;
                                                if (subPixelX < width && subPixelY < height)
                                                {
                                                    vArray[pixelIndex] = blockV[y, x];
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            // set YUV
                            
                        }
                    }
                }

                return decodedImage;
            }

            return new ImgData();
        }

        private List<double[]> GetDctBlocks(double[] data, bool ifDownSampled)
        {
            const int blockSize = 8;
            // blockCount = 0;
            List<double[]> blocks = new List<double[]>();
            var width = img.GetWidth();
            var height = img.GetHeight();
            int blockWidthNumber = (int)Math.Ceiling(width / (double)blockSize);
            int blockHeightNumber = (int)Math.Ceiling(height / (double)blockSize);
            // blockCount = blockHeightNumber * blockWidthNumber;
            if (ifDownSampled) // if down sampled to 420
            {
                width /= 2;
                height /= 2;
                blockWidthNumber = (int)Math.Ceiling(width / (double)blockSize);
                blockHeightNumber = (int)Math.Ceiling(height / (double)blockSize);
            }

            for (int blockY = 0; blockY < blockHeightNumber; blockY++)
            {
                for (int blockX = 0; blockX < blockWidthNumber; blockX++)
                {
                    var block = new double[blockSize * blockSize];
                    for (int y = 0; y < blockSize; y++)
                    {
                        for (int x = 0; x < blockSize; x++)
                        {
                            int pixelIndex = (blockY * blockSize + y) * (int)width + (blockX * blockSize + x);
                            if (blockY * blockSize + y >= height || blockX * blockSize + x >= width)
                            {
                                // padding with black pixels
                                block[y * blockSize + x] = 0.0;
                            }
                            else
                            {
                                // Copy the YUV data from the image to the block
                                block[y * blockSize + x] = data[pixelIndex];
                            }
                        }
                    }
                    blocks.Add(block);
                }
            }

            return blocks;
        }

        // apply DCT for blocks
        private double[,] ApplyDct(double[] block)
        {
            const int blockSize = 8;
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
                            pixelValue = block[x * blockSize + y];
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

        private List<short> ZigzagOrdering(double[,] block)
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
            List<short> zigzag = new List<short>();

            foreach (var index in order)
            {
                int i = index / 8;
                int j = index % 8;
                zigzag.Add((short)block[i, j]);
            }

            return zigzag;
        }

        private double[,] UnZigzagOrdering(List<short> zigzag)
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
            if (saveImage == null) return;
        
            using (var stream = await saveImage.OpenAsync(FileAccessMode.ReadWrite))
            using (var outputStream = stream.AsStreamForWrite())
            using (var writer = new BinaryWriter(outputStream))
            {
                WriteImageHeader(writer);
                WriteHuffmanTables(writer);
                WriteEncodedData(writer);
            }
        }
        
        private void WriteImageHeader(BinaryWriter writer)
        {
            writer.Write((ushort)img.GetWidth());
            writer.Write((ushort)img.GetHeight());
            writer.Write(img.GetDownSampleType()); // 444:0x00 420:0x01
        }
        
        private void WriteHuffmanTables(BinaryWriter writer)
        {
            var tables = new Dictionary<string, Dictionary<char, string>> {
                {"Luminance DC", _dcTableL},
                {"Chrominance DC", _dcTableC},
                {"Luminance AC", _acTableL},
                {"Chrominance AC", _acTableC}
            };
        
            foreach (var table in tables)
            {
                writer.Write((int)table.Value.Count);
                foreach (var entry in table.Value)
                {
                    writer.Write(entry.Key);
                    writer.Write(entry.Value);
                }
            }
        }
        
        private void WriteEncodedData(BinaryWriter writer)
        {
            void WriteComponentData(char component, StringBuilder dcData, StringBuilder acData)
            {
                WriteEncodedComponent(writer, component, 0, dcData); // DC
                WriteEncodedComponent(writer, component, 1, acData); // AC
            }
        
            WriteComponentData('Y', _encodedDcY, _encodedAcY);
            WriteComponentData('U', _encodedDcU, _encodedAcU);
            WriteComponentData('V', _encodedDcV, _encodedAcV);
        }
        
        private void WriteEncodedComponent(BinaryWriter writer, char componentType, byte type, StringBuilder encodedData)
        {
            int additionalBitsLen = (8 - (encodedData.Length - 1) % 8 - 1);
            writer.Write(componentType); // Y/U/V
            writer.Write(type); // DC/AC
            writer.Write((byte)additionalBitsLen); // Additional bits
            writer.Write((ushort)(encodedData.Length / 8 + (additionalBitsLen > 0 ? 1 : 0))); // Data byte length
            WriteBitString(writer, encodedData.ToString()); // Data
        }

        private void DoCompress()
        {
            bool ifDownSampled = (img.GetDownSampleType() == 0x01) ? true : false;
            var yBlocks = GetDctBlocks(img.downsampledY, false);
            var uBlocks = GetDctBlocks(img.downsampledU, ifDownSampled);
            var vBlocks = GetDctBlocks(img.downsampledV, ifDownSampled);
            List<double[,]> yQuantizedBlocks = new List<double[,]>();
            List<double[,]> uQuantizedBlocks = new List<double[,]>();
            List<double[,]> vQuantizedBlocks = new List<double[,]>();
            // Apply DCT and Quantization
            foreach (var block in yBlocks) // Y
            {
                var quantizedBlock = QuantizeBlock(ApplyDct(block));
                yQuantizedBlocks.Add(quantizedBlock);
            }
            foreach (var block in uBlocks) // U
            {
                var quantizedBlock = QuantizeBlock(ApplyDct(block));
                uQuantizedBlocks.Add(quantizedBlock);
            }
            foreach (var block in vBlocks) // V
            {
                var quantizedBlock = QuantizeBlock(ApplyDct(block));
                vQuantizedBlocks.Add(quantizedBlock);
            }
            // luminance
            int yPrevDC = 0;
            // StringBuilder yAcRle = new StringBuilder();
            StringBuilder yDCPMs = new StringBuilder();
            StringBuilder allAcRleLuminance = new StringBuilder();
            // chrominance
            int uPrevDC = 0;
            // StringBuilder uAcRle = new StringBuilder();
            StringBuilder uDCPMs = new StringBuilder();
            StringBuilder allAcRleU = new StringBuilder();
            int vPrevDC = 0;
            // StringBuilder vAcRle = new StringBuilder();
            StringBuilder vDCPMs = new StringBuilder();
            StringBuilder allAcRleV = new StringBuilder();
            StringBuilder allAcRleChrominance = new StringBuilder();
            
            // inline function to process block
            void ProcessBlock(List<double[,]> quantizedBlocks, StringBuilder dcpmBuilder, ref int prevDC, StringBuilder allAcRle)
            {
                StringBuilder acRleBuilder = new StringBuilder();
                foreach (var block in quantizedBlocks)
                {
                    acRleBuilder.Clear();
                    var zigzag = ZigzagOrdering(block);

                    // DCPM
                    int dcpm = zigzag[0] - prevDC;
                    prevDC = zigzag[0];
                    if (dcpm < 0) // mark minus
                    {
                        dcpm = Math.Abs(dcpm) | 0x8000;
                    }
                    dcpmBuilder.Append((char)(dcpm & 0xffff));

                    // AC RLE
                    int zeroCount = 0;
                    for (int i = 1; i < 64; i++)
                    {
                        if (zigzag[i] == 0)
                        {
                            zeroCount++;
                        }
                        else
                        {
                            int combinedPair = (zeroCount << 8) | (Math.Abs(zigzag[i]) & 0xff);
                            if (zigzag[i] < 0) // mark minus
                            {
                                combinedPair |= 0x80; // set the minus flag
                            }
                            acRleBuilder.Append((char)combinedPair);
                            zeroCount = 0; // reset zero count
                        }
                    }
                    acRleBuilder.Append((char)0); // append end/EOB
                    allAcRle.Append(acRleBuilder); // add to the whole AC RLE string, prepare for huffman coding
                    
                }
            }

            ProcessBlock(yQuantizedBlocks, yDCPMs, ref yPrevDC, allAcRleLuminance);
            ProcessBlock(uQuantizedBlocks, uDCPMs, ref uPrevDC, allAcRleU);
            ProcessBlock(vQuantizedBlocks, vDCPMs, ref vPrevDC, allAcRleV);

            // huffman compress
            // luminance
            HuffmanCompression yDc = new HuffmanCompression();
            _encodedDcY = yDc.HuffmanComp(yDCPMs.ToString());
            _dcTableL = yDc.encodingTable;
            HuffmanCompression yAc = new HuffmanCompression();
            _encodedAcY = yAc.HuffmanComp(allAcRleLuminance.ToString());
            _acTableL = yAc.encodingTable;
            // chrominance
            HuffmanCompression cDc = new HuffmanCompression();
            StringBuilder allDCPMsChrominace = new StringBuilder();
            // join u and v DCPMs
            allDCPMsChrominace.Append(uDCPMs);
            allDCPMsChrominace.Append(vDCPMs);
            var tmpDc = cDc.HuffmanComp(allDCPMsChrominace.ToString());
            _encodedDcU = cDc.EncodingString(uDCPMs.ToString());
            _encodedDcV = cDc.EncodingString(vDCPMs.ToString());
            _dcTableC = cDc.encodingTable;
            HuffmanCompression cAc = new HuffmanCompression();
            // join u and v AC RLE
            allAcRleChrominance.Append(allAcRleU);
            allAcRleChrominance.Append(allAcRleV);
            var tmpAc = cAc.HuffmanComp(allAcRleChrominance.ToString());
            _encodedAcU = cAc.EncodingString(allAcRleU.ToString());
            _encodedAcV = cAc.EncodingString(allAcRleV.ToString());
            _acTableC = cAc.encodingTable;
        }
    }
}
