using System;
using System.Reflection;
using System.IO;
using System.Linq;
using CommandLine;
using HarmonyLib;

namespace Chiron_Unpacker
{
    internal class Chiron
    {
        public static string inputFile = "";
        public static string resultDir = "";

        static void Main(string[] args)
        {
            Console.WriteLine("Chiron Unpacker by Malwation");

            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(o =>
                {
                    // Check sample file
                    if (!File.Exists(o.File))
                    {
                        throw new Exception("Input file is invalid");
                    }

                    // Check output directory
                    if (!Directory.Exists(o.Output))
                    {
                        Directory.CreateDirectory(o.Output);
                        Logger.Info("Result folder created");
                    }

                    inputFile = o.File;
                    resultDir = o.Output;
                });
            
            // Load Assembly File
            try
            {
                // Load input file
                Assembly assembly = Assembly.LoadFile(inputFile);

                // Get Assembly.Load(byte[] rawAssembly) method
                MethodInfo mAssemblyLoad = typeof(Assembly).GetMethod("Load", new[] { typeof(byte[]) });
                if (mAssemblyLoad == null)
                    throw new Exception("Could not resolve Assembly.Load");

                // Create a Harmony instance
                Harmony harmony = new Harmony("Chiron");
                
                // Get prefix method (Load)
                MethodInfo mLoadPatch = typeof(Chiron).GetMethod("PreFix_Load");

                // Patch Load Function
                harmony.Patch(mAssemblyLoad, new HarmonyMethod(mLoadPatch));

                // Execute Assembly
                assembly.EntryPoint.Invoke(null, new object[] { null });

            }
            catch (Exception)
            {
                return;
            }
        }

        public static bool PreFix_Load(ref byte[] rawAssembly)
        {
            string resultPath = Path.Combine(resultDir, GetHashSHA256(rawAssembly));
            File.WriteAllBytes(resultPath, rawAssembly);
            return true;
        }

        public static string GetHashSHA256(byte[] data)
        {
            using (var sha256 = new System.Security.Cryptography.SHA256CryptoServiceProvider())
            {
                return string.Concat(sha256.ComputeHash(data).Select(x => x.ToString("X2")));
            }
        }
    }
}
