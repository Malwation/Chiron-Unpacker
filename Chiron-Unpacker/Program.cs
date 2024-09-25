using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CommandLine;
using dnlib.DotNet;

namespace Chiron_Unpacker
{
    internal class Program
    {
        public static string inputFile = "";
        public static string resultDir = "";
        public static bool deobfuscateMode = false;

        // The variable we will use when handling Assembly.Load events
        private static readonly AssemblyLoadEventHandler s_EventHandler =
          new AssemblyLoadEventHandler(OnAssemblyLoad);

        private static readonly EventHandler exit_EventHandler =
            new EventHandler(OnProcessExit);



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

                    resultDir = Path.Combine(o.Output, $"results_{GetHashSHA256(File.ReadAllBytes(o.File))}");
                    // Check output directory
                    if (Directory.Exists(resultDir))
                    {
                        DeleteDirectory(resultDir);
                    }
                    Directory.CreateDirectory(resultDir);
                    Console.WriteLine("Result folder created");

                    inputFile = o.File;
                    deobfuscateMode = o.Deobfuscate;
                });

            // We create a new AppDomain where we can control the AssemblyLoad events.
            // details: https://learn.microsoft.com/en-us/dotnet/api/system.appdomain?view=net-8.0
            AppDomain specialDomain = AppDomain.CreateDomain("Chiron");
            specialDomain.SetData("resultDir", resultDir);
            specialDomain.AssemblyLoad += OnAssemblyLoad;
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

            try
            {
                specialDomain.ExecuteAssembly(inputFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public static void Deobfuscate()
        {
            string[] dumpedFiles = Directory.GetFiles(resultDir);
            foreach(string file in dumpedFiles)
            {
                Console.WriteLine(file);
                ResourceUnpacker deobfuscator = new ResourceUnpacker(file, resultDir);
                if (!deobfuscator.CheckFile())
                    continue;

                deobfuscator.Unpack();
                return;
            }
        }

        public static string GetHashSHA256(byte[] data)
        {
            using (var sha256 = new SHA256CryptoServiceProvider())
            {
                return string.Concat(sha256.ComputeHash(data).Select(x => x.ToString("x2")));
            }
        }

        public static void DeleteDirectory(string target_dir)
        {
            string[] files = Directory.GetFiles(target_dir);
            string[] dirs = Directory.GetDirectories(target_dir);

            foreach (string file in files)
            {
                File.SetAttributes(file, System.IO.FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (string dir in dirs)
            {
                DeleteDirectory(dir);
            }

            Directory.Delete(target_dir, false);
        }

        public static void OnProcessExit(object Sender, object Args)
        {
            Console.WriteLine("Exiting...");
            if (deobfuscateMode)
            {
                Console.WriteLine("Deobfuscating...");
                Deobfuscate();
            }
        }
        public static void OnAssemblyLoad(object Sender, AssemblyLoadEventArgs Args)
        {
            string assemblyName = Args.LoadedAssembly.GetName().Name;
            if (assemblyName == "dnlib"
                || assemblyName.StartsWith("System")
                || assemblyName.StartsWith("Microsoft"))
            {
                return;
            }
            resultDir = (string)AppDomain.CurrentDomain.GetData("resultDir");
            Console.WriteLine("[*] Loading assembly " + assemblyName);
            try
            {
                ModuleDefMD dnlibModule = ModuleDefMD.Load(Args.LoadedAssembly.ManifestModule);
                dnlibModule.Write(Path.Combine(resultDir, $"{assemblyName}.bin"));
                Console.WriteLine($"Saved {assemblyName}.bin");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load module {assemblyName}");
                Console.WriteLine(ex.ToString());
            }


        }
    }
}
