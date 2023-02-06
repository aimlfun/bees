using Bees.AI;
using Bees.Settings;
using Bees.Utilities;
using Bees.UX.Forms.MainUI;
using Bees.World.Bee;
using Bees.World.PlayGround;
using System.Diagnostics;

namespace Bees.Learn;

/// <summary>
/// Responsible for learning, from creation of the neural network assigned to a bee, to making bees to move around.
/// </summary>
internal static class BeeController
{
    #region PRIVATE
    /// <summary>
    /// A timer is called when this amount of time has elapsed.
    /// </summary>
    internal const int c_timeInMSBetweenBeeMoves = 5; //ms

    /// <summary>
    /// Indicates whether learning is in progress.
    /// </summary>
    private static bool s_learningInProgress = false;

    /// <summary>
    /// How many moves we've done for this mutation. Enables those that don't move to be eliminated quickly.
    /// </summary>
    internal static int s_movesMadeByBees = 0;

    /// <summary>
    /// Semaphore. When we enter the timer tick, if we don't exit swiftly we are at risk of re-entry 
    /// via Application.DoEvents(). Trails absolutely can slow proceedings down, as can radar. If we
    /// don't protect against, you can get a StackOverflow from the lines or unwanted mutation.
    /// true - we are currently in the Timer tick
    /// false - we are not in the tTimer tick
    /// </summary>
    private static bool s_inMoveBees = false;

    /// <summary>
    /// Totals per "Brain" id.
    /// </summary>
    internal static Dictionary<string /* id of brain */, int /* amount of nectar in life time */> s_totals = new();
    #endregion

    #region INTERNAL

    /// <summary>
    /// A simple timer approach that moves the bees at a fixed cadence.
    /// </summary>
    internal static System.Windows.Forms.Timer s_timerForBeeMove = new();

    /// <summary>
    /// true - draws the output of the radar, plus target (return home).
    /// false - no radar (runs quicker)
    /// </summary>
    internal static bool ShowRadar = false; // default: false

    /// <summary>
    /// Indicates everything is initialised and the bees can learn.
    /// </summary>
    internal static bool s_initialised = false;

    /// <summary>
    /// The generation (how many times the network has been mutated).
    /// </summary>
    internal static int s_generation = 0;

    /// <summary>
    /// The list of bees indexed by their "id".
    /// </summary>
    internal readonly static Dictionary<int, AIControlledBee> s_bees = new();

    /// <summary>
    /// If set to true, then this ignores requests to mutate.
    /// </summary>
    internal static bool s_stopMutation = false;

    /// <summary>
    /// Defines the number of moves it will initialise the mutate counter to.
    /// This number increases with each generation.
    /// </summary>
    internal static int s_movesToCountBetweenEachMutation = 0;

    /// <summary>
    /// Defines the number of moves before a mutation occurs. 
    /// This is decremented each time the bees move, and upon reaching zero triggers
    /// a mutation.
    /// </summary>
    internal static int s_movesLeftBeforeNextMutation = 0;

    /// <summary>
    /// Which frame of wings being painted.
    /// </summary>
    internal static int s_wingFrame0or1 = 0;

    /// <summary>
    /// Setter/Getter for turning the bee "trail" (where it went) on/off.
    /// </summary>
    internal static bool ShowTrail
    {
        get; set;
    }

    /// <summary>
    /// Indicates whether the learning is in progress.
    /// </summary>
    internal static bool InLearning
    {
        get { return s_learningInProgress; }
    }

    /// <summary>
    /// How many moves the bee has made.
    /// </summary>
    internal static int NumberOfMovesMadeByBees
    {
        get => s_movesMadeByBees;
        set
        {
            if (value < 0) Debugger.Break();            

            s_movesMadeByBees = value;
        }
    }
    #endregion

    /// <summary>
    /// Start the AI learning process.
    /// </summary>
    internal static void StartLearning()
    {
        StopLearning();

        s_generation = 0;
        HiveManager.Reset();

        if (Config.NumberOfAIBeesInHive % 2 != 0) Config.NumberOfAIBeesInHive++; // we cannot mutate 50% if it's odd, so we make it even 

        InitialiseTheNeuralNetworksForTheBees(); // this can include loading from disk

        InitialiseBees(); // creates the bees

        // initialise the bees to mutate at a pre-determined point
        s_movesToCountBetweenEachMutation = Config.BeeMovesBeforeFirstMutation;
        s_movesLeftBeforeNextMutation = s_movesToCountBetweenEachMutation;

        s_learningInProgress = true;

        // start the timer that moves the bees.
        InitialiseAndStartMoveTimer();
    }

