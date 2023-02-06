using Bees.Learn;
using Bees.Settings;
using Bees.Utilities;
using Bees.World.PlayGround;

namespace Bees.World.Bee;

/// <summary>
/// Simple vision implementation of a LIDAR.
/// </summary>
internal class BeeVisionMono : IVision
{
    /// <summary>
    /// Parallel.ForEach causes issues as we
    /// </summary>
    private readonly object parallelForEachLock = new();

    /// <summary>
    /// LIDAR lines are drawn with this
    /// </summary>
    private readonly static Pen s_penToDrawLidar = new(Color.FromArgb(250, 255, 0, 0));

    /// <summary>
    /// Lines to flowers use this/
    /// </summary>
    private readonly static Pen s_penToDrawFlower = new(Color.FromArgb(250, 100, 250, 100));

    /// <summary>
    /// Used to remember where to draw the LIDAR.
    /// </summary>
    private PointF locationOfOutput = new();

    /// <summary>
    /// Used to draw the LIDAR, contains the values given to the neural network. Correspond to "distances" from colliding (1=close).
    /// </summary>
    readonly Dictionary<int, double[]> lastOutputFromSensor = new();

    /// <summary>
    /// The AI bee needs to know how far it is from the wall, trees and other bees.
    /// 
    /// We do this as a LIDAR approach because it is 2d. 
    /// 
    /// We check pixels in the config defined directions.
    /// 
    /// Typically that would be forwards, diagonally forward to inform it as it turns, and to the sides so it knows how
    /// close it is to the grass.
    /// 
    /// The bee could be moving in any direction, we have to compute LIDAR hits with the forward line pointing
    /// where the bee is going.
    /// 
    ///     \  |  /
    ///      \ | /        __       __
    ///  _____\|/_____     |\  /|\  /|
    ///   +--------+         \  |  /        .¦
    ///   |   AI   |          ) | (      .:¦¦¦
    ///   +--------+          angle      speed
    /// </summary>
    /// <returns>Array of proximities (each 0..1F).</returns>
    /// <param name="AngleLookingInDegrees"></param>
    /// <param name="location"></param>
    /// <returns>Proximities 0..1 of what it sees</returns>
    /// <exception cref="NotImplementedException"></exception>
    public double[] VisionSensorOutput(int id, double AngleLookingInDegrees, PointF location)
    {
        // e.g 
        // input to the neural network
        //   _ \ | / _   
        //   0 1 2 3 4 
        //        
        double[] LIDAROutput = new double[Config.SamplePoints];
        double[] LIDAROutputRaw = new double[Config.SamplePoints];

        //   _ \ | / _   
        //   0 1 2 3 4
        //   ^ this
        float LIDARAngleToCheckInDegrees = Config.FieldOfVisionStartInDegrees;

        //   _ \ | / _   
        //   0 1 2 3 4
        //   [-] this
        float LIDARVisionAngleInDegrees = Config.VisionAngleInDegrees;

        int radiusOfBeeInPX = (int)AIControlledBee.s_sizeOfBee / 4; // we process in radius, not diameter, so calc once here

        int searchDistanceInPixels = Config.DepthOfVisionInPixels + radiusOfBeeInPX;

        // checking every bee against every bee is "search dist* 24*24" hit-tests (sqrt,in ellipse etc). SLOW
        // we can avoid by making a list of just those within range at the start and only test those.
        List<AIControlledBee> beesInRange = IVision.GetBeesThatAreCloseEnoughToCollideWith(id, location, searchDistanceInPixels);

        bool flowerFound = false;

        for (int LIDARangleIndex = 0; LIDARangleIndex < Config.SamplePoints; LIDARangleIndex++)
        {
            //     -45  0  45
            //  -90 _ \ | / _ 90   <-- relative to direction of bee, hence + angle bee is pointing
            double LIDARangleToCheckInRadians = MathUtils.DegreesInRadians(AngleLookingInDegrees + LIDARAngleToCheckInDegrees);

            // calculate ONCE per angle, not per radius.
            double cos = Math.Cos(LIDARangleToCheckInRadians);
            double sin = Math.Sin(LIDARangleToCheckInRadians);

            float howCloseToGrassBeeIsForThisAngle = 0;

            // We don't want the bee crashing, we need to check radiating outwards from the bee and find the *closest* square
            // of wall/tree/bee in that direction. i.e. we don't care if there is no wall 30 pixels away if there is a wall right next to the bee.

            bool flower = false;

            // Based on config, we can look ahead. But be mindful, every pixel we check takes time.
            for (int currentLIDARscanningDistanceRadius = 0;
                     currentLIDARscanningDistanceRadius < searchDistanceInPixels;
                     currentLIDARscanningDistanceRadius += 2) // no need to check at 1 pixel resolution
            {
                // simple maths, think circle. We are picking a point at an arbitrary angle at an arbitrary distance from a center point.
                // r = LIDARscanningDistance, angle = LIDARangleToCheckInDegrees

                // we need to convert that into a relative horizontal / vertical position, then add that to the bees location
                // X = r cos angle | y = r sin angle
                double positionOnTrackBeingScannedX = Math.Round(cos * currentLIDARscanningDistanceRadius + location.X);
                double positionOnTrackBeingScannedY = Math.Round(sin * currentLIDARscanningDistanceRadius + location.Y);

                int objectAtLocation = PlayGroundCreator.GetPixelType((int)positionOnTrackBeingScannedX, (int)positionOnTrackBeingScannedY);

                // are we colliding with a wall or stump?
                if (objectAtLocation == PlayGroundCreator.c_wall || objectAtLocation == TreeManager.c_treeStump)
                {
                    howCloseToGrassBeeIsForThisAngle = currentLIDARscanningDistanceRadius;
                    break; // we've found the closest pixel in this direction
                }

                // determine if sensor sees another bee.
                foreach (AIControlledBee bee in beesInRange)
                {
                    PointF hitpoint = new((float)positionOnTrackBeingScannedX, (float)positionOnTrackBeingScannedY);

                    PointF pBee = bee.Location;

                    // if either x or y is too large, it is out of range: Avoid SQRT
                    if (Math.Abs(pBee.X - hitpoint.X) > currentLIDARscanningDistanceRadius) continue;
                    if (Math.Abs(pBee.Y - hitpoint.Y) > currentLIDARscanningDistanceRadius) continue;

                    // too far as the crow flies
                    if (MathUtils.DistanceBetweenTwoPoints(pBee, hitpoint) > currentLIDARscanningDistanceRadius) continue;

                    if (bee.PointIsWithinEllipse(hitpoint))
                    {
                        howCloseToGrassBeeIsForThisAngle = currentLIDARscanningDistanceRadius;
                        break; // we've found bee in this direction
                    }
                }

                if (howCloseToGrassBeeIsForThisAngle > 0) break;

                // do we see a treat on that pixel?
                if (objectAtLocation == FlowerManager.c_flower)
                {
                    howCloseToGrassBeeIsForThisAngle = currentLIDARscanningDistanceRadius;
                    flowerFound = true;
                    flower = true;
                    break; // we've found the closest pixel in this direction
                }
            }

            // at this point we have proximity of grass in a single direction

            // >0 means there is grass within the LIDAR radius
            if (howCloseToGrassBeeIsForThisAngle > 0)
            {
                // the range is 20..30, so we subtract 20 to bring it in the 0-10 range.
                howCloseToGrassBeeIsForThisAngle /= Config.DepthOfVisionInPixels;
                howCloseToGrassBeeIsForThisAngle = howCloseToGrassBeeIsForThisAngle.Clamp(0, 1);

                // the neural network cares about 0..1 for inputs so we scale but
                // but we also need to invert so that "1" needs to mean REALLY close (neuron fires), "0" means no grass

                if (flower)
                {
                    LIDAROutputRaw[LIDARangleIndex] = howCloseToGrassBeeIsForThisAngle;
                    LIDAROutput[LIDARangleIndex] = howCloseToGrassBeeIsForThisAngle; // Math.Exp(howCloseToGrassBeeIsForThisAngle);
                }
                else
                {
                    LIDAROutputRaw[LIDARangleIndex] = -(1 - howCloseToGrassBeeIsForThisAngle) / 3;
                    LIDAROutput[LIDARangleIndex] = -(1 - howCloseToGrassBeeIsForThisAngle) / 3; // - Math.Exp(howCloseToGrassCarIsForThisAngle);
                }
            }
            else
            {
                LIDAROutputRaw[LIDARangleIndex] = 0;
                LIDAROutput[LIDARangleIndex] = 0; // no grass within this direction
            }

            //   _ \ | / _         _ \ | / _   
            //   0 1 2 3 4         0 1 2 3 4
            //  [-] from this       [-] to this
            LIDARAngleToCheckInDegrees += LIDARVisionAngleInDegrees;
        }

        if (flowerFound)
        {
            // find the closest treat
            double min = 1;

            for (int i = 0; i < LIDAROutput.Length; i++)
            {
                if (LIDAROutput[i] > 0 && LIDAROutput[i] < min) min = LIDAROutput[i];
            }

            // zero out the non closest treats
            for (int i = 0; i < LIDAROutput.Length; i++)
            {
                if (LIDAROutput[i] > 0)
                {
                    if (LIDAROutput[i] != min)
                    {
                        LIDAROutput[i] = 0;
                    }
                    else
                    {
                        min = 99; // won't find
                    }
                }
            }
        }

        // store the location for drawing later
        locationOfOutput.X = location.X;
        locationOfOutput.Y = location.Y;

        SetLIDAR(id, LIDAROutput);

        // an array of float values 0..1 indicating "1" grass is really close in that direction to "0" no grass.
        return LIDAROutput;
    }

