using Bees.Learn;
using Bees.Settings;
using Bees.Utilities;
using Bees.UX.Forms.MainUI;
using Bees.World.Bee;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Text;

namespace Bees.World.PlayGround;

/// <summary>
/// Represents the bee's playground. Is able to make a new random one.
/// </summary>
internal static class PlayGroundCreator
{
    #region CONSTANTS    
    /// <summary>
    /// Indicates a "wall".
    /// </summary>
    internal const int c_wall = 3;
    #endregion

    /// <summary>
    /// Width of play area.
    /// </summary>
    internal static int Width = 900;
    
    /// <summary>
    /// Height of play area.
    /// </summary>
    internal static int Height = 550;

    #region CACHE OF TRACK BITMAP BYTES FOR PERFORMANCE        
    /// <summary>
    /// This is the track bitmap. If null, the track image needs to be locked and copied to it.
    /// </summary>
    private static Bitmap? s_srcTrackBitMap = null;

    /// <summary>
    /// This is the attributes of the track bitmap.
    /// </summary>
    private static BitmapData? s_srcTrackBitMapData;

    /// <summary>
    /// This is a pointer to the track bitmap's data.
    /// </summary>
    private static IntPtr s_srcTrackBitMapDataPtr;

    /// <summary>
    /// Bytes per row of pixels.
    /// </summary>
    private static int s_strideTrack;

    /// <summary>
    /// This is how many bytes the track bitmap is.
    /// </summary>
    private static int s_totalLengthTrack;

    /// <summary>
    /// This is the pixels in the track bitmap.
    /// </summary>
    private static byte[] s_rgbArrayOfBeePlayGround = Array.Empty<byte>();

    /// <summary>
    /// This is how many bytes each pixel occupies in the track bitmap.
    /// </summary>
    private static int s_bytesPerPixelTrack;

    /// <summary>
    /// This is how many bytes per row of the track bitmap image (used to multiply "y" by to get to the correct data).
    /// </summary>
    private static int s_offsetTrack;
    #endregion

    /// <summary>
    /// This is the beautiful hive, trees
    /// </summary>
    internal static Bitmap s_playGroundAsImage = new(1, 1);

    /// <summary>
    /// width x height array of bytes, each indicates whether a flower is at the location or tree, or edge.
    /// </summary>
    private static byte[] s_playGroundSilhouette = new byte[] { };

    /// <summary>
    /// Silhouette of the playground image.
    /// </summary>
    internal static Bitmap? s_silhouette = null;

    #region NIGHT TIME
    /// <summary>
    /// Non-zero means night-time (darkness is overlayed).
    /// 0     = Day time
    /// 1..44 = Night, bees are asleep
    /// 45+   = Dusk or dawn.
    /// </summary>
    private static float s_nightTime = 0;

    /// <summary>
    /// Returns TRUE if bees are sleeping.
    /// </summary>
    internal static bool BeesAreSleeping
    {
        get
        {
            return s_nightTime > 0 && s_nightTime < 11;
        }
    }

    /// <summary>
    /// Sets the playground to "day time" mode (no dark overlay).
    /// </summary>
    internal static void SetDayTime()
    {
        s_nightTime = 0;
    }

    /// <summary>
    /// Sets the playground to "night time mode" (dark overlay).
    /// </summary>
    /// <param name="value"></param>
    internal static void SetNightTime(int value)
    {
        s_nightTime = value;
    }

    /// <summary>
    /// Returns TRUE if night time (dark overlay will be displayed).
    /// </summary>
    internal static bool IsNightTime
    {
        get
        {
            return s_nightTime > 0;
        }
    }
    #endregion   

    /// <summary>
    /// Determines whether pixel at (x,y) is grass.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns>true - pixel safe | false - pixel is wall/tree.</returns>
    internal static bool PixelIsSafe(int x, int y)
    {
        int objectAtLocation = GetPixelType(x, y);

        // are we colliding with a wall or stump?
        if (objectAtLocation == c_wall || objectAtLocation == TreeManager.c_treeStump) return false;

        return true;
    }

    /// <summary>
    /// Draws the bees to the graphics (overlaid on top of the playground).
    /// </summary>
    internal static void DrawEverything(Graphics g)
    {
        // draw the playground.
        g.DrawImageUnscaled(FormMain.s_showSilhouette ? GetSilhouetteAsImage() : s_playGroundAsImage, 0, 0);

        // no bees? nothing to draw
        if (BeeController.s_bees is null) return;

        // draw each of the bees
        foreach (int id in BeeController.s_bees.Keys)
        {
            AIControlledBee bee = BeeController.s_bees[id];

            bee.Render(g);
        }

        // overlays a black alpha to simulate darkness.
        if (IsNightTime)
        {
            AddDarkAlphaOverlayToSignifyDuskToMidnightToDawn(g);
        }
    }

