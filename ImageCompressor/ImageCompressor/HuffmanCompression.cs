using System.IO;
using System.Text;

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
    private Dictionary<string, char> decodingTable = new Dictionary<string, char>();
    
    public void Compress(string inputFilePath, string outputFilePath)
    {
        var text = File.ReadAllText(inputFilePath);

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
        
        // Decoding Table
        decodingTable = encodingTable.ToDictionary(pair => pair.Value, pair => pair.Key);

        // Compress
        var encodedText = new StringBuilder();
        foreach (var c in text) encodedText.Append(encodingTable[c]);
        
        // File.WriteAllText(outputFilePath, encodedText.ToString());
        using (FileStream fs = new FileStream(outputFilePath, FileMode.Create))
        using (BinaryWriter writer = new BinaryWriter(fs))
        {
            writer.Write((UInt64)encodingTable.Count); // write Dictionary size
            writer.Write((byte)(8 - ((encodedText.Length - 1) % 8 + 1))); // additional bits
            writer.Write((UInt64)encodedText.Length); // write encoded text size

            foreach (var entry in encodingTable)
            {
                writer.Write(entry.Key);
                writer.Write(entry.Value);
            }
            
            WriteBitString(writer, encodedText.ToString());
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