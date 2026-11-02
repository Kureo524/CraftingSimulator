using UnityEngine;

namespace Nucleaire.Game
{
    /// <summary>Fait avancer la simulation à rythme fixe, avec pause et accélération.</summary>
    public sealed class SimulationRunner : MonoBehaviour
    {
        public static readonly int[] Speeds = { 0, 1, 2, 4 };
        private const int MaxTicksPerFrame = 40;

        private Simulation.World world;
        private float accumulator;
        private int speedIndex = 1;
        private int lastNonZeroIndex = 1;

        public int Speed => Speeds[speedIndex];
        public int SpeedIndex => speedIndex;
        public bool Paused => Speed == 0;
        public event System.Action SpeedChanged;

        public void Init(Simulation.World w) => world = w;

        public void SetSpeedIndex(int index)
        {
            speedIndex = Mathf.Clamp(index, 0, Speeds.Length - 1);
            if (speedIndex > 0) lastNonZeroIndex = speedIndex;
            SpeedChanged?.Invoke();
        }

        public void TogglePause() => SetSpeedIndex(Paused ? lastNonZeroIndex : 0);

        private void Update()
        {
            if (world == null || Paused) return;
            float tickDuration = 1f / world.Settings.ticksPerSecond;
            accumulator += Time.unscaledDeltaTime * Speed;
            int ticks = 0;
            while (accumulator >= tickDuration && ticks < MaxTicksPerFrame)
            {
                world.Step();
                accumulator -= tickDuration;
                ticks++;
            }
            if (ticks == MaxTicksPerFrame) accumulator = 0f; // on ne rattrape pas indéfiniment un retard
        }
    }
}
