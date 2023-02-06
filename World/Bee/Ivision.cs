using Bees.Learn;
using Bees.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bees.World.Bee
{
    /// <summary>
    /// Interface for vision, allowing alternate approaches.
    /// </summary>
    internal interface IVision
    {
        /// <summary>
        /// Depending on the type of vision, the number of neurons vary, this provides the correct amount.
        /// </summary>
        /// <returns></returns>
        public int NeuronsRequiredForVision();

        /// <summary>
        /// This returns a double[] containing the output of the vision system
        /// </summary>
        /// <param name="id">Of the bee.</param>
        /// <param name="AngleLookingInDegrees">Where the bee is looking.</param>
        /// <param name="location">Where the bee is located</param>
        /// <returns></returns>
        public double[] VisionSensorOutput(int id, double AngleLookingInDegrees, PointF location);

        /// <summary>
        /// Stores the sensor output per bee.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="sensor"></param>
        public void SetLIDAR(int id, double[] sensor);

        /// <summary>
        /// Draws the sensor.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="graphics"></param>
        /// <param name="visualCenterPoint"></param>
        public void DrawSensorToImage(int id, Graphics graphics, PointF visualCenterPoint);

        internal static List<AIControlledBee> GetBeesThatAreCloseEnoughToCollideWith(int id, PointF location, int searchDistanceInPixels)
        {
            // make a list of bees in range
            List<AIControlledBee> beesInRange = new();

            foreach (int beeToCheck in BeeController.s_bees.Keys)
            {
                AIControlledBee beeingChecked = BeeController.s_bees[beeToCheck];

                if (beeToCheck == id || beeingChecked.HasBeenEliminated) continue;

                PointF pBee = beeingChecked.Location;

                // if either x or y is too large, it is out of range: Avoid SQRT
                if (Math.Abs(pBee.X - location.X) > searchDistanceInPixels) continue;
                if (Math.Abs(pBee.Y - location.Y) > searchDistanceInPixels) continue;

                // too far as the crow flies
                if (MathUtils.DistanceBetweenTwoPoints(pBee, location) > searchDistanceInPixels) continue;

                beesInRange.Add(beeingChecked);
            }

            return beesInRange;
        }


    }
}