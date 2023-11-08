using System;

namespace Netamite.TickingEngine.Executor {
    public class ThreadedTickExecutor : TickExecuter {
        public ThreadedTickExecutor() { }

        // TODO: Make Multithreading
        public void ExecuteTick(Action action) {
            action();
        }
    }
}
