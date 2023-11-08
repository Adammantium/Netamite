using Netamite.TickingEngine.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netamite.TickingEngine.Entity {
    public class TickingEntity {

        private ulong _Id = 0;
        public ulong Id { get { return _Id; } }

        private TickingEngine tickingEngine = null;

        private float _tickingRate = TickingRate.HALF;
        public float tickingRate {
            get { return _tickingRate; }
        }

        public void Start(TickingEngine tickingEngine = null) {
            if(tickingEngine == null) tickingEngine = TickingEngine.instance;
            
            _Id = tickingEngine.Register(this);

            this.tickingEngine = tickingEngine;
        }

        public void Stop() {
            if(tickingEngine == null) return;

            tickingEngine.Unregister(this);
            _Id = 0;
        }

        public virtual void OnTick() { }

    }
}