    /// <summary>
    /// Initialise the timer. It requires us to dispose of any existing timer, otherwise we end up with
    /// multiple timers all calling the move bees. Everything seems faster without, but we've lost control.
    /// We cannot cancel or pause the timer...
    /// </summary>
    private static void InitialiseAndStartMoveTimer()
    {
        s_timerForBeeMove?.Dispose();

        s_timerForBeeMove = new System.Windows.Forms.Timer();
        s_timerForBeeMove.Tick += MoveBees_Tick;
        s_timerForBeeMove.Interval = c_timeInMSBetweenBeeMoves;

        // timer is STARTED, comment out the line below if you want it to start paused
        s_timerForBeeMove.Enabled = true;
    }

    /// <summary>
    /// Toggles quiet mode.
    /// </summary>
    /// <param name="value"></param>
    internal static void SetQuietModeOnOff(bool value)
    {
        s_timerForBeeMove.Interval = value ? 1 : c_timeInMSBetweenBeeMoves;
        s_timerForBeeMove.Start();
    }

    /// <summary>
    /// Start the AI learning process.
    /// </summary>
    internal static void ResumeLearning()
    {
        // start the independent timers
        s_learningInProgress = true;

        s_timerForBeeMove.Start();
    }

    /// <summary>
    /// Stops the learning process if running.
    /// </summary>
    internal static void StopLearning()
    {
        s_timerForBeeMove.Stop();

        if (!s_learningInProgress) return; // no need to stop

        s_learningInProgress = false;
    }

    /// <summary>
    /// Mutate timer has elapsed, it's normally time to rate the neural network quality per bee
    /// then clone & mutate the worst 50%. But the problem with that approach is that if the AI
    /// makes little progress in the time allotted, the time is wasted; versus if the time is 
    /// short the AI has no time to learn..
    /// </summary>
    private static void TimeToMutate_Tick()
    {
        PlayGroundCreator.RenderAsImage();

        // after a mutation, we humanely kill the bees and get new ones (reset their position/state) whilst keeping the neural networks
        if (!InitialiseBees())
        {
            // enable each mutation to have longer to run, so the bees go further.
            s_movesToCountBetweenEachMutation += 100;
        }

        s_movesToCountBetweenEachMutation = Math.Min(s_movesToCountBetweenEachMutation, Config.LengthOfBeeDayInMoves);
        s_movesLeftBeforeNextMutation = s_movesToCountBetweenEachMutation;
    }


    /// <summary>
    /// Request to force a mutation. We stop the mutation timer, perform the action and restart it.
    /// If we didn't reset the timer, the model will regress as bees won't have had a chance to move to their fullest.
    /// </summary>
    internal static void ForceMutate()
    {
        s_timerForBeeMove.Stop();

        TimeToMutate_Tick(); // pretend the timer fired

        s_timerForBeeMove.Start();
    }

    /// <summary>
    /// Initialises the neural network (one per bee).
    /// </summary>
    internal static void InitialiseTheNeuralNetworksForTheBees()
    {
        NeuralNetwork.s_networks.Clear();

        for (int i = 0; i < Config.NumberOfAIBeesInHive; i++)
        {
            _ = new NeuralNetwork(i, Config.Layers);
        }
    }

    /// <summary>
    /// Initialises the bees, mutating if the brains exist.
    /// </summary>
    /// <returns>true - if the bees mutated, else false.</returns>
    internal static bool InitialiseBees()
    {
        bool beesAllMutated = false;

        s_wingFrame0or1 = 0;
        HiveManager.Reset();

        s_generation++;

        NumberOfMovesMadeByBees = 0;

        // existing bees removed, and poor 50% have their brains mutated
        if (s_bees.Count > 0)
        {
            if (s_learningInProgress) MutateBeeBrains();

            if (NeuralNetwork.s_networks.Count == 0)
            {
                beesAllMutated = true;
                InitialiseTheNeuralNetworksForTheBees();
            }

            s_bees.Clear();
        }

        // create bees with their respective brain attached (create is simpler than resetting)
        for (int i = 0; i < Config.NumberOfAIBeesInHive; i++)
        {
            s_bees.Add(i, new(i));
        }

        return beesAllMutated;
    }

