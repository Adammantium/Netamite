using System;

namespace Netamite.TickingEngine.Executor {
    public interface TickExecuter {
        void ExecuteTick(Action action);
    }
}
