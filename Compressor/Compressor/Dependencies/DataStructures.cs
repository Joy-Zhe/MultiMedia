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
            BGRAData = new BGRA[pixels.Length / 4]; // 每个像素4个字节，因此长度是pixels的四分之一
            YUVData = new YUV[pixels.Length / 4];
            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte blue = pixels[i];
                byte green = pixels[i + 1];
                byte red = pixels[i + 2];
                byte alpha = pixels[i + 3];

                BGRAData[i / 4] = new BGRA(blue, green, red, alpha);
                YUVData[i / 4] = new YUV(RGB2YUV(BGRAData[i / 4]));
            }
        }

        public void SetYUV(YUV[] yuv)
        {
            this.YUVData = yuv;
            this.BGRAData = new BGRA[yuv.Length];
            for (int i = 0; i < yuv.Length; i++)
            {
                // Convert each YUV value back to BGRA and update BGRAData array
                BGRAData[i] = YUV2RGB(yuv[i]);
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

        private BGRA YUV2RGB(YUV yuv)
        {
            int red = (int)(yuv.Y + 1.13983 * yuv.V);
            int green = (int)(yuv.Y - 0.39465 * yuv.U - 0.58060 * yuv.V);
            int blue = (int)(yuv.Y + 2.03211 * yuv.U);

            // Clamp the values to the byte range 0 to 255
            red = Math.Max(0, Math.Min(255, red));
            green = Math.Max(0, Math.Min(255, green));
            blue = Math.Max(0, Math.Min(255, blue));

            return new BGRA((byte)blue, (byte)green, (byte)red, 255); // Assuming alpha channel is always fully opaque
        }

        public void YUV2RGB()
        {
            for (int i = 0; i < YUVData.Length; i++)
            {
                BGRAData[i] = YUV2RGB(YUVData[i]);
            }
        }
    }

    internal class DataStructures
    {

    }
}
