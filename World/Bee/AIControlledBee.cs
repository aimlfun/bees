
using Bees.AI;
using Bees.Learn;
using Bees.Settings;
using Bees.Utilities;
using Bees.World.PlayGround;
using System.Reflection;
using System.Security.Cryptography;

namespace Bees.World.Bee;

/// <summary>
/// Magic AI bee.
/// </summary>
internal class AIControlledBee
{
    /// <summary>
    /// Logic for collisions returns on of these
    /// </summary>
    enum CollisionTypes { None, WallOrTree, Bee }

    /// <summary>
    /// Reasons the bee may have been eliminated (or not).
    /// </summary>
    internal enum EliminationReasons { notEliminated, collided, moves200 };

    /// <summary>
    /// Tasks the bee performs left-to-right.
    /// </summary>
    internal enum BeeTask { CollectNectar, ReturnToHive, ReturnToBed, OrientAngle, Sleep };

    /// <summary>
    /// This is used to orient the bee at the start, and after returning to hive.
    /// </summary>
    private const int c_angleOfBeeFacingUpwards = 270;

    #region STATIC VARIABLES

    /// <summary>
    /// Pen used for bees that collided and were eliminated.
    /// </summary>
    internal static readonly Pen s_redPenForCollidedEliminatedBees = new(Color.FromArgb(180, 255, 0, 0));

    /// <summary>
    /// Pen used for bees the failed to relocate after 200 moves and got eliminated for it.
    /// </summary>
    internal static readonly Pen s_greyPenFor200movesEliminatedBees = new(Color.FromArgb(220, 50, 50, 50));

    /// <summary>
    /// Bee frame 1 with wings in 1st position.
    /// </summary>
    private static Bitmap s_beeBitmapFrame1 = new("UX/Resources/bee-frame-1.png");

    /// <summary>
    /// Bee frame 2 with wings in 2nd position.
    /// </summary>
    private static Bitmap s_beeBitmapFrame2 = new("UX/Resources/bee-frame-2.png");

    /// <summary>
    /// We put the 2 bee images into this array, and each time we need one rotated, we rotate a copy of these 2 image.
    /// </summary>
    private static Bitmap[] s_leftPointingBeeBitmapFramesUsedForRotation = new Bitmap[] { s_beeBitmapFrame1, s_beeBitmapFrame2 };

    /// <summary>
    /// To avoid rotating and resizing bee images, we cache them and re-use.
    /// This is accurate to nearest 0.5 degres.
    /// </summary>
    private static readonly Dictionary<float, Bitmap[]> s_cachedRotatedBeeFrames = new();

    /// <summary>
    /// Pen used to draw the nectar gauge.
    /// </summary>
    private static readonly Pen s_blackPenForGauge = new(Color.Black, 2);

    /// <summary>
    /// Pen used to draw the nectar gauge, amount of nectar collected.
    /// </summary>
    private static readonly Pen s_yellowPenForGauge = new(Color.Yellow, 2);

    /// <summary>
    /// Size of the bee in pixels.
    /// </summary>
    internal static float s_sizeOfBee;
    #endregion

    /// <summary>
    /// What the bee is currently meant to do.
    /// e.g. collect nectar, return to hive.
    /// </summary>
    internal BeeTask currentTaskOfBee = BeeTask.CollectNectar;

    /// <summary>
    /// The unique ID of this bee in the Bees[] dictionary, used to map to AI brain (has same ID).
    /// </summary>
    internal int id = 0;

    /// <summary>
    /// Tracks how many flowers have been visited by the bee (each = 1 unit of nectar).
    /// </summary>
    internal int nectarCollected = 0;

    /// <summary>
    /// Indicates bee has left the hive.
    /// </summary>
    internal bool hasLeftHive = false;

    /// <summary>
    /// Where the bee is on the track.
    /// </summary>
    internal PointF Location = new();

    /// <summary>
    /// Last location bee was at.
    /// </summary>
    internal PointF LastLocation = new();

    /// <summary>
    /// Location the bee started from.
    /// </summary>
    internal PointF StartLocation = new();

    /// <summary>
    /// How far the bee has travelled.
    /// </summary>
    internal float DistanceTravelled;

    /// <summary>
    /// Angle bee is pointing, that dictates the direction the bee will move.
    /// </summary>
    internal double AngleBeeIsPointingInDegrees = 0F;

    /// <summary>
    /// The last angle the bee was pointing.
    /// </summary>
    internal double LastAngleBeeIsPointingInDegrees = 0;

    /// <summary>
    /// Speed the bee is travelling at.
    /// </summary>
    internal double Speed = 1F;

    /// <summary>
    /// Angle the bee was asked to turn to (when returning to hive/bed).
    /// </summary>
    private float AngleRequested = -1;

