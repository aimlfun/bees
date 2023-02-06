using Bees.World.Bee;

namespace Bees.Settings;

/// <summary>
/// Configuration.
/// </summary>
internal static class Config
{
    /// <summary>
    /// We can make the environment weed out the poor performing bees early on, by
    /// giving the a specific course that requires them to dodge left, right and
    /// collect flowers before they are let loose.
    /// </summary>
    internal const bool c_acceleratedTraining = false; // default: false, it learns quick enough anyway for mono vision

    /// <summary>
    /// True - it uses stereo vision, and struggles to achieve anything good.
    /// False - it uses mono vision (LIDAR style), and works well.
    /// </summary>
    internal const bool c_useStereoVision = false; // default: false

    /// <summary>
    /// Constructor.
    /// </summary>
    static Config()
    {
        if (c_useStereoVision)
        {
            VisionSystem = new BeeVisionStereo();

            Layers = new int[] { 2, 10, 2 }; 
        }
        else
        {
            VisionSystem = new BeeVisionMono();

            Layers = new int[] { 2, 2 }; // doesn't need additional layers
        }

        Layers[0] =  VisionSystem.NeuronsRequiredForVision(); // number of inputs
        Layers[^1] = CountOfOutputNeuronsRequiredBasedOnModulation; // number of outputs
    }

    /// <summary>
    /// If true, it will colour each bee's head.
    /// If false, bee will be "bee" coloured.
    /// </summary>
    internal const bool c_paintColouredHats = false;

    /// <summary>
    /// If true, it colours the eyes. This shows
    /// that the 3d sensor originates out of the correct place.
    /// </summary>
    internal const bool c_paintBeesEyes = false;

    /// <summary>
    /// If true, it will draw a line towards the target of the bee (assuming it's
    /// not collecting nectar).
    /// If false, it won't draw an indicator.
    /// </summary>
    internal const bool c_paintBeeTarget = false;

    /// <summary>
    /// If BrieflyDisplayGhostOfEliminatedBees == true, this determines how long they are visible for.
    /// We could faff with timers, but a simple way is to do it after so many moves.
    /// </summary>
    internal static int HowLongToShowGhostOfEliminatedBees { get; set; } = 100; // moves of bees (timer based) 

    /// <summary>
    /// If TRUE it draws dot where the collision sensors are.
    /// </summary>
    internal static bool ShowHitPointsOnBee { get; set; } = false;

    /// <summary>
    /// Enable different vision systems, as long as they conform to the interface.
    /// </summary>
    internal static IVision VisionSystem; 

    /// <summary>
    /// Learning requires us to create a number of bees, and mutate the worst 50%. 
    /// This happens repeatedly, resulting in a more fitting NN being selected.
    /// </summary>
    internal static int NumberOfAIBeesInHive { get; set; } = 24;

    /// <summary>
    /// After this amount of MOVES has elapsed, a mutation occurs.
    /// </summary>
    internal static int BeeMovesBeforeFirstMutation { get; set; } = 500; // moves

    /// <summary>
    /// How long a day is (measured by moves).
    /// </summary>
    internal static int LengthOfBeeDayInMoves = 4200; // moves;

    /// <summary>
    /// How many moves aa bee gets to return home
    /// </summary>
    internal static int AmountofMovesBeeHasToReturnHomeIn = 1000;

    /// <summary>
    /// Initializing network to the right size.
    /// First=INPUT
    /// Last=OUTPUT
    /// Use the UI to configure it.
    /// </summary>
    internal static int[] Layers { get; set; } = new int[] { 2, 20, 2 };

    /// <summary>
    /// Used to "amplify" or "reduce" NN output.
    /// Wing Flap Rate Amplifier: #amplifies the output of the neural network.
    /// </summary>
    internal static float[] outputModulation = new float[] { 2, 2 };

