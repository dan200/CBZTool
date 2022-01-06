using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Threading.Tasks;

namespace Dan200.CBZTool
{
    internal class WhiteBalanceFilter : IImageFilter
    {
        public float BlackProportion = 0.006f;
        public float WhiteProportion = 0.006f;
        public float Margin = 0.05f;

        public WhiteBalanceFilter()
        {
        }

        public void Filter(Bitmap image)
        {
            var bits = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            try
            {
                int margin = (int)(image.Width * Margin);
                var area = new Rectangle(margin, margin, image.Width - 2 * margin, image.Height - 2 * margin);
                var tasks = new Task[3];
                for (int i = 0; i < tasks.Length; ++i)
                {
                    int channelIdx = i;
                    tasks[i] = Task.Run(() => ProcessChannel(bits, area, channelIdx));
                }
                Task.WhenAll(tasks).Wait();
            }
            finally
            {
                image.UnlockBits(bits);
            }
        }

        struct Histogram
        {
            public int[] Samples;
            public int TotalSamples;
        }

        private static unsafe Histogram BuildHistogram(BitmapData bits, int channel, Rectangle rectangle)
        {
            // Build a histogram of colours in the image
            var result = new Histogram();
            result.Samples = new int[256];
            result.TotalSamples = rectangle.Width * rectangle.Height;

            byte* bytes = (byte*)bits.Scan0;
            for (int y = rectangle.Top; y < rectangle.Bottom; ++y)
            {
                byte* pixelAddress = bytes + y * bits.Stride + channel;
                for (int x = rectangle.Left; x < rectangle.Right; ++x)
                {
                    var brightness = *pixelAddress;
                    result.Samples[brightness]++;
                    pixelAddress += 3;
                }
            }

            return result;
        }

        private static float GetPercentile(Histogram histogram, float targetFraction)
        {
            // Find the brightness value which incorporates at least the specified fraction of pixels in the image
            int count = 0;
            int targetCount = (int)(targetFraction * histogram.TotalSamples);
            for (int i = 0; i < histogram.Samples.Length; ++i)
            {
                count += histogram.Samples[i];
                if (count > targetCount)
                {
                    return (float)i / (float)(histogram.Samples.Length - 1.0f);
                }
            }
            return 1.0f;
        }

        private unsafe void ProcessChannel(BitmapData image, Rectangle area, int channelIdx)
        {           
            // Analyse the image
            Histogram histogram = BuildHistogram(image, channelIdx, area);
            float lowInput = GetPercentile(histogram, BlackProportion);
            float highInput = GetPercentile(histogram, 1.0f - WhiteProportion);

            // Process the image
            byte* bytes = (byte*)image.Scan0;
            for (int y = 0; y < image.Height; ++y)
            {
                byte* pixelAddress = bytes + y * image.Stride + channelIdx;
                for (int x = 0; x < image.Width; ++x)
                {
                    var brightness = *pixelAddress;
                    var brightnessFrac = (float)brightness / 255.0f;
                    var targetBrightnessFrac = (brightnessFrac - lowInput) / (highInput - lowInput);
                    var targetBrightness = (byte)Math.Min(Math.Max((int)(targetBrightnessFrac * 255.0f), 0), 255);
                    *pixelAddress = targetBrightness;
                    pixelAddress += 3;
                }
            }
        }
    }
}
