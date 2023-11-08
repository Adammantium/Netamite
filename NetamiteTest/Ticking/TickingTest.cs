using Netamite.TickingEngine;
using Netamite.TickingEngine.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetamiteTest.Ticking {
    internal class TickingTest {

        public static void DoTest() {
            TickingEngineConfiguration config = new TickingEngineConfiguration { 
                                                    TickRate = 2
                                                };

            TickingEngine tickingEngine = new TickingEngine(config);

            TickingEntityTest tickingEntity = new TickingEntityTest();
            tickingEntity.tickingRate = TickingRate.FULL;
            tickingEntity.Start(tickingEngine);

            tickingEntity = new TickingEntityTest();
            tickingEntity.tickingRate = TickingRate.HALF;
            tickingEntity.Start(tickingEngine);

            tickingEntity = new TickingEntityTest();
            tickingEntity.tickingRate = TickingRate.QUARTER;
            tickingEntity.Start(tickingEngine);

            tickingEngine.Start();

            Console.Read();

            tickingEngine.Stop();
        }
    }
}
