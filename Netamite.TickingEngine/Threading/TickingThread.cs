using Netamite.TickingEngine.Entity;
using System; 
using System.Threading;

namespace Netamite.TickingEngine.Threading {
    internal class TickingThread {

        private CancellationTokenSource cancellationTokenSource;
        private TickingEngine engine;

        private long tick = 0;

        public TickingThread(TickingEngine engine) {
            this.engine = engine;
            cancellationTokenSource = new CancellationTokenSource();
        }

        public void Run() {
            while(!cancellationTokenSource.IsCancellationRequested) {
                StartTick();

                engine.config.TickExecuter.ExecuteTick(Tick);

                EndTick();
            }
        }

        private void Tick() {
            foreach(TickingEntity tickingEntity in engine.tickingEntities.Keys) {
                int each_n_ticks = (int) (engine.config.TickRate * tickingEntity.tickingRate);
                each_n_ticks = Math.Min(Math.Max(1, each_n_ticks), engine.config.TickRate);
                each_n_ticks = engine.config.TickRate / each_n_ticks;
                each_n_ticks = Math.Min(Math.Max(1, each_n_ticks), engine.config.TickRate);

                Console.WriteLine(tick + " " + each_n_ticks);
                if(tick % each_n_ticks == 0) {
                    tickingEntity.OnTick();
                }
            }
        }

        private long startTick = 0;
        private void StartTick() {
            startTick = DateTime.UtcNow.Ticks;
        }

        private void EndTick() {
            int tickLength = 1000 / engine.config.TickRate;

            int tickDuration = (int) (DateTime.UtcNow.Ticks - startTick);

            tick++;

            //Console.WriteLine(tick + ": " + tickDuration + " / " + tickLength);
            if(tickDuration > tickLength) {
                // TODO: Error that Ticking Engine cant keep up
                return;
            }
            Thread.Sleep(tickLength - tickDuration);
        }

        public void Cancel() {
            cancellationTokenSource.Cancel();
        }

    }
}