    /// <summary>
    /// Saves networks weights and biases to file.
    /// </summary>
    /// <param name="filename"></param>
    internal static void SaveNeuralNetworkStateForBees(string filename)
    {
        foreach (var id in NeuralNetwork.s_networks.Keys)
        {
            NeuralNetwork.s_networks[id].Save(filename.Replace("{{id}}", id.ToString()));
        }
    }

    /// <summary>
    /// Loads networks weights and biases from a file.
    /// </summary>
    /// <param name="filename"></param>
    internal static void LoadNeuralNetworkStateForBees(string filename)
    {
        foreach (var id in NeuralNetwork.s_networks.Keys)
        {
            NeuralNetwork.s_networks[id].Load(filename.Replace("{{id}}", id.ToString()));
        }

        // if we loaded the network, we're generation zero. We don't track what generation is was saved as.
        s_generation = 0;
    }

    /// <summary>
    /// Time is up or all flowers have been visited.
    /// We need to mutate the brains of the worst performing.
    /// </summary>
    private static void MutateBeeBrains()
    {
        bool completeAndUtterFailure = true; // not bees scored this time or previously.

        // update networks fitness for each bee
        foreach (int id in s_bees.Keys)
        {
            NeuralNetwork.s_networks[id].Fitness = Score.Get(s_bees[id]); // judge how well the bees have done.

            if (completeAndUtterFailure && (NeuralNetwork.s_networks[id].Fitness > 0 || NeuralNetwork.s_networks[id].LastFitness > 0)) completeAndUtterFailure = false;
        }

        if (completeAndUtterFailure)
        {
            // none of them were any good, they all score ZERO every time, throw *all* the bees away

            NeuralNetwork.s_networks.Clear();
            s_totals.Clear();
            return;
        }

        NeuralNetwork.SortNetworkByFitness(); // largest "fitness" (best performing) goes to the bottom

        // sorting is great but index no longer matches the "id".
        // this is because the sort swaps but this misaligns id with the entry            
        List<NeuralNetwork> n = new();

        foreach (int n2 in NeuralNetwork.s_networks.Keys) n.Add(NeuralNetwork.s_networks[n2]);

        NeuralNetwork[] array = n.ToArray();

        // replace the 50% worse offenders with the best, then mutate them.
        // we do this by copying top half (lowest fitness) with top half.
        for (int worstNeuralNetworkIndex = 0; worstNeuralNetworkIndex < Config.NumberOfAIBeesInHive / 2; worstNeuralNetworkIndex++)
        {
            // 50..100 (in 100 neural networks) are in the top performing
            int neuralNetworkToCloneFromIndex = worstNeuralNetworkIndex + Config.NumberOfAIBeesInHive / 2; // +50% -> top 50% 

            if (array[neuralNetworkToCloneFromIndex].Fitness <= 0)
            {
                // remove the total, it's 0.
                if (s_totals.ContainsKey(array[neuralNetworkToCloneFromIndex].BrainId)) s_totals.Remove(array[neuralNetworkToCloneFromIndex].BrainId);

                neuralNetworkToCloneFromIndex = Config.NumberOfAIBeesInHive - 1; // the best bee
            }
            else
            {
                if (!s_totals.ContainsKey(array[neuralNetworkToCloneFromIndex].BrainId)) s_totals.Add(array[neuralNetworkToCloneFromIndex].BrainId, 0);

                s_totals[array[neuralNetworkToCloneFromIndex].BrainId] += s_bees[array[neuralNetworkToCloneFromIndex].Id].nectarCollected;
            }

            NeuralNetwork.CopyFromTo(array[neuralNetworkToCloneFromIndex], array[worstNeuralNetworkIndex]); // copy

            // remove the total, it's 0.
            if (s_totals.ContainsKey(array[worstNeuralNetworkIndex].BrainId)) s_totals.Remove(array[worstNeuralNetworkIndex].BrainId);

            array[worstNeuralNetworkIndex].Mutate(30, 0.5F); // mutate
        }

        // unsort, restoring the order of bee to neural network i.e [x]=id of "x".
        Dictionary<int, NeuralNetwork> unsortedNetworksDictionary = new();

        NeuralNetwork[] nn = new NeuralNetwork[Config.NumberOfAIBeesInHive];

        // bees were sorted, this puts them in order so best brain is "0" not "23".
        PutBeeBrainsInAscendingOrderOfFitness(nn);

        // adjust slightly to provide better a exit
        GiveBestPerformingBeeTheSpotClosestToHiveEntrance(nn);

        foreach (var x in nn)
        {
            unsortedNetworksDictionary.Add(x.Id, x);
        }

        NeuralNetwork.s_networks = unsortedNetworksDictionary;
    }

