﻿using System;
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
        public static bool resourceUnpackMode = false;

        // The variable we will use when handling Assembly.Load events
        private static readonly AssemblyLoadEventHandler s_EventHandler =
          new AssemblyLoadEventHandler(OnAssemblyLoad);

        // The application may be running Environment.Exit(). 
        // So we need to handle the ProcessExit event.
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
                    resourceUnpackMode = o.Resource;
                });

            // We create a new AppDomain where we can control the AssemblyLoad events.
            // details: https://learn.microsoft.com/en-us/dotnet/api/system.appdomain?view=net-8.0
            AppDomain specialDomain = AppDomain.CreateDomain("Chiron");
            specialDomain.SetData("resultDir", resultDir);
            // If it loads any executable file into memory, we will handle and save it.
            specialDomain.AssemblyLoad += OnAssemblyLoad;
            // Callback function used to start deobfuscation processes just before process shutdown
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

            try
            {
                // We run the application we want to unpack in the controlled environment.
                specialDomain.ExecuteAssembly(inputFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public static void ResourceUnpack()
        {
            string[] dumpedFiles = Directory.GetFiles(resultDir);
            foreach (string file in dumpedFiles)
            {
                ResourceUnpacker resourceUnpacker = new ResourceUnpacker(file, resultDir);
                if (!resourceUnpacker.CheckFile())
                    continue;

                resourceUnpacker.Unpack();
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
            if (resourceUnpackMode)
            {
                Console.WriteLine("Resource Unpacking...");
                ResourceUnpack();
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
