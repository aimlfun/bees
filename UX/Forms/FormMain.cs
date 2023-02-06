using Bees.Learn;
using Bees.Settings;
using Bees.World.PlayGround;

namespace Bees.UX.Forms.MainUI;

public partial class FormMain : Form
{
    /// <summary>
    /// Where it loads / saves from.
    /// </summary>
    private const string c_aiModelFilePath = @"c:\temp\bee-model{{id}}.ai";

    /// <summary>
    /// Font when displaying available key presses and 
    /// </summary>
    private static readonly Font s_generationAndKeysFont = new("Arial", 8);

    /// <summary>
    /// Font when displaying the "generation".
    /// </summary>
    private static readonly Font s_fontGeneration = new("Arial", 9);

    /// <summary>
    /// Font when displaying the scoreboard.
    /// </summary>
    private static readonly Font s_fontTotals = new("Lucida Console", 12);

    /// <summary>
    /// Use for outputting top-left aligned text in DrawString().
    /// </summary>
    private static readonly StringFormat sf = new()
    {
        Alignment = StringAlignment.Near,
        LineAlignment = StringAlignment.Near
    };

    /// <summary>
    /// Indicates whether it is learning without painting the UI.
    /// True - learning without
    /// False - visuals displayed, learning more slowly.
    /// </summary>
    private bool quietLearn = false; // default: false

    /// <summary>
    /// 
    /// </summary>
    int lastGenerationPainted = -1;

    /// <summary>
    /// This is the canvas we're using (panelTrack to be precise)
    /// </summary>
    internal static FormMain? s_Canvas;

    /// <summary>
    /// Determines whether to show the scenery of the silhouette (what the bee sees).
    /// </summary>
    internal static bool s_showSilhouette = false;

    /// <summary>
    /// Determins whether the scoreboard is visible or not.
    /// </summary>
    internal static bool s_showingScoreBoard = false;

