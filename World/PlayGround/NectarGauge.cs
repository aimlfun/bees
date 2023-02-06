using Bees.Learn;
using Bees.World.Bee;

namespace Bees.World.PlayGround
{
    /// <summary>
    /// Represents a nectar gauge bar tracking nectar collected, return and not harvested.
    /// </summary>
    internal static class NectarGauge
    {
        /*
         *  Gauge:
         *  +----------+------+------------+
         *  |xxxxxxxxxx|######|            |
         *  +----------+------+------------+
         *    ^nectar    ^non returned  ^ not harvested
         *    (yellow)   (brown)        (black)
         */

        /// <summary>
        /// The red triangle.
        /// </summary>
        private static int s_bestNectarReturnedToHive = 0;

        /// <summary>
        /// Outline for gauge.
        /// </summary>
        private static readonly Pen s_penThickBlackLine = new(Color.Black, 5);

        /// <summary>
        /// Harvested amount on gauge.
        /// </summary>
        private static readonly Pen s_penThickGreyLine = new(Color.Gray, 5);

        /// <summary>
        /// Amount of nectar returned to hive.
        /// </summary>
        private static readonly Pen s_penThickYellowLine = new(Color.Yellow, 5);

        /// <summary>
        /// Top highlight for gauge (aesthetics).
        /// </summary>
        private static readonly Pen s_penHighlight = new(Color.FromArgb(70, 255, 255, 255));

        /// <summary>
        /// Bottom highlight for gauge (aesthetics).
        /// </summary>
        private static readonly Pen s_penLowlight = new(Color.FromArgb(70, 0, 0, 0));

        /// <summary>
        /// Draw the simple guage showing how much nectar was collected, how many flowers etc.
        /// </summary>
        /// <param name="graphics"></param>
        internal static void Draw(Graphics graphics, int height)
        {
            float pixelWidthOfGauge = 88f;

            // use a scale to plot the correct width based on number of flowers
            float scale = pixelWidthOfGauge / FlowerManager.c_numberOfFlowers;

            int nectarHarvested = 0;
            int totalNectarReturnedToHive = 0;

            foreach (AIControlledBee bee in BeeController.s_bees.Values)
            {
                nectarHarvested += bee.nectarCollected;
                totalNectarReturnedToHive += bee.currentTaskOfBee == AIControlledBee.BeeTask.Sleep ? bee.nectarCollected : 0;
            }

            int gaugeLeftPX = 8;
            int lineY = height - 14;

            // see diagram at top of class, we render a black line then overlay what bees collected, and finally what
            // they returned. The order is because they are either equal or decreasing in size. Yellow cannot exceeed grey
            graphics.DrawLine(s_penThickBlackLine, gaugeLeftPX, lineY, gaugeLeftPX + pixelWidthOfGauge, lineY); // full width of gauge
            graphics.DrawLine(s_penThickGreyLine, gaugeLeftPX, lineY, gaugeLeftPX + scale * nectarHarvested, lineY); // nectar bees harvested
            graphics.DrawLine(s_penThickYellowLine, gaugeLeftPX, lineY, gaugeLeftPX + scale * totalNectarReturnedToHive, lineY); // nectar bees returned.

            // to make it look my stylish, we add highlights at top and bottom using an "alpha" transparency
            graphics.DrawLine(s_penHighlight, gaugeLeftPX, lineY - 2, gaugeLeftPX + pixelWidthOfGauge, lineY - 2);
            graphics.DrawLine(s_penLowlight, gaugeLeftPX, lineY + 2, gaugeLeftPX + pixelWidthOfGauge, lineY + 2);

            // box around gauge
            graphics.DrawRectangle(Pens.Black, gaugeLeftPX, lineY - 3, pixelWidthOfGauge, 6);

            // track the best score
            if (totalNectarReturnedToHive > s_bestNectarReturnedToHive) s_bestNectarReturnedToHive = totalNectarReturnedToHive;

            //         pointer
            //       |
            //       |
            //       | 
            //      / \
            //     /   \  red triangle
            //     -----

            float pointer = gaugeLeftPX + s_bestNectarReturnedToHive * scale;

            graphics.DrawLine(Pens.Red, pointer, lineY - 3, pointer, lineY + 2);

            // fill a triangle pointing to the level kept
            graphics.FillPolygon(Brushes.Red,
                                 new PointF[]
                                    { new PointF(pointer,  lineY + 2),
                                      new PointF(pointer - 5,lineY + 2 + 5),
                                      new PointF(pointer + 5,lineY + 2 + 5)});
        }
    }
}