    #region BEE-LIMINATION
    /// <summary>
    /// See HasBeenEliminated.
    /// </summary>
    private bool hasBeenEliminated = false;

    /// <summary>
    /// When sucking nectar, the bee pauses. To pause, it sets a value in this property. Each frame decrements, and when it reaches
    /// zero the bee can move again.
    /// </summary>
    internal int NumberOfTimesNotToMoveWhileSuckingNectar
    {
        get; set;
    }

    /// <summary>
    /// Why the bee was eliminated.
    /// </summary>
    private EliminationReasons reasonBeeWasEliminated = EliminationReasons.notEliminated;

    /// <summary>
    /// Returns the reason why the bee as eliminated.
    /// </summary>
    internal EliminationReasons EliminatedReason
    {
        get { return reasonBeeWasEliminated; }
    }

    /// <summary>
    /// When true, this bee will no longer move. 
    /// Reasons are 
    /// (1) the bee has crashed into the something
    /// (2) the bee decided to miss-beehave.
    /// </summary>
    internal bool HasBeenEliminated
    {
        get { return hasBeenEliminated; }
    }

    /// <summary>
    /// > 0 we see a "ghost" of the bee briefly where it got removed.
    /// </summary>
    internal int EliminatedFadeCount = 0;

    /// <summary>
    /// Reset to where the bee was located before the bee is moved.
    /// </summary>
    private readonly List<PointF> LastlocationsVisitedByBee = new();

    /// <summary>
    /// Plots the locations the bee visits.
    /// </summary>
    internal List<PointF> trail = new();

    /// <summary>
    /// Marks a bee as eliminated (doesn't move, may well will get replaced by a better bee).
    /// </summary>
    /// <param name="reason"></param>
    internal void Eliminate(EliminationReasons reason)
    {
        if (hasBeenEliminated) return; // no action necessary.

        hasBeenEliminated = true;

        EliminatedFadeCount = Config.HowLongToShowGhostOfEliminatedBees; // timer, so we see a "ghost" of the bee briefly where it got removed

        reasonBeeWasEliminated = reason; // track the reason, so we can trouble-shoot 
    }
    #endregion

    #region STATIC INITIALISATION
    /// <summary>
    /// Static Constructor.
    /// </summary>
    static AIControlledBee()
    {
        float c_scale = 50f / 100f;

        s_sizeOfBee = 45f * c_scale;

        LoadAndSizeBee();
    }

    /// <summary>
    /// Loads our bee images, resize and makes them transparent.
    /// </summary>
    internal static void LoadAndSizeBee()
    {
        s_beeBitmapFrame1?.Dispose();
        s_beeBitmapFrame2?.Dispose();

        s_beeBitmapFrame1 = GetBeeImageShrunk("bee-frame-1.png");
        s_beeBitmapFrame2 = GetBeeImageShrunk("bee-frame-2.png");

        s_leftPointingBeeBitmapFramesUsedForRotation = new Bitmap[] { s_beeBitmapFrame1, s_beeBitmapFrame2 };
    }

    /// <summary>
    /// Makes the image transparent, and resizes.
    /// </summary>
    /// <param name="imageName"></param>
    /// <returns></returns>
    private static Bitmap GetBeeImageShrunk(string imageName)
    {
        using Bitmap b = new($"UX/Resources/{imageName}");

        b.MakeTransparent(b.GetPixel(0, 0));

        return (Bitmap)ImageUtils.ResizeImage(b, (int)s_sizeOfBee, (int)s_sizeOfBee);
    }
    #endregion

    /// <summary>
    /// Constructor.
    /// </summary>
    internal AIControlledBee(int newId)
    {
        // this is the data used to link up the neural network / monitor / manipulate
        id = newId;

        // reset the bee to the start point for this track and point it in the correct direction (intended flow of track).
        PointF somewhere = HiveManager.HiveIndexToPosition(id);

        Location = new PointF(somewhere.X, somewhere.Y);
        StartLocation = new PointF(somewhere.X, somewhere.Y);
        LastLocation = new PointF(somewhere.X, somewhere.Y);

        AngleBeeIsPointingInDegrees = c_angleOfBeeFacingUpwards; // pointing upwards
        LastAngleBeeIsPointingInDegrees = c_angleOfBeeFacingUpwards; // pointing upwards
    }

    #region BEE COLLISION DETECTING
    /// <summary>
    /// Compute the hit points based on the angle of the bee, and its location.
    /// </summary>
    /// <returns></returns>
    internal PointF[] DetermineHitTestPoints()
    {
        PointF[] points = RawHitTestPoints();

        PointF origin = new((float)Math.Round(Location.X),
                            (float)Math.Round(Location.Y));

        List<PointF> rotatedPoints = new();

        foreach (PointF p in points)
        {
            rotatedPoints.Add(MathUtils.RotatePointAboutOrigin(new PointF(p.X + origin.X, p.Y + origin.Y),
                                                               origin,
                                                               AngleBeeIsPointingInDegrees));
        }

        return rotatedPoints.ToArray();
    }

