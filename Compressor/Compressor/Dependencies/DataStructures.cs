﻿using System;
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
        // for down sampling
        public double[] downsampledY; // downsampled YUV data
        public double[] downsampledU;
        public double[] downsampledV;
        // image size
        private uint width;
        private uint height;
        private byte downsampledType; // 444:0x00, 420:0x01

        public void DownSampling()
        {
            if (this.downsampledType == 0x00) // 4:4:4
            {
                downsampledY = new double[width * height];
                downsampledU = new double[width * height];
                downsampledV = new double[width * height];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        downsampledY[y * width + x] = this.YUVData[y * width + x].Y;
                        downsampledU[y * width + x] = this.YUVData[y * width + x].U;
                        downsampledV[y * width + x] = this.YUVData[y * width + x].V;
                    }
                }
                // this.SetDownsampledUV(downsampledU, downsampledV);
            }
            else if (this.downsampledType == 0x01) // 4:2:0
            {
                downsampledY = new double[width * height];
                downsampledU = new double[width * height / 4];
                downsampledV = new double[width * height / 4];
                for (int y = 0; y < height; y += 2)
                {
                    for (int x = 0; x < width; x += 2)
                    {
                        double sumU = 0.0;
                        double sumV = 0.0;
                        for (int i = 0; i < 2; i++)
                        {
                            for (int j = 0; j < 2; j++)
                            {
                                if (y + i >= this.height || x + j >= this.width)
                                {
                                    sumU += this.YUVData[(y) * width + (x)].U;
                                    sumV += this.YUVData[(y) * width + (x)].V;
                                }
                                else
                                {
                                    sumU += this.YUVData[(y + i) * width + (x + j)].U;
                                    sumV += this.YUVData[(y + i) * width + (x + j)].V;
                                    downsampledY[(y + i) * width + (x + j)] = this.YUVData[(y + i) * width + (x + j)].Y; // Y will not be downsampled
                                }
                            }
                        }
                        
                        var u = sumU / 4;
                        var v = sumV / 4;
                        downsampledU[y / 2 * width / 2 + x / 2] = u;
                        downsampledV[y / 2 * width / 2 + x / 2] = v;
                    }
                }
                // this.SetDownsampledUV(downsampledU, downsampledV);
            }
        }
        
        public void SetDownSampleType(byte type)
        {
            this.downsampledType = type;
        }
        
        public byte GetDownSampleType()
        {
            return downsampledType;
        }
        public void SetDownsampledUV(double[] downU, double[] downV)
        {
            this.downsampledU = downU;
            this.downsampledV = downV;
        }

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

        public void SetYUV(double[] Y, double[] U, double[] V)
        {
            if (Y.Length != U.Length || U.Length != V.Length)
            {
                throw new ArgumentException("Y, U, and V arrays must have the same length.");
            }

            int length = Y.Length;
            this.YUVData = new YUV[length];
            this.BGRAData = new BGRA[length];

            for (int i = 0; i < length; i++)
            {
                this.YUVData[i] = new YUV(Y[i], U[i], V[i]);
                this.BGRAData[i] = YUV2RGB(this.YUVData[i]);
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
            double u = -0.14713 * input.red - 0.28886 * input.green + 0.436 * input.blue;
            double v = 0.615 * input.red - 0.51499 * input.green - 0.10001 * input.blue;

            return new YUV(y, u, v);
        }

        private BGRA YUV2RGB(YUV yuv)
        {
            int red = (int)(yuv.Y + 1.13983 * (yuv.V));
            int green = (int)(yuv.Y - 0.39465 * (yuv.U) - 0.58060 * (yuv.V));
            int blue = (int)(yuv.Y + 2.03211 * (yuv.U));

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
