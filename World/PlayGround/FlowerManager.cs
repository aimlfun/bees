using Bees.Utilities;
using System.Security.Cryptography;

namespace Bees.World.PlayGround
{
    /// <summary>
    /// Manages the flowers, and is able to draw them and detect if a location is within the centre of the flower.
    /// </summary>
    internal static class FlowerManager
    {
        #region CONSTANTS
        /// <summary>
        /// How many flowers to draw.
        /// </summary>
        internal const int c_numberOfFlowers = 130;

        /// <summary>
        /// 
        /// </summary>
        internal const int c_alphaForFlower = 200;

        /// <summary>
        /// 
        /// </summary>
        internal const int c_flower = 1;

        /// <summary>
        /// Diameter of the each flower (centre circle)
        /// </summary>
        internal const int c_flowerDiameter = 6;
        #endregion

        /// <summary>
        /// We draw flowers, but track them in an array so that we can change their colour as the bees visit.
        /// </summary>
        private static readonly List<Point> s_locationOfFlowers = new();

        /// <summary>
        /// Returns true if there are flowers.
        /// When nectar is gathered, we delete the flower.
        /// </summary>
        internal static bool HasNectarRemaining
        {
            get
            {
                return s_locationOfFlowers.Count > 0;
            }
        }

        /// <summary>
        /// Resets the flowers (removes them all);
        /// </summary>
        internal static void Reset()
        {
            s_locationOfFlowers.Clear();
        }

        /// <summary>
        /// Creates the flowers.
        /// </summary>
        /// <param name="width">Width of the playground</.param>
        /// <param name="height">Height of the playground.</param>
        internal static void CreateFlowersAtRandomLocations(int width, int height)
        {
            //if (s_locationOfFlowers.Count != 0) return;

            for (int i = 0; i < c_numberOfFlowers; i++)
            {
                bool added = false;

                // keep trying, until we are successful at finding a place to put the flower where
                // it doesn't clash with another flower or tree
                while (!added)
                {
                    Point flowerLocation = new(RandomNumberGenerator.GetInt32(0, width - 150) + 140, RandomNumberGenerator.GetInt32(20, height - 40));

                    if (!PlayGroundCreator.TreeOrFlowerNearTo(flowerLocation))
                    {
                        s_locationOfFlowers.Add(flowerLocation);
                        added = true;
                    }
                }
            }
        }

        /// <summary>
        /// Draws all the flowers.
        /// </summary>
        /// <param name="graphicsPlayGroundAsImage"></param>
        internal static void DrawAll(Graphics graphicsPlayGroundAsImage)
        {
            foreach (Point treat in s_locationOfFlowers)
            {
                Draw(graphicsPlayGroundAsImage, treat, true);
            }
        }

        /// <summary>
        /// Draws a flower.
        /// </summary>
        /// <param name="graphicsOfTrackSilhouetteImage">The canvas to draw on.</param>
        /// <param name="flowerLocation">where to paint it</param>
        /// <param name="hasNectar">true - paint purple (it has nectar)</param>
        internal static void Draw(Graphics graphicsOfTrackSilhouetteImage, Point flowerLocation, bool hasNectar)
        {
            using SolidBrush yellow = new(Color.FromArgb(170, 255, 255, 255));

            for (int i = 0; i < 9; i++)
            {
                float angle = 360f / 9 * i;
                double angleInRadians = angle / 360f * (2 * Math.PI);

                graphicsOfTrackSilhouetteImage.FillEllipse(yellow,
                    flowerLocation.X - c_flowerDiameter / 2 + (int)(Math.Sin(angleInRadians) * c_flowerDiameter),
                    flowerLocation.Y - c_flowerDiameter / 2 + (int)(Math.Cos(angleInRadians) * c_flowerDiameter),
                    c_flowerDiameter, c_flowerDiameter);
            }

            using SolidBrush center = new(hasNectar
                                            ? Color.FromArgb(255, 194, 126, 143)
                                            : Color.FromArgb(255, 200, 200, 200));

            graphicsOfTrackSilhouetteImage.FillEllipse(center,
                flowerLocation.X - c_flowerDiameter / 2,
                flowerLocation.Y - c_flowerDiameter / 2,
                c_flowerDiameter, c_flowerDiameter);

            PlayGroundCreator.FillCircle((byte)(hasNectar ? c_flower : 0), flowerLocation, c_flowerDiameter / 2);
        }

        /// <summary>
        /// Detects to see if location is within the centre circle of the flower.
        /// </summary>
        /// <param name="location"></param>
        /// <returns></returns>
        internal static bool Hit(Point location)
        {
            // near to a flower?
            for (int i = 0; i < s_locationOfFlowers.Count; i++)
            {
                if (MathUtils.DistanceBetweenTwoPoints(location, s_locationOfFlowers[i]) < c_flowerDiameter + 2)
                {
                    return true;
                }
            }

            return false;
        }


        /// <summary>
        /// Detects if a flower is near a specific location in "p".
        /// </summary>
        /// <param name="p"></param>
        internal static bool FlowerAt(PointF p)
        {
            int radiusOfFlowerInPX = c_flowerDiameter / 2; // we process in radius, not diameter, so calc once here

            for (int i = 0; i < s_locationOfFlowers.Count; i++)
            {
                Point treat = s_locationOfFlowers[i];

                if (MathUtils.DistanceBetweenTwoPoints(p, treat) < radiusOfFlowerInPX + 2)
                {
                    return true;
                }
            }

            return false; // force re-draw.
        }

        /// <summary>
        /// Removes a treat.
        /// </summary>
        /// <param name="p"></param>
        internal static void RemoveAt(PointF p)
        {
            int radiusOfFlowerInPX = c_flowerDiameter / 2; // we process in radius, not diameter, so calc once here

            for (int i = 0; i < s_locationOfFlowers.Count; i++)
            {
                Point flower = s_locationOfFlowers[i];
                if (MathUtils.DistanceBetweenTwoPoints(p, flower) < radiusOfFlowerInPX + 2)
                {
                    PlayGroundCreator.s_silhouette = null;

                    // remove thee flower
                    using Graphics graphics = Graphics.FromImage(PlayGroundCreator.s_playGroundAsImage);
                    Draw(graphics, s_locationOfFlowers[i], false);
                    graphics.Flush();

                    s_locationOfFlowers.RemoveAt(i);
                    return;
                }
            }
        }

        internal static void AddAcceleratedTrainingFlowers(int width, int height)
        {
            s_locationOfFlowers.Clear();
            s_locationOfFlowers.Add(new(15, 15));
            s_locationOfFlowers.Add(new(30, 30));
            s_locationOfFlowers.Add(new(50, 45));
            s_locationOfFlowers.Add(new(30, 45));
            s_locationOfFlowers.Add(new(80, 60));
            s_locationOfFlowers.Add(new(50, 75));
            s_locationOfFlowers.Add(new(70, 75));
            s_locationOfFlowers.Add(new(30, 85));
            s_locationOfFlowers.Add(new(30, 100));
            CreateFlowersAtRandomLocations(width, height);
        }
    }
}