    /// <summary>
    /// Compute the hit points based on the angle of the bee.
    /// </summary>
    /// <returns></returns>
    internal PointF[] RawHitTestPoints()
    {
        float width = s_sizeOfBee - 5;
        float height = s_sizeOfBee;

        /*              X   X   X
         *             p3  p13   p1
         *   p5 +---------------+ X 
         *      |               |
         *   p7 x        +      | X p12
         *      |               |
         *   p6 +---------------+ X
         *             p4  p24  p2
         *              X   X   X
         */

        PointF p1 = new(height / 2 - height / 24 - 2, +width / 2 - 5 - 2);
        PointF p3 = new(0, +width / 2 - 5 - 1);
        PointF p13 = new((p1.X + p3.X) / 2, +width / 2 - 5 - 1);

        PointF p2 = new(height / 2 - height / 24 - 2, -width / 2 + 5 - 2 + 2);
        PointF p4 = new(0, -width / 2 + 5 - 2 + 2);
        PointF p24 = new((p2.X + p4.X) / 2, -width / 2 + 5 - 2 + 1);

        PointF p12 = new(p1.X + 1, (p1.Y + p2.Y) / 2);

        PointF p5 = new(2 - height / 2 + height / 24 + 2, p3.Y);
        PointF p6 = new(2 - height / 2 + height / 24 + 2, p4.Y);
        PointF p7 = new(-height / 2 + 1, (p4.Y + p3.Y) / 2);

        return new PointF[] { p1, p12, p2, p24, p4, p6, p7, p5, p3, p13 };
    }

    /// <summary>
    /// ReturnsL-
    /// CollisionTypes.WallOrTree - if bee collided with object such as wall or tree; 
    /// CollisionTypes.Bee  - if collided with bee. 
    /// CollisionTypes.None - if no collision.
    /// </summary>
    /// <returns>Indicator of whether bee has collided or not.</returns>
    private CollisionTypes Collided()
    {
        PointF[] points = DetermineHitTestPoints();

        int i = 0;
        foreach (PointF hitTestPoint in points)
        {
            // hit wall or tree?
            if (!PlayGroundCreator.PixelIsSafe((int)(0.5F + hitTestPoint.X), (int)(0.5F + hitTestPoint.Y))) return CollisionTypes.WallOrTree;

            // collided with another bee?
            foreach (int beeToCheck in BeeController.s_bees.Keys)
            {
                if (beeToCheck == id || BeeController.s_bees[beeToCheck].HasBeenEliminated) continue;

                // check every other hit-point to speed up collision detection
                // is the hit-point within a bee's distance?
                if (MathUtils.DistanceBetweenTwoPoints(BeeController.s_bees[beeToCheck].Location, hitTestPoint) > s_sizeOfBee) continue;

                // collided with another bee? i.e is hitpoint within ellipse of other bee.
                if (BeeController.s_bees[beeToCheck].PointIsWithinEllipse(hitTestPoint)) return CollisionTypes.Bee;
            }
            
            i++;

            if (i < 3)
            {
                break;
            }
        }


        return CollisionTypes.None;
    }

    /// <summary>
    /// We treat bees as ellipses for collision detection.
    /// </summary>
    /// <param name="hitTestPoint"></param>
    /// <returns></returns>
    internal bool PointIsWithinEllipse(PointF hitTestPoint)
    {
        double a = s_sizeOfBee / 2;
        double b = s_sizeOfBee;
        double alpha = MathUtils.DegreesInRadians(AngleBeeIsPointingInDegrees);

        double cosAlpha = Math.Cos(alpha);
        double sinAlpha = Math.Sin(alpha);

        double r = Math.Pow(cosAlpha * (hitTestPoint.X - Location.X) +
                            sinAlpha * (hitTestPoint.Y - Location.Y), 2) / a +

                   Math.Pow(sinAlpha * (hitTestPoint.X - Location.X) -
                            cosAlpha * (hitTestPoint.Y - Location.Y), 2) / b;
        return r <= 1;
    }
    #endregion

    #region BEE RENDERING 

    /// <summary>
    /// Contains a list of named colours, used for "trails".
    /// </summary>
    private static string[] s_colours = new string[] { };

    /// <summary>
    /// Returns the colour from the cache by index, caching if required.
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    private static Color GetColorByIndex(int index)
    {
        // cache empty?
        if (s_colours.Length == 0)
        {
            Type colorType = typeof(System.Drawing.Color);

            PropertyInfo[] propInfos = colorType.GetProperties(BindingFlags.Static | BindingFlags.DeclaredOnly | BindingFlags.Public);

            List<string> colours = new();

            // named colours...
            foreach (PropertyInfo propInfo in propInfos)
            {
                colours.Add(propInfo.Name);
            }

            s_colours = colours.ToArray();
        }

        return Color.FromName(s_colours[index]);
    }

