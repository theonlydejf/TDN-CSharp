using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Team.HobbyRobot.TDN.Base;
using Team.HobbyRobot.TDN.Core;

namespace Team.HobbyRobot.TDNTests
{
    public class Tests
    {
        static NetworkStream ns;
        public static void Main(string[] args)
        {
            IPAddress ipAddr = IPAddress.Parse("192.168.1.100");
            IPEndPoint ep = new IPEndPoint(ipAddr, 2222);
            Socket sender = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            TDNParserSettings settings = new DefaultTDNParserSettings();

            try
            {
                while (true)
                {
                    Console.WriteLine("Waiting connection ... ");
                    sender.Connect(ep);

                    ns = new NetworkStream(sender);
                    StreamWriter sw = new StreamWriter(ns);
                    StreamReader sr = new StreamReader(ns);

                    var timer = new System.Timers.Timer(9700);
                    timer.Elapsed += Timer_Elapsed;
                    timer.Start();

                    TDNRoot _root = new TDNRoot();
                    _root["service"] = (ConvertibleTDNValue)"MovementService";
                    _root["request"] = (ConvertibleTDNValue)"travel";
                    _root["params.distance"] = (ConvertibleTDNValue)300f;
                    int parse = 0;
                    int response = 0;
                    int took = 0;
                    long real = 0;
                    Stopwatch stopwatch = new Stopwatch();
                    if(false)
                    {
                        for (int i = 0; i < 100; i++)
                        {
                            stopwatch.Restart();
                            _root.WriteToStream(ns);
                            var r = TDNRoot.ReadFromStream(ns);
                            stopwatch.Stop();
                            real += stopwatch.ElapsedMilliseconds;
                            parse += (int)r["parseTook"].Value;
                            took += (int)r["took"].Value;
                            response += (int)r["responseTook"].Value;
                            Console.WriteLine(i);
                        }
                    }
                    Console.WriteLine("parse: " + parse / 100D);
                    Console.WriteLine("response: " + response / 100D);
                    Console.WriteLine("took: " + took / 100D);
                    Console.WriteLine("real: " + real / 100D);

                    while (true)
                    {
                        TDNRoot root = TDNFactory.ReadRoot("root", new DefaultTDNParserSettings(), _root);
                        root.WriteToStream(ns);
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("\n\nRecieved root:");
                        TDNFactory.PrintRoot(TDNRoot.ReadFromStream(ns));
                        Console.ResetColor();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            TDNRoot heartbeat = new TDNRoot();
            heartbeat["service"] = (ConvertibleTDNValue)"API";
            heartbeat["request"] = (ConvertibleTDNValue)"Heartbeat";
            lock(ns)
            {
                heartbeat.WriteToStream(ns);
            }
        }
    }

    public static class TDNFactory
    {
        private static Dictionary<string, Func<object>> BaseParserLookup = new Dictionary<string, Func<object>>()
        {
            { "bln", () => ReadBoolean() },
            { "flt", () => ReadFloat() },
            { "int", () => ReadInteger() },
            { "str", () => ReadString() }
        };

        public static object ReadAnyObject(string valueKey, TDNParserSettings settings)
        {
            Console.WriteLine("Select type parser:");
            PrintParsers(settings);
            string typeKey = ReadFromUser("Enter type key: ").ToLower();
            Console.ResetColor();

            return ReadAnyObject(typeKey, valueKey, settings);
        }

        public static object ReadAnyObject(string typeKey, string valueKey, TDNParserSettings settings)
        {
            Console.Write("Enter value: ");
            if (BaseParserLookup.ContainsKey(typeKey))
            {
                return BaseParserLookup[typeKey]();
            }
            else if (typeKey == new TDNRootParser().TypeKey)
            {
                return ReadRoot(valueKey, settings, new TDNRoot());
            }
            else if (typeKey == new ArrayParser().TypeKey)
            {
                return ReadArray(valueKey, settings);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Unknown type key!");
                Console.ResetColor();
                return ReadAnyObject(valueKey, settings);
            }
        }

        public static TDNRoot ReadRoot(string rootKey, TDNParserSettings settings, TDNRoot startRoot)
        {
            TDNRoot root = startRoot;
            while (true)
            {
                //TODO vytvoreni
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("\n=====================");
                Console.ResetColor();
                Console.WriteLine($"\n{ rootKey } contents: ");
                PrintRoot(root);

                Console.Write($"Add another value to { rootKey }? (y/n): ");
                char c = char.ToLower(Console.ReadKey().KeyChar);
                if (c != 'y')
                    return root;

                string path = ReadFromUser("\nEnter path: ");

                Console.WriteLine("Select type parser:");
                PrintParsers(settings);
                string typeKey = ReadFromUser("Enter type key: ").ToLower();
                Console.ResetColor();

                object obj = ReadAnyObject(typeKey, path, settings);

                root[path] = new TDNValue(obj, settings.Parsers[typeKey]);
            }
        }

        private static object ReadArray(string valueKey, TDNParserSettings settings)
        {
            Console.WriteLine($"\n\nSelect array { valueKey } item type parser:");
            PrintParsers(settings);
            string typeKey = ReadFromUser("Enter type key: ").ToLower();
            Console.ResetColor();
            if (!settings.Parsers.ContainsKey(typeKey))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Unknown type key!");
                Console.ResetColor();
                return ReadArray(valueKey, settings);
            }

            uint len = Convert.ToUInt32(ReadFromUser("Enter length of an array: "));
            object[] arr = new object[len];
            for (int i = 0; i < len; i++)
            {
                Console.Write($"{ valueKey }[{ i }]: ");
                arr[i] = ReadAnyObject(typeKey, valueKey + "[" + i + "]", settings);
            }

            return new TDNArray(arr, settings.Parsers[typeKey]);
        }

        private static string ReadFromUser(string msg)
        {
            Console.Write(msg);
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            string s = Console.ReadLine();
            Console.ResetColor();
            return s;
        }

        private static void PrintParsers(TDNParserSettings settings)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("Type Key\tParser name");
            Console.ResetColor();
            foreach (var item in settings.Parsers)
            {
                Console.WriteLine($"{ item.Key }:\t\t{ item.Value.GetType().Name }");
            }
        }

        public static object ReadBoolean() => ReadFromUser("").ToLower().Contains('t');

        public static object ReadFloat() => Convert.ToSingle(ReadFromUser(""));

        public static object ReadInteger() => Convert.ToInt32(ReadFromUser(""));

        public static object ReadString() => ReadFromUser("");

        public static void PrintRoot(TDNRoot root)
        {
            Console.WriteLine("(");
            foreach (var val in root)
            {
                Console.Write(val.Key);
                Console.Write(": ");
                if (val.Value.Value is TDNRoot root1)
                {
                    PrintRoot(root1);
                    continue;
                }
                if (val.Value.Value is TDNArray arr)
                {
                    Console.WriteLine("[");
                    foreach (object item in arr)
                    {
                        if (arr.ItemParser.TypeKey.Equals(new TDNRootParser().TypeKey))
                            PrintRoot((TDNRoot)item);
                        else
                            Console.WriteLine(item);
                        Console.Write(",");
                        continue;
                    }
                    Console.WriteLine("]");
                    continue;
                }
                Console.WriteLine(val.Value.Value);
            }
            Console.WriteLine(")");
        }
    }
}
