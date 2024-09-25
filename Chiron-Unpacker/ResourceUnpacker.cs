using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Xml.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Chiron_Unpacker
{
    /// <summary>
    /// Class <c>ResourceUnpacker</c> manages deobfuscation process.
    /// </summary>
    public class ResourceUnpacker
    {
        private ModuleDefMD module;
        private Assembly assembly;
        private string File;
        private string Result;
        private MethodDef rc4Function;
        private string rc4Key;
        private string resourceName;

        public ResourceUnpacker(string filePath, string resultDir)
        {
            File = filePath;
            Result = resultDir;
        }

        public void Unpack()
        {
            /*
             *  49	00B5	nop
                50	00B6	nop
                51	00B7	ldsfld	string ...::RESOURCE_NAME
                52	00BC	call	uint8[] ...::GetResourceObject(string)
                53	00C1	ldsfld	string ...::DECRYPTION_KEY
                54	00C6	call	uint8[] ...::RC4_FUNCTION(uint8[], string)
             * 
             */

            (MethodDef unpackMethod, int index) = FindXref(rc4Function);
            if (unpackMethod == null)
            {
                Console.WriteLine("Error! There is no unpack method");
                return;
            }
            Console.WriteLine($"UnpackMethod: {unpackMethod.MDToken.ToString()}, index: {index}");
            
            MethodDef cctorMethod = unpackMethod.DeclaringType.FindStaticConstructor();
            if (cctorMethod == null)
            {
                Console.WriteLine("Error! There was a problem getting the cctor method");
                return;
            }
            Console.WriteLine($"Constructor method MDToken: {cctorMethod.MDToken.ToString()}");

            IList<Instruction> instructions = unpackMethod.Body.Instructions;
            if (instructions[index - 1].OpCode == OpCodes.Ldsfld
                && instructions[index - 3].OpCode == OpCodes.Ldsfld)
            {
                FieldDef fDecryptionKey = (FieldDef)instructions[index - 1].Operand;
                FieldDef fResourceName = (FieldDef)instructions[index - 3].Operand;
                rc4Key = GetFieldValue(fDecryptionKey, cctorMethod);
                resourceName = GetFieldValue(fResourceName, cctorMethod);
                Console.WriteLine($"Decryption Key: {rc4Key}\nResource Name: {resourceName}");
            }
            else
            {
                Console.WriteLine("Error! there was a problem getting the fields");
                return;
            }

            Stream manifestStream = assembly.GetManifestResourceStream($"{resourceName}.resources");
            byte[] resourceData = null;
            using (ResourceReader reader = new ResourceReader(manifestStream))
            {
                IDictionaryEnumerator enumerator = reader.GetEnumerator();
                while(enumerator.MoveNext())
                {
                    var name = (string)enumerator.Key;
                    string _;
                    reader.GetResourceData(name, out _, out resourceData);
                }
            }

            byte[] newArray = new byte[resourceData.Length - 4];
            Array.Copy(resourceData, 4, newArray, 0, newArray.Length);

            byte[] decryptedResource = RC4Decrypt(newArray, rc4Key);
            System.IO.File.WriteAllBytes(Path.Combine(Result, $"{resourceName}.bin"), decryptedResource);
        }

        public static byte[] RC4Decrypt(byte[] resource, string rc4Key)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(rc4Key);
            int i = 0;
            while (i <= resource.Length)
            {
                resource[i % resource.Length] = Convert.ToByte((Convert.ToInt32(resource[i % resource.Length] ^ bytes[i % bytes.Length]) - Convert.ToInt32(resource[(i + 1) % resource.Length]) + 256) % 256);
                i++;
            }
            Array.Resize(ref resource, resource.Length - 1);
            return resource;
        }

        public string DynamicStringDecrypter(MethodSpec method, uint decryptKey)
        {
            try
            {
                Module manifestModule = assembly.ManifestModule;
                return (string)manifestModule.ResolveMethod(method.MDToken.ToInt32()).Invoke(null, new object[] { decryptKey });
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }


        public string GetFieldValue(FieldDef field, MethodDef cctor)
        {
            /*
             * 132	023A	ldc.i4	0x7B1DF5C5
                133	023F	call	DECRYPT_METHOD
                134	0244	stsfld	string LQTMDMee8GPq3vc7go.kDdORR4d0BYgXElVp6::JVEV1aGXTs
             */
            IList<Instruction> instructions = cctor.Body.Instructions;
            for (int i = 0; i < instructions.Count; i++)
            {
                if (instructions[i].OpCode == OpCodes.Stsfld
                    && (FieldDef)instructions[i].Operand == field)
                {
                    return DynamicStringDecrypter((MethodSpec)instructions[i - 1].Operand, (uint)instructions[i - 2].GetLdcI4Value());
                }
            }
            return null;
        }

        /// <summary>
        /// Method <c>FindDecryptionMethod</c> tries to find the RC4 decryption method.
        /// </summary>
        public bool FindDecryptionMethod()
        {
            // pattern:
            /* 
             * 37  0046    sub
             * 38  0047    ldc.i4  0x100
             * 39  004C    add
             * 40  004D    ldc.i4  0x100
             * 41  0052    rem
             * 42  0053    call ...
             */

            foreach (TypeDef type in module.Types)
            {
                if (!type.HasMethods)
                    continue;

                if (!type.IsSealed)
                    continue;

                foreach(MethodDef method in type.Methods)
                {
                    if (!method.HasBody)
                        continue;

                    if (method.Body.Instructions.Count < 80)
                        continue;

                    for(int i = 2; i < method.Body.Instructions.Count; i++)
                    {
                        IList<Instruction> instructions = method.Body.Instructions;

                        if (instructions[i].OpCode == OpCodes.Sub
                            && instructions[i + 1].IsLdcI4() && instructions[i + 1].GetLdcI4Value() == 256
                            && instructions[i + 2].OpCode == OpCodes.Add
                            && instructions[i + 3].IsLdcI4() && instructions[i + 3].GetLdcI4Value() == 256
                            && instructions[i + 4].OpCode == OpCodes.Rem  // % operator
                            && instructions[i + 5].OpCode == OpCodes.Call)
                        {
                            rc4Function = method;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public (MethodDef, int) FindXref(MethodDef method)
        {
            string functionName = method.Name;

            foreach (TypeDef type in module.Types)
            {
                if (!type.HasMethods)
                    continue;

                foreach(MethodDef methodDef in type.Methods)
                {
                    if (!methodDef.HasBody)
                        continue;

                    IList<Instruction> instructions = methodDef.Body.Instructions;
                    for (int i = 0; i < instructions.Count; i++)
                    {
                        try
                        {
                            if (instructions[i].OpCode == OpCodes.Call)
                            {
                                if (instructions[i].Operand.ToString().Contains(functionName))
                                {
                                    if (instructions.Count < 10)
                                        return FindXref(methodDef);
                                    else
                                        return (methodDef, i);
                                }
                            }
                        }
                        catch (Exception)
                        {
                            continue;
                        }
                    }
                }
            }

            return (null, 0);
        }

        /// <summary>
        /// Method <c>CheckFile</c> tries to load the File into memory.
        /// </summary>
        public bool CheckFile()
        {
            if (File == null)
            {
                return false;
            }

            try
            {
                module = ModuleDefMD.Load(File);
                assembly = Assembly.LoadFrom(File);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Console.WriteLine($"Exception >>> {File}");
                return false;
            }

            if (!module.HasResources)
                return false;

            if (FindDecryptionMethod())
            {
                Console.WriteLine($"Found RC4 Decryption Method {rc4Function.MDToken.ToString()}");
                return true;
            }
            else
            {
                Console.WriteLine("Decryption method not found");
                return false;
            }
        }

    }
}