    /// <summary>
    /// Renders the bee.
    /// </summary>
    /// <param name="g"></param>
    internal void Render(Graphics g)
    {
        if (HasBeenEliminated)
        {
            DrawGhostOutline(g); // outline
            return;
        }

        // does the actual drawing of the bee.
        // Because it involves rotation, we cache the rotated bee images. To keep the cache limited, we do it to the nearest 1/2 a degree.
        float angle = (float)Math.Round(AngleBeeIsPointingInDegrees * 2) / 2;

        // rotate once only store image for that angle.
        if (!s_cachedRotatedBeeFrames.ContainsKey(angle))
        {
            s_cachedRotatedBeeFrames.Add(angle,
                                        new Bitmap[] {
                                            ImageUtils.RotateBitmapWithColoredBackground(s_leftPointingBeeBitmapFramesUsedForRotation[0], AngleBeeIsPointingInDegrees), // wing pos 1
                                            ImageUtils.RotateBitmapWithColoredBackground(s_leftPointingBeeBitmapFramesUsedForRotation[1], AngleBeeIsPointingInDegrees)  // wing pos 2
                                        });
        }

        // draws lines showing path the bees took
        PaintTrailsIfEnabled(g);

        // draw the rotated bee. The image is static (no wings beating) if sleeping.
        Bitmap rotatedImageOfBee = s_cachedRotatedBeeFrames[angle][currentTaskOfBee == BeeTask.Sleep || PlayGroundCreator.BeesAreSleeping ? 0 : BeeController.s_wingFrame0or1];

        g.DrawImageUnscaled(rotatedImageOfBee,
                    new Point((int)Location.X - rotatedImageOfBee.Width / 2,
                              (int)Location.Y - rotatedImageOfBee.Height / 2));

        // if outside of the hive, we display a nectar gauge to left of bee
        if (Location.X > 116 || Location.Y < 190)
        {
            DrawNectarGauge(g);
        }

        // provide dots where the bee is sensing hits, if turned on
        if (Config.ShowHitPointsOnBee) ShowHitPoints(g);

        // re-request for the sensor as the bee has moved, and we want to render
        if (!HasBeenEliminated && BeeController.ShowRadar && currentTaskOfBee != AIControlledBee.BeeTask.Sleep)
        {
            Config.VisionSystem.DrawSensorToImage(id, g, Location);

            // shows where the bee needs to head
            if (Config.c_paintBeeTarget)
            {
                DrawTargettingLines(g);
                return;
            }
        }

        // when in stero vision, this affirms our vision is originating from the correct place
        if (Config.c_paintBeesEyes)
        {
            ShowBeesEyes(g);
        }

        // each bet gets a blob of colour on their head
        if (Config.c_paintColouredHats)
        {
            PaintColouredHatOnBee(g);
        }
    }

    /// <summary>
    /// Enable to see where each bee is targetting
    /// </summary>
    /// <param name="g"></param>
    private void DrawTargettingLines(Graphics g)
    {
        // targeting for nectar is the flowers, so nothing to draw
        if (currentTaskOfBee == BeeTask.CollectNectar) return;

        // red line indicating the "x" it is heading to
        if (currentTaskOfBee != BeeTask.OrientAngle && currentTaskOfBee != BeeTask.Sleep)
        {
            double rad = MathUtils.DegreesInRadians(AngleRequested);
            double x = Math.Cos(rad) * 50 + Location.X;
            double y = Math.Sin(rad) * 50 + Location.Y;

            g.DrawLine(Pens.Red, new Point((int)x, (int)y), Location);
        }

        // indicate which bed it is heading to
        if (currentTaskOfBee == BeeTask.ReturnToBed)
        {
            g.DrawLine(Pens.Cyan, HiveManager.GetNextFreeBed(), Location);
        }
    }

    /// <summary>
    /// Computes the eyes relative to the rotated bee, then colours them
    /// in a random colour.
    /// </summary>
    /// <param name="g"></param>
    private void ShowBeesEyes(Graphics g)
    {
        PointF pToTheLeft = new(Location.X + 14 / 2, Location.Y - 8 / 2);
        PointF pToTheRight = new(Location.X + 14 / 2, Location.Y + 8 / 2);

        PointF eyeLeft = MathUtils.RotatePointAboutOrigin(pToTheLeft, new PointF(Location.X, Location.Y), AngleBeeIsPointingInDegrees);

        PointF eyeRight = MathUtils.RotatePointAboutOrigin(pToTheRight, new PointF(Location.X, Location.Y), AngleBeeIsPointingInDegrees);

        g.FillRectangle(new SolidBrush(Color.FromArgb(200, RandomNumberGenerator.GetInt32(0, 255), RandomNumberGenerator.GetInt32(0, 255), RandomNumberGenerator.GetInt32(0, 255))), new RectangleF(eyeLeft.X - 1.5f, eyeLeft.Y - 1.5f, 3, 3));
        g.FillRectangle(new SolidBrush(Color.FromArgb(200, RandomNumberGenerator.GetInt32(0, 255), RandomNumberGenerator.GetInt32(0, 255), RandomNumberGenerator.GetInt32(0, 255))), new RectangleF(eyeRight.X - 1.5f, eyeRight.Y - 1.5f, 3, 3));
    }