    /// <summary>
    /// To simulate dawn, dusk and night we draw a black rectangle of alpha transparency.
    /// At midnight it's a large alpha of 238 (not very see thru).
    /// At dawn/dusk we overlay a lot lower alpha, giving darkening but bees still visible.
    /// </summary>
    /// <param name="g"></param>
    private static void AddDarkAlphaOverlayToSignifyDuskToMidnightToDawn(Graphics g)
    {
        int alpha = (int)Math.Round(240 - Math.Abs(s_nightTime) * 2).Clamp(0, 255);
        
        using SolidBrush brush = new(Color.FromArgb(alpha, 0, 0, 0));

        // overlay darkness
        g.FillRectangle(brush, 0, 0, Width, Height);
    }

    /// <summary>
    /// Generates and renders the playground (hive + trees + flowers) and stores the output in a Bitmap "s_playGroundAsImage".
    /// To generate, it places random trees and flowers.
    /// An important part of the process is the creation of a "silhouette" - what we use for the bees "vision". It contains pixels that are
    /// purple (flowers) or green (tree trunk) or red (walls).
    /// </summary>
    internal static void RenderAsImage(int width = -1, int height = -1)
    {
        if (width != -1) Width = width;
        if (height != -1) Height = height;

        TreeManager.Reset();
        FlowerManager.Reset();

        s_silhouette = null;
        s_playGroundSilhouette = new byte[Width * Height];
        s_playGroundAsImage = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);

        using Graphics graphicsPlayGroundAsImage = Graphics.FromImage(s_playGroundAsImage);

        graphicsPlayGroundAsImage.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        graphicsPlayGroundAsImage.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

        // edge around screen
        graphicsPlayGroundAsImage.FillRectangle(Brushes.White, 0, 0, Width, Height);

        Grass.Draw(graphicsPlayGroundAsImage, Width, Height);

        // draw the hive background
        HiveManager.Draw(graphicsPlayGroundAsImage, Height);

        using Pen penThickWhiteLine = new(Color.White, 10);

        graphicsPlayGroundAsImage.DrawLines(penThickWhiteLine, new Point[] { new Point(0, 0), new Point(Width - 1, 0), new Point(Width - 1, Height - 1), new Point(0, Height - 1), new Point(0, 0) });

        if (!Config.c_acceleratedTraining)
        {
            // diagonal line left to right midway.
            graphicsPlayGroundAsImage.DrawLine(penThickWhiteLine, 0, 240, 104, 190);

            graphicsPlayGroundAsImage.DrawLine(penThickWhiteLine, 104, 281, 104, Height);
        }
        else
        {
            graphicsPlayGroundAsImage.DrawLine(penThickWhiteLine, 104, 50, 104, Height);
        }
        graphicsPlayGroundAsImage.Flush();

        //  cache the playground as a byte array rather than keep copying it (performance improved)
        CopyImageOfPlaygroundToAnAccessibleInMemoryArray();
        CopyToByteArray();

        if (!Config.c_acceleratedTraining)
        {
            // place random trees
            TreeManager.CreateTreesAtRandomLocationsIfNoTreesExist(Width, Height);

            // place random flowers
            FlowerManager.CreateFlowersAtRandomLocations(Width, Height);
        }
        else
        {
            TreeManager.AddAcceleratedTrainingTrees(Width, Height);
            FlowerManager.AddAcceleratedTrainingFlowers(Width, Height);
        }

        TreeManager.DrawAll(graphicsPlayGroundAsImage);

        FlowerManager.DrawAll(graphicsPlayGroundAsImage);

        if (Config.OutputAsciiArtSilhouette) DumpSilhouetteAsAsciiArt();