    /// <summary>
    /// Ensure best brain/bee ends up in position 2 so it has the clearest
    /// exit. 
    /// e.g. If we put the best in position 3, then it is impeded by 0, 1 and 2.
    /// </summary>
    /// <param name="nn"></param>
    private static void GiveBestPerformingBeeTheSpotClosestToHiveEntrance(NeuralNetwork[] nn)
    {
        /*
         *   Best "start" location 2, 0, 1, then 5, 3, 4.
         *  +----------------+ 
         *  | < 0>      < 2> |
         *  |      < 1>      |
         *  | < 3>      < 5> |
         *  |      < 4>      |        
         */
        for (var i = 0; i < nn.Length; i += 3)
        {
            (nn[i + 2], nn[i]) = (nn[i], nn[i + 2]);

            nn[i].Id = i;
            nn[i + 2].Id = i + 2;
        }
    }

    /// <summary>
    /// Bees were sorted. 23 = top, 0 = worst.
    /// 
    /// We want the best to leave the hive first, to prevent it being impeded by poor
    /// performing best.
    /// 
    /// The only possible flaw is the rank (sort) is based on last run, not the "new" average.
    /// </summary>
    /// <param name="nn"></param>
    private static void PutBeeBrainsInAscendingOrderOfFitness(NeuralNetwork[] nn)
    {
        int idx = Config.NumberOfAIBeesInHive - 1;

        foreach (var x in NeuralNetwork.s_networks.Values)
        {
            nn[idx] = x;
            x.Id = idx;
            x.LastFitness = (x.LastFitness + x.Fitness) / 2;
            idx--;
        }
    }

    /// <summary>
    /// Move all the bees (occurs when the timer fires)
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private static void MoveBees_Tick(object? sender, EventArgs e)
    {
        if (s_inMoveBees) return;

        // interval of 1 indicates quiet mode 
        if (s_timerForBeeMove.Interval == 1)
            s_timerForBeeMove.Stop(); // we're doing it via a loop.
        else
            s_inMoveBees = true; // stop re-entrant due to tick taking too long.

        bool loop = true;

        while (loop)
        {
            s_wingFrame0or1 = 1 - s_wingFrame0or1;

            // interval 1 means move in a loop, yielding occasionally, it's quicker than waiting 1ms for the bee timer.
            if (s_timerForBeeMove.Interval != 1) loop = false;

            ++NumberOfMovesMadeByBees;

            // is it time to return to the hive?
            if (Config.LengthOfBeeDayInMoves > 2999 && BeeController.s_movesLeftBeforeNextMutation == Config.AmountofMovesBeeHasToReturnHomeIn)
            {
                NotifyBeesThatItIsHomeTime();
            }

            bool allBeesHaveBeenEliminated = false;

            MoveAllBees(ref allBeesHaveBeenEliminated);

            FormMain.s_Canvas?.PaintFrameOfBeePlayGround(); // track is drawn, with bees overlaid

            if (allBeesHaveBeenEliminated) Application.DoEvents(); // lock is required to stop this becoming re-entrant if paint takes too long

            // all bees have collided, we can't continue, so we force a mutate
            // is it time to mutate?
            if (allBeesHaveBeenEliminated || (!s_stopMutation && --s_movesLeftBeforeNextMutation < 1))
            {
                TimeToMutate_Tick();
            }

            if (s_timerForBeeMove.Interval == 1 && NumberOfMovesMadeByBees % 200 == 0) Application.DoEvents();
        }

        s_inMoveBees = false; // remove lock
    }