    /// <summary>
    /// Draws a blob on the bees head.
    /// </summary>
    /// <param name="g"></param>
    private void PaintColouredHatOnBee(Graphics g)
    {
        PointF pToTheLeft = new(Location.X + 14 / 2 - 1, Location.Y - 1);

        PointF eyeLeft = MathUtils.RotatePointAboutOrigin(pToTheLeft, new PointF(Location.X, Location.Y - 1), AngleBeeIsPointingInDegrees);

        Color c = GetColorByIndex(id);

        g.FillRectangle(new SolidBrush(Color.FromArgb(255, c.R, c.G, c.B)),
                        new RectangleF(eyeLeft.X - 2.5f, eyeLeft.Y - 2.5f, 5, 5));
    }

    /// <summary>
    /// Draws the "trails" showing the path of the bee. To reduce lines, we do it every 10px radius from last
    /// </summary>
    /// <param name="g"></param>
    private void PaintTrailsIfEnabled(Graphics g)
    {
        if (trail.Count == 0 || MathUtils.DistanceBetweenTwoPoints(trail[^1], Location) > 10)
        {
            trail.Add(Location);
        }

        // draw if enabled, whereas we track whether enabled or not
        if (trail.Count > 1 && BeeController.ShowTrail)
        {
            // Color.FromArgb(100, id * 10, (id * 17) % 256, (id * 63) % 256)
            using Pen penTrail = new(GetColorByIndex(id));
            g.DrawLines(penTrail, trail.ToArray());
        }
    }

    /// <summary>
    /// Draws the nectar gauge to the left of the bee.
    /// </summary>
    /// <param name="g"></param>
    private void DrawNectarGauge(Graphics g)
    {
        /* 
         *  nectarX
         *  .
         *  +--+ <--- nectarYTop                     --- sizeOfNectarHolder
         *  |##|                                      |
         *  |##|                                      |
         *  |##|  < spare room to capture nectar      |
         *  |##|     (black)                          |
         *  |##|                                      |
         *  |--| <--- nectarYFill                     |
         *  |xx|                                      |
         *  |xx|  < nectar captured                   |
         *  |xx|        (yellow)                      |
         *  +--+ <--- nectarYBottom                  ---
         * 
         */
        float nectarX = Location.X - s_sizeOfBee / 2 - 3;
        float nectarYTop = Location.Y - s_sizeOfBee / 2 + 4;
        float nectarYBottom = Location.Y + s_sizeOfBee / 2 - 4;
        float sizeOfNectarHolder = nectarYBottom - nectarYTop;
        float nectarYFill = nectarYBottom - Math.Min(sizeOfNectarHolder / 8 * nectarCollected, sizeOfNectarHolder);

        // not filled with nectar (black)
        g.DrawLine(s_blackPenForGauge, nectarX, nectarYTop, nectarX, nectarYBottom);

        // nectar (yeellow)
        g.DrawLine(s_yellowPenForGauge, nectarX, nectarYFill, nectarX, nectarYBottom);
    }

    /// <summary>
    /// Draws dots where the hit points are. Great for debugging!
    /// </summary>
    /// <param name="g"></param>
    internal void ShowHitPoints(Graphics g)
    {
        PointF[] points = DetermineHitTestPoints();

        foreach (PointF hitTestPoint in points)
        {
            g?.DrawRectangle(Pens.Cyan, (int)(0.5F + hitTestPoint.X), (int)(0.5F + hitTestPoint.Y), 1, 1);
        }
    }