    /// <summary>
    /// Used to "amplify" or "reduce" NN output.
    /// Speed Amplifier: Generally keep as 1, but if more than 1, this amplifies the output of the neural network.
    /// Steering Amplifier: if more than 1, this amplifies the rotational output of the neural network.
    /// i.e if set to 5 instead of turning 1 degree, it turns 5 degrees.
    /// </summary>
    internal static float[] OutputModulation
    {
        get { return outputModulation; }
        set
        {
            outputModulation = value;
        }
    }

    /// <summary>
    /// Counts neurons required.
    /// Output modulations of zero will result in "*0" and therefore no effect. Therefore
    /// we don't include that as a neuron.
    /// </summary>
    internal static int CountOfOutputNeuronsRequiredBasedOnModulation
    {
        get
        {
            int cnt = 0;

            foreach (float f in OutputModulation)
            {
                if (f != 0) ++cnt;
            }

            return cnt;
        }
    }

    /// <summary>
    /// See FieldOfVisionStartInDegrees.
    /// </summary>
    internal static int _fieldOfVisionStartInDegrees = -120; // degrees

    /// <summary>
    ///     -45  0  45
    ///  -90 _ \ | / _ 90   <-- relative to direction of bee, hence + angle bee is pointing.
    ///   ^ this
    /// </summary>
    internal static int FieldOfVisionStartInDegrees
    {
        get { return _fieldOfVisionStartInDegrees; }
        set
        {
            if (value > FieldOfVisionStopInDegrees) FieldOfVisionStopInDegrees = value;

            _fieldOfVisionStartInDegrees = value;
        }
    }

    /// <summary>
    /// See FieldOfVisionStopInDegrees.
    /// </summary>
    internal static int _fieldOfVisionStopInDegrees = 120; // degrees

    /// <summary>
    ///     -45  0  45
    ///  -90 _ \ | / _ 90   <-- relative to direction of bee, hence + angle bee is pointing.
    ///                ^ this
    /// </summary>
    internal static int FieldOfVisionStopInDegrees
    {
        get { return _fieldOfVisionStopInDegrees; }

        set
        {
            if (value < FieldOfVisionStartInDegrees) FieldOfVisionStartInDegrees = value;

            _fieldOfVisionStopInDegrees = value;
        }
    }

    /// <summary>
    /// Do we check for 5 e.g. -90,-45,0,+45,+90, or just -45,0,45? etc.
    /// It will divide the field of view by this amount. 
    //              (3)  
    ///     (2) -45  0  45 (4)
    /// (1)  -90 _ \ | / _ 90  (5)  <-- # sample points = 5.
    /// </summary>
    internal static int SamplePoints { get; set; } = 60; //7;

    /// <summary>
    /// See DepthOfVisionInPixels.
    /// </summary>
    internal static int _depthOfVisionInPixels = 70; // px

    /// <summary>
    /// ##########
    /// 
    ///    ¦    }
    ///    ¦    } how far the AI looks ahead
    ///    ¦    }
    ///   (o)  bee
    /// </summary>
    internal static int DepthOfVisionInPixels
    {
        get { return _depthOfVisionInPixels; }

        set
        {
            if (value < 1) throw new ArgumentOutOfRangeException(nameof(value));

            _depthOfVisionInPixels = value;
        }
    }

    /// <summary>
    /// Subtracts the 2 angles and divides by sample point.
    /// </summary>
    internal static float VisionAngleInDegrees
    {
        get
        {
            return SamplePoints == 1 ? 0 : (float)(FieldOfVisionStopInDegrees - FieldOfVisionStartInDegrees) / (SamplePoints - 1);
        }
    }

    /// <summary>
    /// Bees are able to move in a pretty agile way, but it's not just back/forwards.
    /// They can drift sideways presumably by flapping differently.
    /// True - enables it to move sideways as well as rotation.
    /// False - bee is contrained to rotation.
    /// </summary>
    internal static bool BeeCanDriftSideways { get; set; } = true; // default = true

    /// <summary>
    /// If set to true, it draws what the BEE sees as an ascii art file.
    /// </summary>
    internal static bool OutputAsciiArtSilhouette { get; set; } = false;
}