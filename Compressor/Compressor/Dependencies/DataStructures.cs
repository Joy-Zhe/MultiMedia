using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compressor.Dependencies
{
    public struct BGRA
    {
        public byte blue;
        public byte green;
        public byte red;
        public byte alpha;

        public BGRA(byte blue, byte green, byte red, byte alpha)
        {
            this.blue = blue;
            this.green = green;
            this.red = red;
            this.alpha = alpha;
        }
    }

    public struct YUV
    {
        public double Y;
        public double U;
        public double V;

        public YUV(double Y, double U, double V)
        {
            this.Y = Y;
            this.U = U;
            this.V = V;
        }

        public YUV(YUV yuv)
        {
            this.Y = yuv.Y;
            this.U = yuv.U;
            this.V = yuv.V;
        }
    }

    public struct ImgData
    {
        private byte[] pixels;
        public BGRA[] BGRAData;
        public YUV[] YUVData;
        private uint width;
        private uint height;

        public byte[] GetPixels()
        {
            return pixels;
        }

        public uint GetWidth()
        {
            return width;
        }

        public uint GetHeight()
        {
            return height;
        }

        public void SetPixels(byte[] pixels)
        {
            this.pixels = pixels;
            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte blue = pixels[i];
                byte green = pixels[i + 1];
                byte red = pixels[i + 2];
                byte alpha = pixels[i + 3];

                BGRAData[i] = new BGRA(blue, green, red, alpha);
                YUVData[i] = new YUV(RGB2YUV(BGRAData[i]));
            }
        }

        public void SetWH(uint width, uint height)
        {
            this.width = width;
            this.height = height;
        }

        private YUV RGB2YUV(BGRA input)
        {
            double y = 0.299 * input.red + 0.587 * input.green + 0.114 * input.blue;
            double u = -0.147 * input.red - 0.289 * input.green + 0.437 * input.blue;
            double v = 0.615 * input.red - 0.515 * input.green - 0.100 * input.blue;

            return new YUV(y, u, v);
        }

        public void YUV2RGB()
        {

        }
    }

    internal class DataStructures
    {

    }
}