    /// <summary>
    /// Draws a bee shape indicating where it collides with wall/tree, and after a period it disappears.
    /// </summary>
    /// <param name="g"></param>
    /// <exception cref="Exception"></exception>
    internal virtual void DrawGhostOutline(Graphics g)
    {
        if (EliminatedFadeCount <= 0) return;

        --EliminatedFadeCount; // decrease, so when it hits zero we no longer draw it.

        Pen ghostPen = EliminatedReason switch
        {
            AIControlledBee.EliminationReasons.notEliminated => throw new Exception("bee ghost only applies to eliminated bees"),
            AIControlledBee.EliminationReasons.collided => s_redPenForCollidedEliminatedBees,
            AIControlledBee.EliminationReasons.moves200 => s_greyPenFor200movesEliminatedBees,
            _ => throw new Exception("missing reason"),
        };

        PointF[] pointsArray = DetermineHitTestPoints();

        // add all the points plus repeat start to enclose shape
        List<PointF> points = new(pointsArray)
        {
            pointsArray[0]
        };

        g.DrawLines(ghostPen, points.ToArray());
    }
#endregion

#region BEE PHYSICS
    /// <summary>
    /// Moves the bee using speed at given angle, with the ability to drift sideways where
    /// there is a large enough difference in flap speed.
    /// </summary>
    /// <param name="beeInputState"></param>
    internal void ApplyPhysics(double leftWingFlapRate, double rightWingFlapRate)
    {
        // based on which wing is beat harder, we steer in the direction of the slower
        AngleBeeIsPointingInDegrees += 30 * (rightWingFlapRate - leftWingFlapRate) / 4;

        // if both are "1" we move top speed, anything else is less than.
        Speed = (leftWingFlapRate + rightWingFlapRate) / 2;

        if (Math.Abs(Speed) <0.3f) Speed = 0.7;

        // it'll work even if we violate this, but let's keep it clean 0..359.999 degrees.
        if (AngleBeeIsPointingInDegrees < 0) AngleBeeIsPointingInDegrees += 360;
        if (AngleBeeIsPointingInDegrees >= 360) AngleBeeIsPointingInDegrees -= 360;

        // move the bee using basic sin/cos math ->  x = r cos(theta), y = r x sin(theta)
        // in this instance "r" is the speed output, theta is the angle of the bee.

        double angleBeeIsPointingInRadians = MathUtils.DegreesInRadians(AngleBeeIsPointingInDegrees);
        Location.X += (float)(Math.Cos(angleBeeIsPointingInRadians) * Speed);
        Location.Y += (float)(Math.Sin(angleBeeIsPointingInRadians) * Speed);

        // bees can fly sideways, if enabled (rather than rotate)
        if (Config.BeeCanDriftSideways)
        {
            int driftSpeed = Config.c_useStereoVision ? 2 : 5;

            if (leftWingFlapRate > 0 && rightWingFlapRate <= 0)
            {
                angleBeeIsPointingInRadians -= MathUtils.DegreesInRadians(90);
                Location.X += (float)(Math.Cos(angleBeeIsPointingInRadians) * Speed * driftSpeed);
                Location.Y += (float)(Math.Sin(angleBeeIsPointingInRadians) * Speed * driftSpeed);
            }
            else
            if (leftWingFlapRate <= 0 && rightWingFlapRate > 0)
            {
                angleBeeIsPointingInRadians += MathUtils.DegreesInRadians(90);
                Location.X += (float)(Math.Cos(angleBeeIsPointingInRadians) * Speed * driftSpeed);
                Location.Y += (float)(Math.Sin(angleBeeIsPointingInRadians) * Speed * driftSpeed);
            }
        }

        if (!hasLeftHive && Location.X > 110) hasLeftHive = true;
    }
#endregion

#region BEE MOVING
    /// <summary>
    /// Called at a constant interval to simulate bee moving.
    /// </summary>
    internal void Move()
    {
        // if the bee has collided with the grass it is stuck, we don't move it or call its NN.
        if (HasBeenEliminated) return;

        ApplyAIoutputAndMoveTheBee();

        // having moved, did the AI crash into a tree or wall -> dead
        // another other bee? Sorry honey, this bee moving back to where it was.
        CollisionTypes collision = Collided();

        // if hit another bee -> mutate, and reposition
        if (collision == CollisionTypes.Bee)
        {
            AngleBeeIsPointingInDegrees = LastAngleBeeIsPointingInDegrees;

            // retain last location
            Location = new PointF(LastLocation.X, LastLocation.Y);
        }

        // if collided with wall
        if (collision == CollisionTypes.WallOrTree)
        {
            Eliminate(EliminationReasons.collided);
        }

        // punish those who don't move.
        if (HasNotMovedForAWhile())
        {
            Eliminate(EliminationReasons.moves200);
        }

        // got all the nectar? arrived at the hive?
        if (!HasBeenEliminated) CheckToSeeIfBeeNeedsToSwitchTask();
    }

