using Netamite.TickingEngine.Entity;
using System;

namespace NetamiteTest.Ticking {
    internal class TickingEntityTest : TickingEntity {
        public override void OnTick() {
            Console.WriteLine("Entity " + Id);
        }
    }
}
