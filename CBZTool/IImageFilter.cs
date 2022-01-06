using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace Dan200.CBZTool
{
    internal interface IImageFilter
    {
        void Filter(Bitmap bitmap);
    }
}
