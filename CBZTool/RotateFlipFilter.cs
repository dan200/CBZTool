using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Threading.Tasks;

namespace Dan200.CBZTool
{
    internal class RotateFlipFilter : IImageFilter
    {
        public RotateFlipType RotateFlipType;

        public RotateFlipFilter(RotateFlipType rotateFlipType)
        {
            RotateFlipType = rotateFlipType;
        }

        public void Filter(Bitmap image)
        {
            image.RotateFlip(RotateFlipType);
        }
    }
}