    /// <summary>
    /// Sets each bee in a state making it head home.
    /// </summary>
    private static void NotifyBeesThatItIsHomeTime()
    {
        // not trained enough to do the fun "return to hive" simulation?
        if (s_movesToCountBetweenEachMutation < 3000) return;

        if (Config.c_acceleratedTraining) return; // don't do during that, as the "usual" wall is blocked

        foreach (AIControlledBee bee in s_bees.Values)
        {
            // eliminated cannot return. Those not in CollectNectar are in bed or returning already.
            if (bee.HasBeenEliminated || bee.currentTaskOfBee != AIControlledBee.BeeTask.CollectNectar) continue;

            bee.currentTaskOfBee = AIControlledBee.BeeTask.ReturnToHive;
        }
    }

    /// <summary>
    /// Iterate over all the bees, moving them they will be rewarded for each flower they visit.
    /// </summary>
    /// <param name="allBeesHaveBeenEliminated"></param>
    private static void MoveAllBees(ref bool allBeesHaveBeenEliminated)
    {
        SetDarknessForNightTime();

        // if all the bees are sleeping they don't move
        if (PlayGroundCreator.BeesAreSleeping) return;

        // move each bee, and detect collisions.
        foreach (AIControlledBee bee in s_bees.Values)
        {
            if (bee.HasBeenEliminated) continue; // eliminated bees don't move.

            if (bee.currentTaskOfBee == AIControlledBee.BeeTask.Sleep) continue; // sleeping bees don't move
            
            if (--bee.NumberOfTimesNotToMoveWhileSuckingNectar > 0) continue; // bee's pause to suck nectar. If they're doing it, they miss their move.

            bee.Move();

            DetectIfBeeHasArrivedAtFlowerWithNectarAndIncrementCount(bee);
        }

        // when there is no nectar, the buzzy things have finished their job for the day.
        if (!FlowerManager.HasNectarRemaining)
        {
            allBeesHaveBeenEliminated = true;
            return;
        }

        // assume eliminated, until we discover that one has not
        allBeesHaveBeenEliminated = true;

        // work out if all bees have been eliminated - time to mutate.
        foreach (AIControlledBee bee in s_bees.Values)
        {
            if (!bee.HasBeenEliminated && bee.currentTaskOfBee != AIControlledBee.BeeTask.Sleep)
            {
                allBeesHaveBeenEliminated = false; // at least one bee is still in the game
                break;
            }
        }
    }

    /// <summary>
    /// If bee goes over a flower, and it has not already been visited then increment count, and set flag to make it pause.
    /// </summary>
    /// <param name="bee"></param>
    private static void DetectIfBeeHasArrivedAtFlowerWithNectarAndIncrementCount(AIControlledBee bee)
    {
        int x = (int)bee.Location.X;
        int y = (int)bee.Location.Y;

        if (!FlowerManager.FlowerAt(new Point(x, y))) return;


        ++bee.nectarCollected;

        // stop other bees going to the flower (and this repeatedly visiting same ones)
        FlowerManager.RemoveAt(new PointF(x, y));

        // makes the bee pause
        bee.NumberOfTimesNotToMoveWhileSuckingNectar = 25;
    }

    /// <summary>
    /// Enables us to start dark, and lighten. Bees awaken.
    /// Do the same in reverse.
    /// </summary>
    private static void SetDarknessForNightTime()
    {
        // end of day?
        if (s_movesMadeByBees > Config.LengthOfBeeDayInMoves - (float)Config.AmountofMovesBeeHasToReturnHomeIn / 4f)
        {
            // yes, how dark?
            float night = ((((float)Config.LengthOfBeeDayInMoves - (float)s_movesMadeByBees) / (float)Config.AmountofMovesBeeHasToReturnHomeIn) * 128 - 60).Clamp(0, 128);

            PlayGroundCreator.SetNightTime((int)night);
            return;
        }

        switch (s_movesMadeByBees)
        {
            case > 70: // day time after 70 frames
                return;

            case < 10: // <10 is black (dead of night)
                PlayGroundCreator.SetNightTime(1);
                break;

            case < 68: // <60 varying degrees of night
                PlayGroundCreator.SetNightTime(1 + 4 * (s_movesMadeByBees - 9));
                break;

            default: // night is no more...
                PlayGroundCreator.SetDayTime();
                break;
        }
    }
}