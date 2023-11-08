using System;

namespace Netamite.TickingEngine.Executor {
    public class SimpleTickExecuter : TickExecuter {
        public SimpleTickExecuter() { }

        public void ExecuteTick(Action action) {
            action();
        }
    }
}
