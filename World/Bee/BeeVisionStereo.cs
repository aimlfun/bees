using Bees.Learn;
using Bees.Settings;
using Bees.Utilities;
using Bees.World.PlayGround;
using System.Runtime.InteropServices;

namespace Bees.World.Bee;

/// <summary>
/// Simple vision implementation of binocular (stereo) vision, that doesn't really work.
/// </summary>
internal class BeeVisionStereo : IVision
{
    /// <summary>
    /// Parallel.ForEach causes issues as we
    /// </summary>
    private readonly object parallelForEachLock = new();

    /// <summary>
    /// Used to remember where to draw the LIDAR.
    /// </summary>
    private PointF locationOfOutput = new();
    
    /// <summary>
    /// We draw a "blob" of colour per item we sense
    /// </summary>
    private Dictionary<int,List<SensorBlobToColour>> sensorBlobOutput= new();

    /// <summary>
    /// Used to draw the LIDAR, contains the values given to the neural network. Correspond to "distances" from colliding (1=close).
    /// </summary>
    readonly Dictionary<int, double[]> lastOutputFromSensor = new();

    /// <summary>
    /// The AI bee needs to know how far it is from the wall, trees and other bees.
    /// Unlike Mono we have to handle EYE positions, and we sweep in two overlapping arcs.
    /// Whereas the LIDAR is distance and type of object the this stereo vision is pixel colours,
    /// the idea being depth is inferred. Eyes see "color" not "depth". The latter takes two eyes.
    /// (compound eyes for bees).
    /// It works extremely poorly for side vision, like human peripheral vision. That's presumably why
    /// they have compound eyes.
    /// </summary>
    /// <param name="id">ID of the bee</param>
    /// <param name="AngleLookingInDegrees">Direction the bee is looking</param>
    /// <param name="location">Where the bee is located</param>
    /// <returns>Proximities 0..1 indicating colour of pixels it sees</returns>
    /// <exception cref="NotImplementedException"></exception>
    public double[] VisionSensorOutput(int id, double AngleLookingInDegrees, PointF location)
    {
        int rangewithoverlap = (Config.SamplePoints * 2); // / 3

        // e.g 
        // input to the neural network
        //   _ \ | / _   
        //   0 1 2 3 4 
        //        
        double[] LIDAROutput = new double[2 * rangewithoverlap];
        double[] LIDAROutputRaw = new double[2 * rangewithoverlap];

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

        if (!sensorBlobOutput.ContainsKey(id)) sensorBlobOutput.Add(id, new()); else sensorBlobOutput[id].Clear();

        PointF pToTheLeft = new(location.X + 14/2, location.Y - 8/2);
        PointF pToTheRight = new(location.X + 14/2, location.Y + 8/2);

        PointF eyeLeft = MathUtils.RotatePointAboutOrigin(
            pToTheLeft,
            new PointF(location.X, location.Y), AngleLookingInDegrees);

        PointF eyeRight = MathUtils.RotatePointAboutOrigin(
            pToTheRight,
            new PointF(location.X, location.Y), AngleLookingInDegrees);

        for (int direction = -1; direction <= 1; direction += 2)
        {
            PointF loc;

            if (direction == -1) loc = eyeLeft; else loc = eyeRight;

            for (int LIDARangleIndex = 0; LIDARangleIndex < rangewithoverlap; LIDARangleIndex++)
            {
                //     -45  0  45
                //  -90 _ \ | / _ 90   <-- relative to direction of bee, hence + angle bee is pointing
                double LIDARangleToCheckInRadians = MathUtils.DegreesInRadians(AngleLookingInDegrees - 90 + (direction == 1 ? LIDARAngleToCheckInDegrees : Config.FieldOfVisionStopInDegrees - (LIDARAngleToCheckInDegrees - Config.FieldOfVisionStartInDegrees)));

                // calculate ONCE per angle, not per radius.
                double cos = Math.Cos(LIDARangleToCheckInRadians);
                double sin = Math.Sin(LIDARangleToCheckInRadians);

                int pixel = 0;

                // these track the position of the pixel "seen", for providing a black area where it doesn't see.
                double lastx = 0;
                double lasty = 0;

                // peripheral vision is less distance
#if usingPeripheralVision
                int distanceInPixels = LIDARangleIndex > rangewithoverlap *10 /30  ? 10 : searchDistanceInPixels;
#else
                int distanceInPixels = searchDistanceInPixels;
#endif
                // Based on config, we can look ahead. But be mindful, every pixel we check takes time.
                for (int currentLIDARscanningDistanceRadius = 0;
                         currentLIDARscanningDistanceRadius < distanceInPixels;
                         currentLIDARscanningDistanceRadius += 2) // no need to check at 1 pixel resolution
                {

                    // simple maths, think circle. We are picking a point at an arbitrary angle at an arbitrary distance from a center point.
                    // r = LIDARscanningDistance, angle = LIDARangleToCheckInDegrees

                    // we need to convert that into a relative horizontal / vertical position, then add that to the bees location
                    // X = r cos angle | y = r sin angle
                    double positionOnTrackBeingScannedX = Math.Round(cos * currentLIDARscanningDistanceRadius + loc.X);
                    double positionOnTrackBeingScannedY = Math.Round(sin * currentLIDARscanningDistanceRadius + loc.Y);

                    lastx = positionOnTrackBeingScannedX;
                    lasty = positionOnTrackBeingScannedY;

                    int objectAtLocation = PlayGroundCreator.GetPixelType((int)positionOnTrackBeingScannedX, (int)positionOnTrackBeingScannedY);

                    // are we colliding with a wall or stump?
                    if (objectAtLocation == PlayGroundCreator.c_wall || objectAtLocation == TreeManager.c_treeStump)
                    {
                        sensorBlobOutput[id].Add(new SensorBlobToColour(objectAtLocation, new PointF((float)positionOnTrackBeingScannedX, (float)positionOnTrackBeingScannedY)));
                        pixel = objectAtLocation;
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
                            pixel = 5;
                            sensorBlobOutput[id].Add(new SensorBlobToColour(pixel, new PointF((float)positionOnTrackBeingScannedX, (float)positionOnTrackBeingScannedY)));
                            break; // we've found bee in this direction
                        }
                    }

                    if (pixel != 0) break;

                    // do we see a treat on that pixel?
                    if (objectAtLocation == FlowerManager.c_flower)
                    {
                        pixel = FlowerManager.c_flower;
                        sensorBlobOutput[id].Add(new SensorBlobToColour(pixel, new PointF((float)positionOnTrackBeingScannedX, (float)positionOnTrackBeingScannedY)));
                        break; // we've found the closest pixel in this direction
                    }
                }
                
                // >0 means it saw something within the LIDAR radius
                if (pixel > 0)
                {
                    if (pixel != 1) pixel = -pixel;
                    LIDAROutputRaw[LIDARangleIndex] = pixel / 5;
                    LIDAROutput[LIDARangleIndex] = pixel / 5; // Math.Exp(howCloseToGrassBeeIsForThisAngle);
                }
                else
                {
                    sensorBlobOutput[id].Add(new(0, new PointF((float)lastx, (float)lasty)));
;
                    LIDAROutputRaw[LIDARangleIndex] = 0;
                    LIDAROutput[LIDARangleIndex] = 0; // no grass within this direction
                }

                //   _ \ | / _         _ \ | / _   
                //   0 1 2 3 4         0 1 2 3 4
                //  [-] from this       [-] to this
                LIDARAngleToCheckInDegrees += LIDARVisionAngleInDegrees;
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
        if (!sensorBlobOutput.ContainsKey(id)) return;

        foreach (var x in sensorBlobOutput[id])
        {
            Brush? b = null;

            switch (x.Pixel)
            {
                case 0: b = new SolidBrush(Color.FromArgb(100, 0, 0, 0)); break;
                case 1: b = new SolidBrush(Color.FromArgb(100, 255, 255, 255)); ; break;
                case 2: b = new SolidBrush(Color.FromArgb(100, 0, 255, 0)); break; // tree
                case 3: b = new SolidBrush(Color.FromArgb(100, 255, 0, 0)); break; // wall
                case 5: b = new SolidBrush(Color.FromArgb(100, 255, 255, 0)); break;
            }

            if (b is not null)
            {
                graphics.FillEllipse(b, x.Location.X - 2, x.Location.Y - 2, 4, 4);
                b.Dispose();
            }
        }
    }

    /// <summary>
    /// Store the vision output per bee.
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
    /// Stereo eyes have an overlap, so the sample points is 1 1
    /// </summary>
    /// <returns></returns>
    public int NeuronsRequiredForVision()
    {
        return 2 * (Config.SamplePoints * 2); // / 3;
    }

    /// <summary>
    /// Pixels seen by eye are tracked as colour @ location by this class.
    /// </summary>
    internal class SensorBlobToColour
    {
        internal int Pixel;
        internal PointF Location;

        public SensorBlobToColour(int objectAtLocation, PointF pointF)
        {
            Pixel= objectAtLocation;
            Location= pointF;
        }
    }
}