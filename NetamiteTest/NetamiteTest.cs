using Netamite.Logging;
using Netamite.TickingEngine;
using NetamiteTest.Ticking;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using static NetamiteTest.ConsoleMenu;

namespace NetamiteTest {
    public class NetamiteTest {
        public static void Main(string[] args) {
            Console.WriteLine("  _   _      _                  _ _       \r\n | \\ | |    | |                (_) |      \r\n |  \\| | ___| |_ __ _ _ __ ___  _| |_ ___ \r\n | . ` |/ _ \\ __/ _` | '_ ` _ \\| | __/ _ \\\r\n | |\\  |  __/ || (_| | | | | | | | ||  __/\r\n |_| \\_|\\___|\\__\\__,_|_| |_| |_|_|\\__\\___|\r\n                                          \r\n                                          ");



            // Create options that you want your menu to have
            List<Option> options = new List<Option>
            {
                new Option("Ticking Engine",    () => TickingTest.DoTest()),
                new Option("Value Converter",   () => ValueConverterTest.DoTest()),
                new Option("TCP/UDP",           () => IPTest.DoTest()),
                new Option("STEAM",             () => SteamTest.DoTest()),
                new Option("TIMEOUT TEST",      () => TimeoutTest.DoTest()),
                new Option("Nested Data Test",  () => NestedTest.DoTest()),
                new Option("Exit",              () => Environment.Exit(0)),
            };

            Console.OutputEncoding = System.Text.Encoding.UTF8;

            ConsoleMenu cm = new ConsoleMenu(options);
            cm.Open();
        }
    }
}