    /// <summary>
    /// By default the task for the bee is to collect nectar.
    /// When it's full and cannot harvest more, it should
    /// return to the hive. And when it gets late, that applies also.
    /// After arriving at the hive it needs to go to bed.
    /// </summary>
    private void CheckToSeeIfBeeNeedsToSwitchTask()
    {
        // determine whether the Bee needs to be heading towards the hive / bed
        // 8 = full nectar pouch
        if (nectarCollected == 8 && currentTaskOfBee == BeeTask.CollectNectar)
        {
            if (Config.c_acceleratedTraining) return; // don't do during that, as the "usual" wall is blocked

            currentTaskOfBee = BeeTask.ReturnToHive;
            return;
        }

        // diagonal line is (0, 240)-(105, 190).
        // y=ax+c. c = 240. Therefore a=(190-240)/105=-0.4762
        // y=ax+c, x=0 y=c=240.
        // For x=105, y= =-0.4762 x 105 + 240
        if ((!Config.c_acceleratedTraining && currentTaskOfBee == BeeTask.ReturnToHive && Location.X < 90 && Location.Y > -0.4762 * Location.X + 240)||
            (Config.c_acceleratedTraining && currentTaskOfBee == BeeTask.ReturnToHive && Location.Y > 300))
        {
            currentTaskOfBee = BeeTask.ReturnToBed;
            return;
        }

        // it bee arriving at its destination (bed)
        if (currentTaskOfBee == BeeTask.ReturnToBed && MathUtils.DistanceBetweenTwoPoints(HiveManager.GetNextFreeBed(), Location) < 10)
        {
            currentTaskOfBee = AngleBeeIsPointingInDegrees != c_angleOfBeeFacingUpwards ? BeeTask.OrientAngle : BeeTask.Sleep;
            Location = HiveManager.GetNextFreeBed();
            HiveManager.ClaimNextAvailableBed(); // even whilst orienting
        }
    }

    /// <summary>
    /// Read the sensors, provide to the "brains" (neural network) and take action based on
    /// the output.
    /// However, behaviour depends on the current "task"
    /// </summary>
    private void ApplyAIoutputAndMoveTheBee()
    {
        LastAngleBeeIsPointingInDegrees = AngleBeeIsPointingInDegrees;

        // retain last location
        LastLocation = new PointF(Location.X, Location.Y);

        PointF DesiredPosition;

        switch (currentTaskOfBee)
        {
            // the main task, collecting nectar.
            case BeeTask.CollectNectar:
                SearchForNectarAvoidingWallsAndTrees();
                return;

            // bee is somewhere in the playground, it needs to head towards hive entrance
            case BeeTask.ReturnToHive:
                if (!Config.c_acceleratedTraining)
                {
                    // if it is above the diagonal line, aim the bee towards centre of screen
                    if (Location.X < 110 && Location.Y < -0.4762 * Location.X + 240)
                        DesiredPosition = new PointF(300, 260);
                    else
                        DesiredPosition = new PointF(85, 260);
                }
                else
                {
                    DesiredPosition = new PointF(55, 260);
                }

                break;

            // bee is in hive, now needs to target the bed.
            case BeeTask.ReturnToBed:
                DesiredPosition = HiveManager.GetNextFreeBed();
                break;

            // bee is in bed, facing wrong direction - needs to face upwards
            case BeeTask.OrientAngle:
                RotateBeeToFaceUpwards();
                return;

            default:
                throw new Exception("illegal unhandled task"); // what did you want the bee to do?
        }

        float angleInDegrees = (float)MathUtils.RadiansInDegrees((float)Math.Atan2(DesiredPosition.Y - Location.Y, DesiredPosition.X - Location.X));

        AngleRequested = angleInDegrees;

        float deltaAngle = Math.Abs(angleInDegrees - (float)AngleBeeIsPointingInDegrees).Clamp(0, 30); // max bee can proceed

        // quickest way to get from current angle to new angle turning the optimal direction
        float angleInOptimalDirection = ((angleInDegrees - (float)AngleBeeIsPointingInDegrees + 540f) % 360) - 180f;

        // limit max of 30 degrees
        AngleBeeIsPointingInDegrees = MathUtils.Clamp360((float)AngleBeeIsPointingInDegrees + deltaAngle * Math.Sign(angleInOptimalDirection));

        double[] neuralNetworkInput = Config.VisionSystem.VisionSensorOutput(id, AngleBeeIsPointingInDegrees, Location); /// input is the distance sensors of how soon we'll impact

        if (currentTaskOfBee == BeeTask.ReturnToHive)
        {
            // nothing ahead blocking in first few sensors
            if ((neuralNetworkInput[Config.SamplePoints / 2 - 1] == 0 || neuralNetworkInput[Config.SamplePoints / 2 - 1] < -0.3) &&
                (neuralNetworkInput[Config.SamplePoints / 2] == 0 || neuralNetworkInput[Config.SamplePoints / 2] < -0.3) &&
                (neuralNetworkInput[Config.SamplePoints / 2 + 1] == 0 || neuralNetworkInput[Config.SamplePoints / 2 + 1] < -0.3))
            {
                Speed = (MathUtils.DistanceBetweenTwoPoints(Location, DesiredPosition) / 10).Clamp(0, 3);
            }
            else
            {
                SearchForNectarAvoidingWallsAndTrees();
                return;
            }
        }
        else
        {
            Speed = 1;
        }

        DistanceTravelled += MathUtils.DistanceBetweenTwoPoints(Location, LastLocation);

        double angleBeeIsPointingInRadians = MathUtils.DegreesInRadians(AngleBeeIsPointingInDegrees);
        Location.X += (float)(Math.Cos(angleBeeIsPointingInRadians) * Speed);
        Location.Y += (float)(Math.Sin(angleBeeIsPointingInRadians) * Speed);

        UpdateRadar();
    }

