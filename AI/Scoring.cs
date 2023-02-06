using Bees.Utilities;
using Bees.World.Bee;
using static Bees.World.Bee.AIControlledBee;

namespace Bees.AI
{
    /// <summary>
    /// Scoring for the bees.
    /// </summary>
    internal static class Score
    {
        /// <summary>
        /// Gets the score for this bee. It is used to rank the bees, and decide which to mutate
        /// </summary>
        /// <param name="bee"></param>
        /// <returns>Score</returns>
        internal static float Get(AIControlledBee bee)
        {
            // The further the bee flies the more points. This encourages it to pick bees
            // that leave the hive and forage.
            float fitness = bee.DistanceTravelled;

            // The goal is nectar, so more flowers visited the better the bee. We reward handsomely
            // for doing so.
            fitness += bee.nectarCollected * 10000;

            // We need to encourage them to leave the hive, so bonus 1000 for that.
            if (bee.nectarCollected > 0 || bee.Location.X > 111) fitness += 1000;

            // Extra points for returning home in time to sleep.
            if (bee.currentTaskOfBee == BeeTask.Sleep) fitness += 1000;

            // They get no points for being near where they started, unless they left the hive and got some nectar as
            // laziness shall not be tolerated.
            if (bee.currentTaskOfBee == BeeTask.CollectNectar && bee.nectarCollected == 0 && MathUtils.DistanceBetweenTwoPoints(bee.StartLocation, bee.Location) < 40) fitness = 0;
            
            return fitness;
        }
    }
}
