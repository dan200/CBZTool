﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Threading.Tasks;

namespace Dan200.CBZLib.ImageFilters
{
    public class DenoiseFilter : IImageFilter
    {
        public int KernelSize = 3;
        public int SimilarityThreshold = 10;

        public DenoiseFilter()
        {
        }

        public void ApplyTo(Bitmap image)
        {
            var bits = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            try
            {
                var tasks = new Task[Environment.ProcessorCount];
                int standardSliceHeight = image.Height / tasks.Length;
                int lastSliceHeight = image.Height - (tasks.Length - 1) * standardSliceHeight;
                for (int i = 0; i < tasks.Length; ++i)
                {
                    int thisSliceHeight = (i < tasks.Length - 1) ? standardSliceHeight : lastSliceHeight;
                    var area = new Rectangle(0, i * standardSliceHeight, image.Width, thisSliceHeight);
                    tasks[i] = Task.Run(() => ProcessArea(bits, area));
                }
                Task.WhenAll(tasks).Wait();
            }
            finally
            {
                image.UnlockBits(bits);
            }
        }

        private unsafe void ProcessArea(BitmapData image, Rectangle area)
        {
            // Denoise the image
            byte* bytes = (byte*)image.Scan0;
            for (int channelIdx = 0; channelIdx < 3; ++channelIdx)
            {
                for (int y = area.Top; y < area.Bottom; ++y)
                {
                    byte* pixelAddress = bytes + y * image.Stride + area.Left * 3 + channelIdx;
                    for (int x = area.Left; x < area.Right; ++x)
                    {
                        var brightness = *pixelAddress;
                        int sum = 0;
                        int samples = 0;

                        int startX = Math.Max(x - KernelSize, 0);
                        int endX = Math.Min(x + KernelSize, image.Width - 1);
                        int startY = Math.Max(y - KernelSize, 0);
                        int endY = Math.Min(y + KernelSize, image.Height - 1);
                        byte* neighbourAddress = bytes + startY * image.Stride + startX * 3 + channelIdx;
                        for (int ny = startY; ny <= endY; ny++)
                        {
                            byte* lineStart = neighbourAddress;
                            for (int nx = startX; nx <= endX; nx++)
                            {
                                int neighbourBrightness = *neighbourAddress;
                                if (neighbourBrightness >= brightness - SimilarityThreshold && neighbourBrightness <= brightness + SimilarityThreshold)
                                {
                                    sum += neighbourBrightness;
                                    samples++;
                                }
                                neighbourAddress += 3;
                            }
                            neighbourAddress = lineStart + image.Stride;
                        }

                        *pixelAddress = (byte)(sum / samples);
                        pixelAddress += 3;
                    }
                }
            }
        }
    }
}