    /// <summary>
    /// Draws the LIDAR onto the image provided. Unlike the live LIDAR, we are drawing with the bee facing upwards.
    /// </summary>
    /// <param name="graphics"></param>
    /// <param name="visualImage"></param>
    public void DrawSensorToImage(int id, Graphics graphics, PointF visualCenterPoint)
    {
        if (lastOutputFromSensor is null) throw new Exception("this should come from the LIDAR");
        if (!lastOutputFromSensor.ContainsKey(id)) return;

        //      .  
        //       \ | .    
        //      __(o).     "(o)" is the bee. lines vary in length depending on proximity to wall 

        float LIDARAngleToCheckInDegrees = (float)BeeController.s_bees[id].AngleBeeIsPointingInDegrees + Config.FieldOfVisionStartInDegrees + 90;
        float LIDARVisionAngleInDegrees = Config.VisionAngleInDegrees;

        s_penToDrawLidar.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;

        for (int LIDARangleIndex = 0; LIDARangleIndex < Config.SamplePoints; LIDARangleIndex++)
        {
            double nnOutput = lastOutputFromSensor[id][LIDARangleIndex];

            //     -45  0  45
            //  -90 _ \ | / _ 90   <-- relative to direction of bee, hence + angle bee is pointing
            double LIDARangleToCheckInRadians = MathUtils.DegreesInRadians(LIDARAngleToCheckInDegrees) - /* 90 degrees */ Math.PI / 2;

            if (nnOutput != 0)
            {
                double inputInverted = nnOutput > 0 ? nnOutput : -3 * nnOutput;

                if (nnOutput < 0)
                    inputInverted = 1 - inputInverted;

                // size of line to draw
                double sizeOfLine = inputInverted * Config.DepthOfVisionInPixels;

                // we need to convert that into a relative horizontal / vertical position, then add that to the bees location
                // X = r cos angle | y = r sin angle
                float x = (float)Math.Round(Math.Cos(LIDARangleToCheckInRadians) * Math.Abs(sizeOfLine)) + visualCenterPoint.X;
                float y = (float)Math.Round(Math.Sin(LIDARangleToCheckInRadians) * Math.Abs(sizeOfLine)) + visualCenterPoint.Y;

                Pen pen = nnOutput switch
                {
                    > 0 => s_penToDrawFlower,
                    _ => s_penToDrawLidar,
                };

                graphics.DrawLine(pen, visualCenterPoint, new PointF(x, y));
            }

            LIDARAngleToCheckInDegrees += LIDARVisionAngleInDegrees;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="id"></param>
    /// <param name="sensor"></param>
    public void SetLIDAR(int id, double[] sensor)
    {
        lock (parallelForEachLock)
        {
            if (!lastOutputFromSensor.ContainsKey(id)) lastOutputFromSensor.Add(id, sensor); else lastOutputFromSensor[id] = sensor;
        }
    }

    /// <summary>
    /// Mono is one sensor per sample point.
    /// </summary>
    /// <returns></returns>
    public int NeuronsRequiredForVision()
    {
        return Config.SamplePoints;
    }

}