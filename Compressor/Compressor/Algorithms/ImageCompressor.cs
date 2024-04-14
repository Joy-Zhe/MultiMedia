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
using Windows.UI.Xaml.Documents;

namespace Compressor.Algorithms
{
    internal class ImageCompressor
    {
        ImgData img;

        // new variables
        StringBuilder _encodedDcY = new StringBuilder();
        StringBuilder _encodedDcU = new StringBuilder();
        StringBuilder _encodedDcV = new StringBuilder();
        StringBuilder _encodedAcY = new StringBuilder();
        StringBuilder _encodedAcU = new StringBuilder();
        StringBuilder _encodedAcV = new StringBuilder();
        Dictionary<int, string> _dcTableL = new Dictionary<int, string>();
        Dictionary<int, string> _dcTableC = new Dictionary<int, string>();
        Dictionary<int, string> _acTableL = new Dictionary<int, string>();
        Dictionary<int, string> _acTableC = new Dictionary<int, string>();

        // for debug
        public static async Task WriteDataToFileAsync(double[] dataArray, int width, int height, StorageFile outputFile)
        {
            StringBuilder sb = new StringBuilder();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    sb.Append($"{dataArray[index]:F2}\t");
                }
                sb.AppendLine();
            }

            // 使用Windows Runtime API写入文件
            await FileIO.WriteTextAsync(outputFile, sb.ToString());
        }
        public async Task Compress(StorageFile input, StorageFile output, byte type = 0x00)
        {
            DataLoader dataLoader = new DataLoader();
            await dataLoader.LoadImagePixels(input);
            img = dataLoader.GetImgData();
            img.SetDownSampleType(type); // set down sample type
            img.DownSampling(); // down sampling
            DoCompress();
            await EncodingImage(output);
        }

        public async Task TestYUV(StorageFile inputFile, StorageFile outputFile)
        {
            DataLoader dataLoader = new DataLoader();
            await dataLoader.LoadImagePixels(inputFile);
            img = dataLoader.GetImgData();
            img.SetDownSampleType(0x01); // set down sample type
            img.DownSampling();
            img.UpSampling();
            await dataLoader.SaveImagePixels(img.BGRAData, outputFile, img.GetWidth(), img.GetHeight());
        }

        public async Task DeCompress(StorageFile input, StorageFile output)
        {
            DataLoader dataLoader = new DataLoader();
            ImgData decodedImage = await DecodingImage(input);
            await dataLoader.SaveImagePixels(decodedImage.BGRAData, output, decodedImage.GetWidth(), decodedImage.GetHeight());
        }

        private List<int> DecodeBits(byte[] bitStream, Dictionary<string, int> table, int additionalBitsLen)
        {
            StringBuilder bitString = new StringBuilder();
            List<int> result = new List<int>();

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
                        result.Add(table[code.ToString()]);
                        code.Clear();
                        break;
                    }
                }
            }

            return result;
        }
        private List<int> ReadDC(BinaryReader reader, Dictionary<string, int> dcHuffmanTable)
        {
            List<int> dcList = new List<int>();
            
            // Y/U/V
            char type = reader.ReadChar();
            // AC/DC
            byte ifAc = reader.ReadByte(); // DC:0 AC:1
            // add bits len
            byte additionalBitsLen = reader.ReadByte();
            // bits len(2B)
            int length = reader.ReadInt32();
            byte[] compressedData = reader.ReadBytes(length);
            List<int> data = DecodeBits(compressedData, dcHuffmanTable, additionalBitsLen);
            int nowDcpm = 0;
            dcList.Clear();
            // reconstruct dcpm table
            foreach (var delta in data)
            {
                nowDcpm += delta;
                dcList.Add(nowDcpm);
            }

            return dcList;
        }

        private List<int[]> ReadAC(BinaryReader reader, Dictionary<string, int> acHuffmanTable)
        {
            List<int[]> result = new List<int[]>();
            List<int> block = new List<int>();

            // Y/U/V
            char type = reader.ReadChar();
            // AC/DC
            byte ifAc = reader.ReadByte(); // DC:0 AC:1
            if (ifAc != 0x01)
            {
                // error
                
            }
            // add bits len
            byte additionalBitsLen = reader.ReadByte();
            // bits len(2B)
            int length = reader.ReadInt32();
            byte[] compressedData = reader.ReadBytes(length);
            List<int> data = DecodeBits(compressedData, acHuffmanTable, additionalBitsLen);
            block.Clear();
            foreach (var c in data) // (zeroCnt, value)
            {
                int zeroCnt = c >> 16;
                int value = (short)(c & 0xffff);
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
                        continue;
                    }
                    else
                    {
                        result.Add(block.ToArray());
                        block.Clear();
                        continue;
                    }
                }
                for (int i = 0; i < zeroCnt; i++)
                {
                    block.Add(0);
                }
                block.Add(value);
            }
            return result;
        }

        void RestoreBlocks(List<int> dcList, List<int[]> acList, double[] dataArray, char componentType, byte downSampleType, int width, int height)
        {
            // calculate block number
            const int blockSize = 8;
            int blockWidthNumber = (int)Math.Ceiling((downSampleType == 0x00 ? width : (width / 2.0)) / (double)blockSize);
            int blockHeightNumber = (int)Math.Ceiling((downSampleType == 0x00 ? height : (height / 2.0)) / (double)blockSize);
            int blockNumber = blockHeightNumber * blockWidthNumber;
            for (int i = 0; i < blockNumber; i++)
            {
                int blockY = (i / blockWidthNumber) * blockSize;
                int blockX = (i % blockWidthNumber) * blockSize;
                // get zigzag string
                List<int> zigzag = new List<int> { dcList[i] };
                for (int j = 1; j < 64; j++)
                {
                    zigzag.Add(acList[i][j - 1]);
                }

                var block = ApplyIDCT(DequantizeBlock(UnZigzagOrdering(zigzag), componentType));
                if (i == 0 && componentType != 'Y')
                {
                    Debug.WriteLine(componentType);
                    Debug.WriteLine("unzigzag");
                    PrintMatrix(UnZigzagOrdering(zigzag));
                    Debug.WriteLine("dequantized");
                    PrintMatrix(DequantizeBlock(UnZigzagOrdering(zigzag), componentType));
                    Debug.WriteLine("idct");
                    PrintMatrix(block);
                }

                for (int y = 0; y < blockSize; y++)
                {
                    for (int x = 0; x < blockSize; x++)
                    {
                        int pixelX = blockX + x;
                        int pixelY = blockY + y;
                        int pixelIndex = pixelY * width + pixelX;
                        if (downSampleType == 0x01 && componentType != 'Y')
                        {
                            for (int dy = 0; dy < 2; dy++)
                            {
                                for (int dx = 0; dx < 2; dx++)
                                {
                                    int mappedX = pixelX * 2 + dx;
                                    int mappedY = pixelY * 2 + dy;
                                    pixelIndex = mappedY * width + mappedX;
                                    if (mappedX < width && mappedY < height)
                                    {
                                        dataArray[pixelIndex] = block[y, x];
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (pixelX < width && pixelY < height)
                            {
                                dataArray[pixelIndex] = block[y, x];
                            }
                        }
                    }
                }
            }
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
                            Dictionary<string, int> dcLTable = new Dictionary<string, int>();
                            Dictionary<string, int> dcCTable = new Dictionary<string, int>();
                            Dictionary<string, int> acLTable = new Dictionary<string, int>();
                            Dictionary<string, int> acCTable = new Dictionary<string, int>();
                            // construct dc L table
                            int dcTableLSize = reader.ReadInt32(); // read dc L table size
                            for (int i = 0; i < dcTableLSize; i++)
                            {
                                int key = reader.ReadInt32();
                                string value = reader.ReadString();
                                dcLTable[value] = key;
                            }
                            // construct dc C table
                            int dcTableCSize = reader.ReadInt32(); // read dc C table size
                            for (int i = 0; i < dcTableCSize; i++)
                            {
                                int key = reader.ReadInt32();
                                string value = reader.ReadString();
                                dcCTable[value] = key;
                            }
                            // construct ac L table
                            int acTableLSize = reader.ReadInt32(); // read ac L table size
                            for (int i = 0; i < acTableLSize; i++)
                            {
                                int key = reader.ReadInt32();
                                string value = reader.ReadString();
                                acLTable[value] = key;
                            }
                            // construct ac C table
                            int acTableCSize = reader.ReadInt32(); // read ac C table size
                            for (int i = 0; i < acTableCSize; i++)
                            {
                                int key = reader.ReadInt32();
                                string value = reader.ReadString();
                                acCTable[value] = key;
                            }
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
                            // YUV
                            RestoreBlocks(dcListY, acListY, yArray, 'Y', 0x00, width, height);
                            RestoreBlocks(dcListU, acListU, uArray, 'U', decodedImage.GetDownSampleType(), width, height);
                            RestoreBlocks(dcListV, acListV, vArray, 'V', decodedImage.GetDownSampleType(), width, height);

                            // debug
                            //await WriteDataToFileAsync(vArray, width, height, debugFile);

                            // set YUV
                            decodedImage.SetYUV(yArray, uArray, vArray);
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
            var width = Math.Ceiling(ifDownSampled ? (img.GetWidth() / 2.0) : img.GetWidth());
            var height = Math.Ceiling(ifDownSampled ? (img.GetHeight() / 2.0) : img.GetHeight());
            int blockWidthNumber = (int)Math.Ceiling(width / (double)blockSize);
            int blockHeightNumber = (int)Math.Ceiling(height / (double)blockSize);

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
                    double cu = (u == 0) ? 1 / Math.Sqrt(2) : 1;
                    double cv = (v == 0) ? 1 / Math.Sqrt(2) : 1;
                    for (int x = 0; x < blockSize; x++)
                    {
                        for (int y = 0; y < blockSize; y++)
                        {
                            double pixelValue = block[x * blockSize + y];
                            sum += pixelValue *
                                   Math.Cos((2 * x + 1) * u * Math.PI / 16) *
                                   Math.Cos((2 * y + 1) * v * Math.PI / 16);
                        }
                    }
                    dctCoefficients[u, v] = 0.25 * cu * cv * sum; // 规范化每个系数
                }
            }

            return dctCoefficients;
        }

        private double[,] ApplyIDCT(double[,] dctCoefficients)
        {
            const int blockSize = 8;
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
                    block[x, y] = 0.25 * sum;
                }
            }

            return block;
        }

        private double[,] DequantizeBlock(double[,] quantizedCoefficients, char type)
        {
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

            int[,] quantizationMatrixUv = new int[,]
            {
                { 17, 18, 24, 47, 99, 99, 99, 99 },
                { 18, 21, 26, 66, 99, 99, 99, 99 },
                { 24, 26, 56, 99, 99, 99, 99, 99 },
                { 47, 66, 99, 99, 99, 99, 99, 99 },
                { 99, 99, 99, 99, 99, 99, 99, 99 },
                { 99, 99, 99, 99, 99, 99, 99, 99 },
                { 99, 99, 99, 99, 99, 99, 99, 99 },
                { 99, 99, 99, 99, 99, 99, 99, 99 }
            };

            double[,] dequantizedCoefficients = new double[8, 8];

            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    if (type == 'Y')
                    {
                        dequantizedCoefficients[i, j] = quantizedCoefficients[i, j] * quantizationMatrix[i, j];
                    }
                    else
                    {
                        dequantizedCoefficients[i, j] = quantizedCoefficients[i, j] * quantizationMatrixUv[i, j];
                    }
                }
            }

            return dequantizedCoefficients;
        }

        // for debug
        private void PrintMatrix(double[,] matrix)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);
            StringBuilder output = new StringBuilder();

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    output.Append($"{matrix[i, j]:F2}\t");  // :F2 控制输出格式为两位小数
                }
                output.AppendLine();
            }
            Debug.WriteLine(output.ToString());
        }

        private void PrintArrayAsMatrix(double[] array, int rows, int cols)
        {
            if (array.Length != rows * cols)
            {
                throw new ArgumentException("Array length does not match the specified dimensions.");
            }

            StringBuilder output = new StringBuilder();
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    output.Append($"{array[i * cols + j]:F2}\t");
                }
                output.AppendLine();
            }
            Debug.WriteLine(output.ToString());
        }

        // ---------------------------

        private double[,] QuantizeBlock(double[,] dctCoef, char type)
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

            int[,] quantizationMatrixUv = new int[,]
            {
                { 17, 18, 24, 47, 99, 99, 99, 99 },
                { 18, 21, 26, 66, 99, 99, 99, 99 },
                { 24, 26, 56, 99, 99, 99, 99, 99 },
                { 47, 66, 99, 99, 99, 99, 99, 99 },
                { 99, 99, 99, 99, 99, 99, 99, 99 },
                { 99, 99, 99, 99, 99, 99, 99, 99 },
                { 99, 99, 99, 99, 99, 99, 99, 99 },
                { 99, 99, 99, 99, 99, 99, 99, 99 }
            };

            double[,] quantizedCoefficients = new double[8, 8];

            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    if (type == 'Y')
                    {
                        quantizedCoefficients[i, j] = Math.Round(dctCoef[i, j] / quantizationMatrix[i, j]);
                    }
                    else
                    {
                        quantizedCoefficients[i, j] = Math.Round(dctCoef[i, j] / quantizationMatrixUv[i, j]);
                    }
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
                zigzag.Add((int)block[i, j]);
            }

            return zigzag;
        }

        private double[,] UnZigzagOrdering(List<int> zigzag)
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
            var tables = new Dictionary<string, Dictionary<int, string>> {
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
            void WriteComponentData(char component, StringBuilder data, byte ifAc)
            {
                WriteEncodedComponent(writer, component, ifAc, data); 
            }
            WriteComponentData('Y', _encodedDcY, (byte)0x00);
            WriteComponentData('U', _encodedDcU, (byte)0x00);
            WriteComponentData('V', _encodedDcV, (byte)0x00);
            WriteComponentData('Y', _encodedAcY, (byte)0x01);
            WriteComponentData('U', _encodedAcU, (byte)0x01);
            WriteComponentData('V', _encodedAcV, (byte)0x01);
        }
        
        private void WriteEncodedComponent(BinaryWriter writer, char componentType, byte type, StringBuilder encodedData)
        {
            int additionalBitsLen = (8 - (encodedData.Length - 1) % 8 - 1);
            writer.Write(componentType); // Y/U/V
            writer.Write(type); // DC/AC
            writer.Write((byte)additionalBitsLen); // Additional bits
            writer.Write((int)(encodedData.Length / 8 + (additionalBitsLen > 0 ? 1 : 0))); // Data byte length
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
                var quantizedBlock = QuantizeBlock(ApplyDct(block), 'Y');
                yQuantizedBlocks.Add(quantizedBlock);
            }
            foreach (var block in uBlocks) // U
            {
                var quantizedBlock = QuantizeBlock(ApplyDct(block), 'U');
                if (block == uBlocks.First())
                {
                    Debug.WriteLine("Ori U");
                    PrintArrayAsMatrix(block, 8, 8);
                    Debug.WriteLine("DCT");
                    PrintMatrix(ApplyDct(block));
                    Debug.WriteLine("Quantize");
                    PrintMatrix(quantizedBlock);
                }
                uQuantizedBlocks.Add(quantizedBlock);
            }
            foreach (var block in vBlocks) // V
            {
                var quantizedBlock = QuantizeBlock(ApplyDct(block), 'V');
                if (block == vBlocks.First())
                {
                    Debug.WriteLine("Ori V");
                    PrintArrayAsMatrix(block, 8, 8);
                    Debug.WriteLine("DCT");
                    PrintMatrix(ApplyDct(block));
                    Debug.WriteLine("Quantize");
                    PrintMatrix(quantizedBlock);
                }
                vQuantizedBlocks.Add(quantizedBlock);
            }
            // luminance
            int yPrevDC = 0;
            List<int> yDCPMs = new List<int>();
            List<int> allAcRleLuminance = new List<int>();
            // chrominance
            int uPrevDC = 0;
            List<int> uDCPMs = new List<int>();
            List<int> allAcRleU = new List<int>();
            int vPrevDC = 0;
            List<int> vDCPMs = new List<int>();
            List<int> allAcRleV = new List<int>();
            List<int> allAcRleChrominance = new List<int>();
            
            // inline function to process block
            void ProcessBlock(List<double[,]> quantizedBlocks, List<int> dcpmBuilder, ref int prevDC, List<int> allAcRle)
            {
                List<int> acRleBuilder = new List<int>();
                foreach (var block in quantizedBlocks)
                {
                    acRleBuilder.Clear();
                    var zigzag = ZigzagOrdering(block);

                    // DCPM
                    int dcpm = zigzag[0] - prevDC;
                    prevDC = zigzag[0];
                    dcpmBuilder.Add(dcpm);

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
                            int combinedPair = (zeroCount << 16) | ((short)zigzag[i]) & 0xffff;
                            acRleBuilder.Add(combinedPair);
                            zeroCount = 0; // reset zero count
                        }
                    }
                    acRleBuilder.Add(0); // append end/EOB
                    allAcRle.AddRange(acRleBuilder); // add to the whole AC RLE string, prepare for huffman coding
                    
                }
            }

            ProcessBlock(yQuantizedBlocks, yDCPMs, ref yPrevDC, allAcRleLuminance);
            ProcessBlock(uQuantizedBlocks, uDCPMs, ref uPrevDC, allAcRleU);
            ProcessBlock(vQuantizedBlocks, vDCPMs, ref vPrevDC, allAcRleV);

            // huffman compress
            // luminance
            HuffmanCompression yDc = new HuffmanCompression();
            _encodedDcY = yDc.HuffmanComp(yDCPMs);
            _dcTableL = yDc.EncodingTable;
            HuffmanCompression yAc = new HuffmanCompression();
            _encodedAcY = yAc.HuffmanComp(allAcRleLuminance);
            _acTableL = yAc.EncodingTable;
            // chrominance
            HuffmanCompression cDc = new HuffmanCompression();
            List<int> allDCPMsChrominace = new List<int>();
            // join u and v DCPMs
            allDCPMsChrominace.AddRange(uDCPMs);
            allDCPMsChrominace.AddRange(vDCPMs);
            var tmpDc = cDc.HuffmanComp(allDCPMsChrominace);
            _encodedDcU = cDc.EncodingString(uDCPMs);
            _encodedDcV = cDc.EncodingString(vDCPMs);
            _dcTableC = cDc.EncodingTable;
            HuffmanCompression cAc = new HuffmanCompression();
            // join u and v AC RLE
            allAcRleChrominance.AddRange(allAcRleU);
            allAcRleChrominance.AddRange(allAcRleV);
            var tmpAc = cAc.HuffmanComp(allAcRleChrominance);
            _encodedAcU = cAc.EncodingString(allAcRleU);
            _encodedAcV = cAc.EncodingString(allAcRleV);
            _acTableC = cAc.EncodingTable;
        }
    }
}
