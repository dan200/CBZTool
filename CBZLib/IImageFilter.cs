using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace Dan200.CBZLib
{
    public interface IImageFilter
    {
        void ApplyTo(Bitmap bitmap);
    }
}
