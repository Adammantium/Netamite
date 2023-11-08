using Netamite.TickingEngine.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetamiteTest.Ticking {
    internal class TickingEntityTest : TickingEntity {
        public override void OnTick() {
            Console.WriteLine("Entity "+ Id);
        }
    }
}
