using Netamite.TickingEngine.Data;
using Netamite.TickingEngine.Entity;
using Netamite.TickingEngine.Threading;
using System.Collections.Concurrent;
using System.Threading;

namespace Netamite.TickingEngine {
    public class TickingEngine {
        internal TickingEngineConfiguration config;

        private TickingThread tickingThread;
        private Thread engineThread;

        internal static TickingEngine instance;

        internal ConcurrentDictionary<TickingEntity, ulong> tickingEntities = new ConcurrentDictionary<TickingEntity, ulong>();
        private ulong nextId = 0;

        public TickingEngine(TickingEngineConfiguration config) {
            this.config = config;
            instance = this;
            Initialize();
        }

        private void Initialize() {
            if(config.Multithreading && config.MaxThreads > 0) {
                ThreadPool.SetMaxThreads(config.MaxThreads, config.MaxThreads);
            }
        }

        public void Start() {
            tickingThread = new TickingThread(this);
            engineThread = new Thread(tickingThread.Run);
            engineThread.Name = "TickingEngine";
            engineThread.Start();
        }

        public void Stop() {
            if(engineThread.IsAlive) {
                tickingThread.Cancel();
            }
            foreach(TickingEntity entity in tickingEntities.Keys) {
                entity.Stop();
            }
        }

        internal ulong Register(TickingEntity tickingEntity) {
            if(tickingEntities.ContainsKey(tickingEntity)) {
                ulong val;
                if(tickingEntities.TryGetValue(tickingEntity, out val)) 
                    return val;
            }

            tickingEntities.TryAdd(tickingEntity, ++nextId);
            return nextId;
        }
        internal void Unregister(TickingEntity tickingEntity) {
            tickingEntities.TryRemove(tickingEntity, out _);
        }
    }
}
