using System;

namespace NetamiteTest {
    public class NetamiteTest {
        public static void Main(string[] args) {
            Console.WriteLine("  _   _      _                  _ _       \r\n | \\ | |    | |                (_) |      \r\n |  \\| | ___| |_ __ _ _ __ ___  _| |_ ___ \r\n | . ` |/ _ \\ __/ _` | '_ ` _ \\| | __/ _ \\\r\n | |\\  |  __/ || (_| | | | | | | | ||  __/\r\n |_| \\_|\\___|\\__\\__,_|_| |_| |_|_|\\__\\___|\r\n                                          \r\n                                          ");

            Console.WriteLine("==> TCP/UDP");
            IPTest.DoTest();

            Console.WriteLine("");
            Console.WriteLine("==> STEAM");
            SteamTest.DoTest();


            Console.WriteLine("");
            Console.WriteLine(">>>>> TEST DONE <<<<<<");
            Console.Read();
        }
    }
}