    /// <summary>
    /// Constructor.
    /// </summary>
    public FormMain()
    {
        InitializeComponent();

        // make the playground double buffered etc.
        panelBeePlayGround.GetType().GetMethod("SetStyle",
                                                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(panelBeePlayGround, new object[]
                                                {
                                                    ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint,
                                                    true
                                                });

        panelBeePlayGround.Paint += PanelBeePlayGround_Paint;

        KeyDown += FormMain_KeyDown; // Some settings used less regularly are invoked by keyboard shortcuts
    }

    /// <summary>
    /// Keyboard Shortcuts - frame-rate, pause, radar, quiet, hit-points.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void FormMain_KeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.M:
                // forces immediate mutation
                BeeController.ForceMutate();
                break;

            case Keys.F:
                // frame-rate toggle fast<>slow
                BeeController.s_timerForBeeMove.Interval = (BeeController.s_timerForBeeMove.Interval == BeeController.c_timeInMSBetweenBeeMoves ? 300 : BeeController.c_timeInMSBetweenBeeMoves);
                break;

            case Keys.P:
                // pause
                BeeController.s_timerForBeeMove.Enabled = !BeeController.s_timerForBeeMove.Enabled;
                break;

            case Keys.V:
                // bee vision is a silhouette, they see flower centre and tree stump plus walls
                s_showSilhouette = !s_showSilhouette;
                break;

            case Keys.R:
                // radar toggle
                BeeController.ShowRadar = !BeeController.ShowRadar;
                break;

            case Keys.Q:
                /// When "quiet" no rendering occurs (playground/bees). The AI process is accelerated.
                quietLearn = !quietLearn;
                lastGenerationPainted = -1;
                BeeController.SetQuietModeOnOff(quietLearn); // reduces or increases time

                panelBeePlayGround.Invalidate();
                break;

            case Keys.H:
                // show or hide hit points
                Config.ShowHitPointsOnBee = !Config.ShowHitPointsOnBee;
                break;

            case Keys.L:
                // loads AI and restarts it.                
                LoadAIModel();
                break;

            case Keys.S:
                // Saves AI to disk                
                BeeController.SaveNeuralNetworkStateForBees(c_aiModelFilePath);
                break;

            case Keys.D:
                // enable/disable mutation
                BeeController.s_stopMutation = !BeeController.s_stopMutation;
                break;

            case Keys.T:
                // show the path the bee took
                BeeController.ShowTrail = !BeeController.ShowTrail;
                break;

            case Keys.B:
                // show the scoreboard (brain # vs. flowers collected)
                s_showingScoreBoard = !s_showingScoreBoard;
                break;
        }
    }

    /// <summary>
    /// Updates generation text, and if not in quiet mode paints the playground and bees.
    /// </summary>
    internal void PaintFrameOfBeePlayGround()
    {
        panelBeePlayGround.Invalidate();
    }

    /// <summary>
    /// Called when the form loads. In initialises the world-manager.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void FormMain_Load(object sender, EventArgs e)
    {
        s_Canvas = this;

        panelBeePlayGround.Cursor = Cursors.Arrow;
        PlayGroundCreator.RenderAsImage(panelBeePlayGround.Width, panelBeePlayGround.Height);

        BeeController.StartLearning();
    }

    /// <summary>
    /// Returns true/false as ON/OFF.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    private string BoolToOnoff(bool value)
    {
        return value ? "ON" : "OFF";
    }

    /// <summary>
    /// Paint the background, and bees (with debug like radar).
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void PanelBeePlayGround_Paint(object? sender, PaintEventArgs e)
    {
        Graphics graphics = e.Graphics;

        if (quietLearn)
        {
            DisplayGenerationWhilstInQuietLearn(graphics);
            return;
        }

        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

        PlayGroundCreator.DrawEverything(graphics);

        NectarGauge.Draw(graphics, panelBeePlayGround.Height);

        DisplayGenerationAndKeyboardControls(graphics);

        if (Config.c_acceleratedTraining)
        {
            DrawCross(new PointF(55, 260), graphics);
        }
        else
        {
            DrawCross(new PointF(300, 260), graphics);
            DrawCross(new PointF(85, 260), graphics);
        }
        ShowScoreBoard(graphics);
    }

    /// <summary>
    /// Draws a cross where the bees need to head to return to the hive.
    /// </summary>
    /// <param name="location"></param>
    /// <param name="graphics"></param>
    private void DrawCross(PointF location, Graphics graphics)
    {
        // -5     +5
        //   
        //   :    :    
        //             __ -5
        //    \  /     
        //      +
        //    /   \    __ +5
        //       
        graphics.DrawLine(Pens.Red, location.X - 5, location.Y - 5, location.X + 5, location.Y + 5); // top-left to bottom right
        graphics.DrawLine(Pens.Red, location.X + 5, location.Y - 5, location.X - 5, location.Y + 5); // top-right to bottom left
    }

    /// <summary>
    /// Outputs scores for the "brains".
    /// </summary>
    /// <param name="graphics"></param>
    private void ShowScoreBoard(Graphics graphics)
    {
        if (!s_showingScoreBoard) return;

        // construct a string and draw all of it including line breaks
        // is quicker than drawing multiple lines of text (an easier)
        string label = "";

        foreach (string id in BeeController.s_totals.Keys)
        {
            label += $"{id} - {BeeController.s_totals[id]}\n";
        }

        graphics.DrawString(label, s_fontTotals, Brushes.Black, new Rectangle(120, 10, panelBeePlayGround.Width - 130, panelBeePlayGround.Height - 20));
    }

    /// <summary>
    /// Keyboard commands / generations.
    /// </summary>
    /// <param name="graphics"></param>
    private void DisplayGenerationAndKeyboardControls(Graphics graphics)
    {
        // much of this could be saved as part of the static background image to avoid drawing
        // except the ON/OFF, and generation complicate it (it would need to wipe the background
        // and re-paint. Also the silhouette would contain text the bees would not like, so
        // for now, this is the easiest and cleanest (but not fastest) approach.
        string label =
                    $"Generation: {BeeController.s_generation}\n" +
                    "\n" +
                    $"Mutate in: {BeeController.s_movesLeftBeforeNextMutation}\n" +
                    "\n" +
                    "[M]utate Now\n" +
                    "[D]isable Mutation\n" +
                    $"[P]ause ({BoolToOnoff(!BeeController.s_timerForBeeMove.Enabled)})\n" +
                    $"[R]adar ({BoolToOnoff(BeeController.ShowRadar)})\n" +
                    $"[H]itpoints ({BoolToOnoff(Config.ShowHitPointsOnBee)})\n" +
                    $"[V]ision ({BoolToOnoff(s_showSilhouette)})\n" +
                    $"[T]rail ({BoolToOnoff(BeeController.ShowTrail)})\n" +
                    "[S]ave AI\n" +
                    "[L]oad AI";

        graphics.DrawString(label, s_generationAndKeysFont, Brushes.Black, new Rectangle(Config.c_acceleratedTraining ? 120 : 10, 10, 120, 200), sf);
    }

    /// <summary>
    /// Display generation number whilst quiet learning.
    /// </summary>
    /// <param name="graphics"></param>
    private void DisplayGenerationWhilstInQuietLearn(Graphics graphics)
    {
        if (lastGenerationPainted == BeeController.s_generation) return;

        lastGenerationPainted = BeeController.s_generation;

        graphics.Clear(Color.Black);
        graphics.DrawString($"GENERATION: {lastGenerationPainted}", s_fontGeneration, Brushes.White, 10, 200);
    }

    /// <summary>
    /// User clicked the [Mutate] button.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ButtonForceMutate_Click(object sender, EventArgs e)
    {
        if (!BeeController.InLearning)
        {
            MessageBox.Show("Mutate only works whilst learning.");
            return;
        }

        BeeController.ForceMutate();
    }

    /// <summary>
    /// User clicked [L]
    /// </summary>
    private void LoadAIModel()
    {
        BeeController.StopLearning();
        BeeController.LoadNeuralNetworkStateForBees(c_aiModelFilePath);

        // unpauses and starts them moving.
        BeeController.InitialiseBees();
        BeeController.ResumeLearning();

        // if we don't do this, they will mutate within 100 moves or so.
        // an alternate approach is to set the number of moves before mutation to 3300.
        BeeController.s_movesLeftBeforeNextMutation = Config.LengthOfBeeDayInMoves;
        BeeController.s_movesToCountBetweenEachMutation = Config.LengthOfBeeDayInMoves;
    }

}