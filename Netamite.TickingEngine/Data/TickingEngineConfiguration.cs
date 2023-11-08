using Netamite.TickingEngine.Executor;

namespace Netamite.TickingEngine.Data {
    public class TickingEngineConfiguration {
        public int TickRate = 16;

        public bool Multithreading = true;
        public int MaxThreads = 0;

        public TickExecuter TickExecuter = new SimpleTickExecuter();
    }
}
