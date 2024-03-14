using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace ImageCompressor;

    public class HuffmanNode
    {
        public char Symbol { get; set; }
        public int Frequency { get; set; }
        public HuffmanNode Left { get; set; }
        public HuffmanNode Right { get; set; }
    }
    
    public class HuffmanCompression
    {
        private Dictionary<char, string> encodingTable = new Dictionary<char, string>();

        public void Compress(string inputFilePath, string outputFilePath)
        {
            string text = File.ReadAllText(inputFilePath);
            
            // character frequency
            Dictionary<char, int> frequencyTable = new Dictionary<char, int>();
            foreach (char c in text)
            {
                if (frequencyTable.ContainsKey(c))
                {
                    frequencyTable[c]++;
                }
                else
                {
                    frequencyTable[c] = 1;
                }
            }
            
            // Huffman Tree
            List<HuffmanNode> nodes = frequencyTable.Select(pair => new HuffmanNode
            {
                Symbol = pair.Key,
                Frequency = pair.Value
            }).ToList();

            while (nodes.Count > 1)
            {
                nodes = nodes.OrderBy(node => node.Frequency).ToList();
                HuffmanNode parent = new HuffmanNode
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
            StringBuilder encodedText = new StringBuilder();
            foreach (char c in text)
            {
                encodedText.Append(encodingTable[c]);
            }
            
            File.WriteAllText(outputFilePath, encodedText.ToString());
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
    }
