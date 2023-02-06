using Bees.Settings;

namespace Bees.World.PlayGround
{
    /// <summary>
    /// Draws a hive. That's a gold box with hexagons cut out.
    /// Manages the sleeping quarters.
    /// </summary>
    internal static class HiveManager
    {
        /// <summary>
        /// Status of the bed within the hive.
        /// </summary>
        enum BedStatus { Empty, Occupied }

        /// <summary>
        /// Tracks which beds have a bee sleeping in them.
        /// </summary>
        private static readonly BedStatus[] s_hiveOccupants = new BedStatus[24];

        /// <summary>
        /// 
        /// </summary>
        private readonly static Font s_fontForHiveNumbering = new("Arial", 10);

        /// <summary>
        /// Draws the hive (honey background with hexagons).
        /// </summary>
        /// <param name="graphicsPlayGroundAsImage"></param>
        /// <param name="heighte"></param>
        internal static void Draw(Graphics graphicsPlayGroundAsImage, int height)
        {
            graphicsPlayGroundAsImage.FillRectangle(Brushes.Gold, 5, 281, 101, height - 285);

            // draw the hive
            for (int id = 0; id < Config.NumberOfAIBeesInHive; id++)
            {
                PointF somewhere = HiveIndexToPosition(id);
                DrawHexagonAt(graphicsPlayGroundAsImage, somewhere);

                SizeF size = graphicsPlayGroundAsImage.MeasureString(id.ToString(), s_fontForHiveNumbering);
                graphicsPlayGroundAsImage.DrawString(id.ToString(), s_fontForHiveNumbering, Brushes.Yellow, new PointF(2 + somewhere.X - size.Width / 2, somewhere.Y - size.Height / 2));
            }
        }

        /// <summary>
        /// Returns the centre of the hive hexagon bed for any given id.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        internal static PointF HiveIndexToPosition(int id)
        {
            /*  
             *   25   52   79
             *   |    |    |
             *  +----------------+ ___ 298
             *  | < 0>      < 2> |
             *  |      < 1>      |
             *  | < 3>      < 5> |
             *  |      < 4>      |
             *  | < 6>      < 8> |
             *  |      < 7>      |
             *  | < 9>      <11> |
             *  |      <10>      |
             *  | <12>      <14> |
             *  |      <13>      |
             *  | <15>      <17> |
             *  |      <16>      |
             *  | <18>      <20> |
             *  |       <19>     |
             *  | <21>      <23> |
             *  |      <22>      |
             *  +----------------+
             *
             */

            return new(id % 3 * 27 + 25,
                       298 + id / 3 * 30 + 15 * ((id - 1) % 3 == 0 ? 1 : 0));
        }

        /// <summary>
        /// The bees come out of their "hexagon", their sleeping quarters. I have no idea what happens IRL.
        /// </summary>
        /// <param name="graphicsPlayGroundAsImage"></param>
        /// <param name="somewhere"></param>
        private static void DrawHexagonAt(Graphics graphicsPlayGroundAsImage, PointF somewhere)
        {
            List<PointF> hexagon = new();

            for (double i = 0; i < Math.PI * 2; i += Math.PI / 3)
            {
                float x = (float)(Math.Cos(i) * 15) + somewhere.X;
                float y = (float)(Math.Sin(i) * 15) + somewhere.Y;

                hexagon.Add(new PointF(x, y));
            }

            // black hexagon
            graphicsPlayGroundAsImage.FillPolygon(Brushes.Black, hexagon.ToArray());
        }

        /// <summary>
        /// Resets the beds -> no bees in the hive.
        /// </summary>
        internal static void Reset()
        {
            // mark the beds empty
            for (int i = Config.NumberOfAIBeesInHive - 1; i >= 0; i--)
            {
                s_hiveOccupants[i] = BedStatus.Empty;
            }
        }

        /// <summary>
        /// Gets the next free bed. The bees target the next free bed.
        /// </summary>
        /// <returns></returns>
        internal static PointF GetNextFreeBed()
        {
            // we allocate beds furthest away from hive opening
            for (int i = Config.NumberOfAIBeesInHive - 1; i >= 0; i--)
            {
                if (s_hiveOccupants[i] == BedStatus.Empty) return HiveIndexToPosition(i);
            }

            return HiveIndexToPosition(0);
        }

        /// <summary>
        /// Claim bed, happens once a bee arrives at it's intended destination.
        /// Two cannot claim, as the bees can not overlap.
        /// </summary>
        internal static void ClaimNextAvailableBed()
        {
            for (int i = Config.NumberOfAIBeesInHive - 1; i >= 0; i--)
            {
                // is it occupied?
                if (s_hiveOccupants[i] == BedStatus.Empty)
                {
                    // no, occupy it to prevent it being claimed by another bee.
                    s_hiveOccupants[i] = BedStatus.Occupied;
                    return;
                }
            }
        }
    }
}