    /// <summary>
    /// If not display radar, we don't need to hit the vision system an extra time.
    /// </summary>
    private void UpdateRadar()
    {
        if (!BeeController.ShowRadar) return;

        // ensure the vision radar shows based on where it is now, not where it will be
        if (currentTaskOfBee != BeeTask.Sleep) _ = Config.VisionSystem.VisionSensorOutput(id, AngleBeeIsPointingInDegrees, Location); // input is the distance sensors of how soon we'll impact
    }

    /// <summary>
    /// Orient the bee to face upwards in its bed.
    /// </summary>
    private void RotateBeeToFaceUpwards()
    {
        float angleInDegrees = c_angleOfBeeFacingUpwards; // upwards

        float deltaAngle = Math.Abs(angleInDegrees - (float)AngleBeeIsPointingInDegrees).Clamp(0, 10); // max bee can proceed

        // quickest way to get from current angle to new angle turning the optimal direction
        float angleInOptimalDirection = ((angleInDegrees - (float)AngleBeeIsPointingInDegrees + 540f) % 360) - 180f;

        // limit max of 10 degrees
        AngleBeeIsPointingInDegrees = MathUtils.Clamp360((float)AngleBeeIsPointingInDegrees + deltaAngle * Math.Sign(angleInOptimalDirection));

        // Bee is in bed, facing the right way.

        if ((int)angleInDegrees == AngleBeeIsPointingInDegrees)
        {
            currentTaskOfBee = BeeTask.Sleep;
        }

        UpdateRadar();
    }

    /// <summary>
    /// AI logic to move forward avoiding walls / trees / bees.
    /// </summary>
    private void SearchForNectarAvoidingWallsAndTrees()
    {
        // input is the distance sensors of how soon we'll impact
        double[] neuralNetworkInput = Config.VisionSystem.VisionSensorOutput(id, AngleBeeIsPointingInDegrees, Location);

        // provide the neural with vision and let it decide what to do with the bee
        double[] outputFromNeuralNetwork = NeuralNetwork.s_networks[id].FeedForward(neuralNetworkInput); // process inputs

        ApplyPhysics(leftWingFlapRate: (outputFromNeuralNetwork[0] * Config.OutputModulation[0]).Clamp(-0.1, 1.5),
                     rightWingFlapRate: (outputFromNeuralNetwork[1] * Config.OutputModulation[1]).Clamp(-0.1, 1.5));

        // accumulate how far it has moved
        DistanceTravelled += MathUtils.DistanceBetweenTwoPoints(Location, LastLocation);

        UpdateRadar();
    }

    /// <summary>
    /// If bees refuse to move, we eliminate them. This enables us to track them and
    /// check for bad bee-haviour.
    /// </summary>
    /// <returns></returns>
    private bool HasNotMovedForAWhile()
    {
        if (currentTaskOfBee == BeeTask.Sleep) return false; // Bee should be asleep

        LastlocationsVisitedByBee.Add(Location);

        // the ones at the bottom of the hive receive extra time, as they cannot fly thru the bees at the top of hive
        // this gives time for them to move or die
        if (LastlocationsVisitedByBee.Count < id * 10 + 30) return false;

        var distance = MathUtils.DistanceBetweenTwoPoints(LastlocationsVisitedByBee[0], LastlocationsVisitedByBee[^1]);

        if (distance < 15) return true; // bee has barely moved

        distance = MathUtils.DistanceBetweenTwoPoints(StartLocation, Location);

        // bees are not making any effort... remove them
        if (distance < 40 && !hasLeftHive) return true;
        if (!Config.c_acceleratedTraining && BeeController.s_movesMadeByBees > 500 + id * 10 && !hasLeftHive) return true;

        // ensure we only track the last X moves
        LastlocationsVisitedByBee.RemoveAt(0);

        return false; // bee has moved.
    }

    /// <summary>
    /// Helpful when debugging.
    /// </summary>
    /// <returns></returns>
    public override string? ToString()
    {
        return $"id: {id} task: {currentTaskOfBee} nectar: {nectarCollected} angle: {AngleBeeIsPointingInDegrees} speed: {Speed} location: {Location} elimination: {reasonBeeWasEliminated}";
    }
#endregion
}