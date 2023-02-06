using Bees.Utilities;
using System.Security.Cryptography;

namespace Bees.World.PlayGround
{
    /// <summary>
    /// Manages the creation of random trees within the playground, including drawing them and hit detection.
    /// </summary>
    internal static class TreeManager
    {
        #region CONSTANTS
        /// <summary>
        /// 
        /// </summary>
        internal const int c_treeStump = 2;

        /// <summary>
        /// How many random trees we create.
        /// </summary>
        private const int c_numberOfTreesToCreateInPlayground = 30;

        /// <summary>
        /// How thick the trunk of the trees are. A bit uniform, but simplifies.
        /// </summary>
        private const int c_treeRadius = 55;

        /// <summary>
        /// The "alpha" we apply for a tree.
        /// </summary>
        internal const int c_alphaForTree = 140;
        #endregion

        /// <summary>
        /// We draw trees at random locations.
        /// We also need to be able to detect if a point is hitting them.
        /// </summary>
        private static readonly List<Point> s_locationOfTrees = new();

        /// <summary>
        /// Resets our trees (none). CreateTreesAtRandomLocations repopulates.
        /// </summary>
        internal static void Reset()
        {
            s_locationOfTrees.Clear();
        }

        /// <summary>
        /// Create the trees at random locations.
        /// </summary>
        internal static void CreateTreesAtRandomLocationsIfNoTreesExist(int width, int height)
        {
            // call Reset() if you want to replace existing trees
            //if (s_locationOfTrees.Count != 0) return;

            for (int i = 0; i < c_numberOfTreesToCreateInPlayground; i++)
            {
                Point treeLocation = new(RandomNumberGenerator.GetInt32(0, width - 160) + 150, RandomNumberGenerator.GetInt32(0, height));

                s_locationOfTrees.Add(treeLocation);
            }
        }

        /// <summary>
        /// Draws a tree.
        /// </summary>
        /// <param name="graphicsOfTrackSilhouetteImage"></param>
        /// <param name="centreOfTree"></param>
        internal static void Draw(Graphics graphicsOfTrackSilhouetteImage, Point centreOfTree)
        {
            using SolidBrush center = new(Color.FromArgb(c_alphaForTree, 134, 95, 66));
            using Pen p = new(Color.FromArgb(c_alphaForTree, 134, 95, 66), 1);

            float diam = 40 + RandomNumberGenerator.GetInt32(-3, 5);
            graphicsOfTrackSilhouetteImage.FillEllipse(center, centreOfTree.X - (diam / 2), centreOfTree.Y - diam / 2, diam, diam);
            PlayGroundCreator.FillCircle(TreeManager.c_treeStump, centreOfTree, (int)diam / 2);

            float angle = 0;
            using SolidBrush green = new(Color.FromArgb(115, RandomNumberGenerator.GetInt32(0, 5), RandomNumberGenerator.GetInt32(150, 200), RandomNumberGenerator.GetInt32(0, 5)));
            graphicsOfTrackSilhouetteImage.FillEllipse(green, centreOfTree.X - diam / 2, centreOfTree.Y - diam / 2, diam, diam);

            for (int i = 0; i < RandomNumberGenerator.GetInt32(15, 30); i++)
            {
                float diamOutside = c_treeRadius / 2 + RandomNumberGenerator.GetInt32(-1, 1);

                angle += RandomNumberGenerator.GetInt32(15, 25);

                int ox = RandomNumberGenerator.GetInt32(-8, 8);
                int oy = RandomNumberGenerator.GetInt32(-8, 8);

                float x1 = centreOfTree.X - diamOutside / 2 + (int)(Math.Sin(angle / 360f * (2 * Math.PI)) * diamOutside) + ox;
                float y1 = centreOfTree.Y - diamOutside / 2 + (int)(Math.Cos(angle / 360f * (2 * Math.PI)) * diamOutside) + oy;

                graphicsOfTrackSilhouetteImage.FillEllipse(green, x1, y1, diamOutside, diamOutside);
            }

            graphicsOfTrackSilhouetteImage.FillEllipse(green, centreOfTree.X - diam / 2, centreOfTree.Y - diam / 2, diam, diam);
            graphicsOfTrackSilhouetteImage.DrawEllipse(Pens.Green, centreOfTree.X - diam / 2, centreOfTree.Y - diam / 2, diam, diam);
        }

        /// <summary>
        /// Detect if point is within trunk of tree.
        /// </summary>
        /// <param name="location"></param>
        /// <returns></returns>
        internal static bool Hit(Point location)
        {
            // near to a tree?
            for (int i = 0; i < s_locationOfTrees.Count; i++)
            {
                if (MathUtils.DistanceBetweenTwoPoints(location, s_locationOfTrees[i]) < c_treeRadius + 2)
                {
                    return true; // location is within tree
                }
            }

            return false;
        }

        /// <summary>
        /// Draws all the trees.
        /// </summary>
        /// <param name="graphicsPlayGroundAsImage"></param>
        internal static void DrawAll(Graphics graphicsPlayGroundAsImage)
        {
            foreach (Point locationOfTree in s_locationOfTrees)
            {
                Draw(graphicsPlayGroundAsImage, locationOfTree);
            }
        }

        internal static void AddAcceleratedTrainingTrees(int width, int height)
        {
            if (s_locationOfTrees.Count != 0) return;

            s_locationOfTrees.Add(new(80, 100));

            s_locationOfTrees.Add(new(30, 170));

            s_locationOfTrees.Add(new(55, 250));

            CreateTreesAtRandomLocationsIfNoTreesExist(width, height);
        }
    }
}