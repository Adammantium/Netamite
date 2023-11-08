using Netamite.TickingEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetamiteTest.Ticking {
    internal class TickingTest {

        public static void DoTest() {
            TickingEngine tickingEngine = new TickingEngine(new Netamite.TickingEngine.Data.TickingEngineConfiguration());

            TickingEntityTest tickingEntity = new TickingEntityTest();
            tickingEntity.Start(tickingEngine);

            tickingEngine.Start();

            Console.Read();

            tickingEngine.Stop();
        }
    }
}