        graphicsPlayGroundAsImage.Flush();
    }


    /// <summary>
    /// Outputs the silhouette that is used for hit detection of trees/flowers/walls.
    /// </summary>
    private static void DumpSilhouetteAsAsciiArt()
    {
        StringBuilder sb = new();

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                int indexOriginal = x + y * Width;

                switch (s_playGroundSilhouette[indexOriginal])
                {
                    case 0: sb.Append(' '); break;
                    case FlowerManager.c_flower: sb.Append('*'); break;
                    case TreeManager.c_treeStump: sb.Append('o'); break;
                    case c_wall: sb.Append('#'); break;
                    default: Debugger.Break(); break;
                }
            }

            sb.AppendLine();
        }

        File.WriteAllText(@"c:\temp\SilhouetteAsAsciiArt.txt", sb.ToString());
    }

    /// <summary>
    /// Creates a map of the world based off the alpha.
    /// </summary>
    private static void CopyToByteArray()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                int indexOriginal = x * s_bytesPerPixelTrack + y * s_offsetTrack;

                byte b = s_rgbArrayOfBeePlayGround[indexOriginal]; // use "A" 
                s_playGroundSilhouette[x + y * Width] = AlphaToSimpleValue(b, x);
            }
        }
    }

    /// <summary>
    /// Convert silhouette byte into simple valuue.
    /// </summary>
    /// <param name="b"></param>
    /// <param name="x"></param>
    /// <returns></returns>
    private static byte AlphaToSimpleValue(byte b, int x)
    {
        switch (b)
        {
            case FlowerManager.c_alphaForFlower:
                if (x < 113) return 0;

                return FlowerManager.c_flower;

            case TreeManager.c_alphaForTree: 
                return TreeManager.c_treeStump;
            
            case 255: 
                return c_wall;

            default:
                break;
        }

        return 0;
    }

    /// <summary>
    /// Returns the silhouette pixel at the loction (x,y). 
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    internal static int GetPixelType(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return c_wall;

        return s_playGroundSilhouette[x + y * Width];
    }

    /// <summary>
    /// Detects the presence of a tree or flower, and returns true if one is near.
    /// </summary>
    /// <param name="p"></param>
    /// <returns></returns>
    internal static bool TreeOrFlowerNearTo(Point p)
    {
        // near to a flower?
        if (FlowerManager.Hit(p)) return true;

        // near to a tree?
        if (TreeManager.Hit(p)) return true;

        return false;
    }

    /// <summary>
    /// Cache the playground as a byte array rather than keep copying it (performance improved).
    /// </summary>
    private static void CopyImageOfPlaygroundToAnAccessibleInMemoryArray()
    {
        if (s_playGroundAsImage is null) throw new Exception("s_playGroundAsImage image should be populated before calling this."); // can't cache what has been drawn!

        s_srcTrackBitMap = s_playGroundAsImage;
        s_srcTrackBitMapData = s_srcTrackBitMap.LockBits(new Rectangle(0, 0, s_srcTrackBitMap.Width, s_srcTrackBitMap.Height), ImageLockMode.ReadOnly, s_playGroundAsImage.PixelFormat);
        s_srcTrackBitMapDataPtr = s_srcTrackBitMapData.Scan0;
        s_strideTrack = s_srcTrackBitMapData.Stride;

        s_totalLengthTrack = Math.Abs(s_strideTrack) * s_srcTrackBitMap.Height;
        s_rgbArrayOfBeePlayGround = new byte[s_totalLengthTrack];

        s_bytesPerPixelTrack = Image.GetPixelFormatSize(s_srcTrackBitMapData.PixelFormat) / 8;
        s_offsetTrack = s_strideTrack;
        System.Runtime.InteropServices.Marshal.Copy(s_srcTrackBitMapDataPtr, s_rgbArrayOfBeePlayGround, 0, s_totalLengthTrack);

        s_srcTrackBitMap.UnlockBits(s_srcTrackBitMapData);
    }

    /// <summary>
    /// Paints a circle to the map of the world (tree / flower).
    /// </summary>
    /// <param name="cAlpha"></param>
    /// <param name="location"></param>
    /// <param name="r"></param>
    internal static void FillCircle(byte cAlpha, Point location, int r)
    {
        int r2 = r * r;

        for (int cy = -r; cy <= r; cy++)
        {
            int cx = (int)(Math.Sqrt(r2 - cy * cy) + 0.5);
            int cyy = cy + location.Y;

            for (int z = location.X - cx; z <= location.X + cx; z++)
            {
                if (z < 0 || z >= Width || cyy < 0 || cyy >= Height) continue;

                s_playGroundSilhouette[z + cyy * Width] = cAlpha;
            }
        }
    }

    /// <summary>
    /// Converts the playground image into a bee vision silhouette. 
    /// </summary>
    /// <returns></returns>
    internal static Bitmap GetSilhouetteAsImage()
    {
        if (s_silhouette is not null) return s_silhouette;

        // do not wrap this in using, we are storing it in a static variable.
        Bitmap bitmapSilhouetteStoredForVision = new(Width, Height);

        using Graphics graphics = Graphics.FromImage(bitmapSilhouetteStoredForVision);
        graphics.Clear(Color.DarkGray);

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                int indexOriginal = x + y * Width;

                switch (s_playGroundSilhouette[indexOriginal])
                {
                    case FlowerManager.c_flower: bitmapSilhouetteStoredForVision.SetPixel(x, y, Color.Purple); break;
                    case TreeManager.c_treeStump: bitmapSilhouetteStoredForVision.SetPixel(x, y, Color.Green); break;
                    case c_wall: bitmapSilhouetteStoredForVision.SetPixel(x, y, Color.Red); break;
                }
            }
        }

        s_silhouette = bitmapSilhouetteStoredForVision;

        return bitmapSilhouetteStoredForVision;
    }
}