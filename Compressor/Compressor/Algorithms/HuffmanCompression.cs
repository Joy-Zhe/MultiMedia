﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Popups;

namespace Compressor.Algorithms
{
    public class HuffmanNode
    {
        public char Symbol { get; set; }
        public int Frequency { get; set; }
        public HuffmanNode Left { get; set; }
        public HuffmanNode Right { get; set; }
    }

    internal class HuffmanCompression
    {
        private Dictionary<char, string> encodingTable = new Dictionary<char, string>();

        public async Task Compress(StorageFile inputFilePath, StorageFolder outputFilePath, String outputfileName)
        {
            try
            {
                /*                var text = File.ReadAllText(inputFilePath);*/
                string text = await FileIO.ReadTextAsync(inputFilePath);
                // character frequency
                var frequencyTable = new Dictionary<char, int>();
                foreach (var c in text)
                    if (frequencyTable.ContainsKey(c))
                        frequencyTable[c]++;
                    else
                        frequencyTable[c] = 1;

                // Huffman Tree
                var nodes = frequencyTable.Select(pair => new HuffmanNode
                {
                    Symbol = pair.Key,
                    Frequency = pair.Value
                }).ToList();

                while (nodes.Count > 1)
                {
                    nodes = nodes.OrderBy(node => node.Frequency).ToList();
                    var parent = new HuffmanNode
                    {
                        Frequency = nodes[0].Frequency + nodes[1].Frequency,
                        Left = nodes[0],
                        Right = nodes[1]
                    };
                    nodes.RemoveRange(0, 2);
                    nodes.Add(parent);
                }

                // Encoding Table
                BuildEncodingTable(nodes[0], "");

                // Compress
                var encodedText = new StringBuilder();
                foreach (var c in text) encodedText.Append(encodingTable[c]);

                StorageFile outputFile = await outputFilePath.CreateFileAsync(outputfileName, CreationCollisionOption.GenerateUniqueName);

                // File.WriteAllText(outputFilePath, encodedText.ToString());
                using (IRandomAccessStream stream = await outputFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    using (Stream outputStream = stream.AsStreamForWrite())
                    {
                        using (BinaryWriter writer = new BinaryWriter(outputStream))
                        {
                            writer.Write((UInt64)encodingTable.Count); // 写入字典大小
                            writer.Write((byte)(8 - ((encodedText.Length - 1) % 8 + 1))); // 写入额外的位数
                            writer.Write((UInt64)encodedText.Length); // 写入编码后文本的大小

                            foreach (var entry in encodingTable)
                            {
                                writer.Write(entry.Key); // 写入字符
                                writer.Write(entry.Value); // 写入编码
                            }

                            WriteBitString(writer, encodedText.ToString());
                        }
                    }
                }

                MessageDialog finish = new MessageDialog("Huffman压缩已完成");
                await finish.ShowAsync();
            }
            catch (Exception ex)
            {
                throw ex;
            }
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

        private void BuildEncodingTable(HuffmanNode node, string code)
        {
            if (node.Left == null && node.Right == null)
            {
                encodingTable[node.Symbol] = code;
            }
            else
            {
                BuildEncodingTable(node.Left, code + "0");
                BuildEncodingTable(node.Right, code + "1");
            }
        }

        private string DecodeBits(byte[] bitsIn, Dictionary<string, char> decodeTable, byte addtionalBits)
        {
            StringBuilder decodedText = new StringBuilder();
            StringBuilder bitString = new StringBuilder();

            foreach (var b in bitsIn)
            {
                string bit = Convert.ToString(b, 2).PadLeft(8, '0');
                bitString.Append(bit);
            }

            bitString.Remove(bitString.Length - addtionalBits, addtionalBits);

            while (bitString.Length > 0)
            {
                StringBuilder code = new StringBuilder();
                while (bitString.Length > 0)
                {
                    code.Append(bitString[0]); // read one bit
                    bitString.Remove(0, 1); // remove the bit read
                    if (decodeTable.ContainsKey(code.ToString()))
                    {
                        decodedText.Append(decodeTable[code.ToString()]);
                        code.Clear();
                        break;
                    }
                }
            }

            return decodedText.ToString();
        }

        public void DeCompress(string inputFilePath, string outputFilePath)
        {
            byte[] compressedData;
            Dictionary<string, char> dictionary = new Dictionary<string, char>();
            byte additionalBits;

            using (FileStream fs = new FileStream(inputFilePath, FileMode.Open))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                var dictionarySize = (int)reader.ReadUInt64();
                additionalBits = reader.ReadByte();
                var bitsLength = (int)reader.ReadUInt64();

                for (int i = 0; i < dictionarySize; i++)
                {
                    var key = reader.ReadChar(); // symbol
                    var value = reader.ReadString(); // encoded 01 string
                    dictionary.Add(value, key);
                }

                compressedData = reader.ReadBytes(bitsLength);
            }
            string decodedText = DecodeBits(compressedData, dictionary, additionalBits);

            File.WriteAllText(outputFilePath, decodedText);
        }
    }
}
