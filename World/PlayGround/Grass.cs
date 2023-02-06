namespace Bees.World.PlayGround
{
    /// <summary>
    /// Uses a tiled grass image to draw grass.
    /// </summary>
    internal static class Grass
    {
        /// <summary>
        /// Draw grass as a background.
        /// </summary>
        /// <param name="graphicsPlayGroundAsImage"></param>
        internal static void Draw(Graphics graphicsPlayGroundAsImage, int width, int height)
        {
            using Bitmap bitmapOfGrass = new("UX/Resources/grass.png");

            // cover the whole bitmap with grasss tiless

            for (int x = 5; x < width + bitmapOfGrass.Width - 10; x += bitmapOfGrass.Width - 1)
            {
                for (int y = 5; y < height + bitmapOfGrass.Height - 10; y += bitmapOfGrass.Height - 1)
                {
                    graphicsPlayGroundAsImage.DrawImageUnscaled(bitmapOfGrass, x, y);
                }
            }
        }
    }
}
