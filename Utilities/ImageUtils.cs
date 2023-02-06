using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Bees.Utilities;

/// <summary>
/// Image utilities.
/// </summary>
internal static class ImageUtils
{
    /// <summary>
    /// Rotates a bitmap.
    /// </summary>
    /// <param name="bitmap"></param>
    /// <param name="angleInDegrees"></param>
    /// <returns></returns>
    internal static Bitmap RotateBitmapWithColoredBackground(Bitmap bitmap, double angleInDegrees)
    {
        Bitmap returnBitmap = new(bitmap.Width, bitmap.Height, PixelFormat.Format32bppPArgb);

        using Graphics graphics = Graphics.FromImage(returnBitmap);

        graphics.InterpolationMode = InterpolationMode.NearestNeighbor; // rough quality
        graphics.CompositingQuality = CompositingQuality.HighSpeed;
        graphics.TranslateTransform((float)bitmap.Width / 2, (float)bitmap.Height / 2); // to center about middle, we need to move the point of rotation to middle
        graphics.RotateTransform((float)angleInDegrees);
        graphics.TranslateTransform(-(float)bitmap.Width / 2, -(float)bitmap.Height / 2); // undo the point of rotation

        var savedCompositingMode = graphics.CompositingMode;
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.DrawImageUnscaled(bitmap, new Point(0, 0));
        graphics.CompositingMode = savedCompositingMode;

        return returnBitmap;
    }

    /// <summary>
    /// Resizes an image.
    /// </summary>
    /// <param name="image"></param>
    /// <param name="canvasWidth"></param>
    /// <param name="canvasHeight"></param>
    /// <returns></returns>
    internal static Image ResizeImage(Image image, int canvasWidth, int canvasHeight)
    {
        int originalWidth = image.Width;
        int originalHeight = image.Height;

        Image thumbnail = new Bitmap(canvasWidth, canvasHeight); // changed parm names

        using Graphics graphic = Graphics.FromImage(thumbnail);

        graphic.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphic.SmoothingMode = SmoothingMode.HighQuality;
        graphic.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphic.CompositingQuality = CompositingQuality.HighQuality;

        // Figure out the ratio
        double ratioX = canvasWidth / (double)originalWidth;
        double ratioY = canvasHeight / (double)originalHeight;

        double ratio = ratioX < ratioY ? ratioX : ratioY; // use whichever multiplier is smaller

        // now we can get the new height and width
        int newHeight = Convert.ToInt32(originalHeight * ratio);
        int newWidth = Convert.ToInt32(originalWidth * ratio);

        // Now calculate the X,Y position of the upper-left corner 
        // (one of these will always be zero)
        int posX = Convert.ToInt32((canvasWidth - originalWidth * ratio) / 2);
        int posY = Convert.ToInt32((canvasHeight - originalHeight * ratio) / 2);

        graphic.Clear(Color.Transparent); // white padding
        graphic.DrawImage(image, posX, posY, newWidth, newHeight);

        return thumbnail;
    }

}