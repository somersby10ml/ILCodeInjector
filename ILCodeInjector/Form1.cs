using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

using Mono.Cecil;
using Mono.Cecil.Cil;

namespace ILCodeInjector
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            string targetPath = Application.ExecutablePath;
            string savePath = Directory.GetCurrentDirectory() + "\\save.exe";

            #region compile inject.cs
            String outputFile = "tmpres.exe";
            string injectSource = File.ReadAllText("inject.cs");

            Compiler compiler = new Compiler();

            // import reference
            string[] references = {
                "System.dll", "System.Core.dll", "mscorlib.dll",
                "System.Windows.Forms.dll",  "System.Threading.Thread.dll",
                "System.Runtime.InteropServices.dll", "System.Threading.dll",
                "System.Threading.Thread.dll", "System.Diagnostics.TraceSource.dll" };

            compiler.SetReferences(references);
            compiler.SetOutput(outputFile);

            /* compile the user's code */
            try
            {
                compiler.Compile(injectSource);
            }
            catch (Exception ee)
            {
                MessageBox.Show("Compiler Error " + ee.ToString());
                return;
            }
            #endregion

            AssemblyHandler tmpAssembly = new AssemblyHandler();        // compiled file
            AssemblyHandler assemblyToInject = new AssemblyHandler();   // target file
            if (tmpAssembly.LoadAssembly(outputFile) != true)
            {
                MessageBox.Show("Assembly Load Error " + outputFile);
                return;
            }

            if (assemblyToInject.LoadAssembly(targetPath) != true)
            {
                MessageBox.Show("Assembly Load Error " + targetPath);
                return;
            }

            TypeDefinition tmpProgramType = tmpAssembly.FindTypeInAssembly("Program1");
            if (tmpProgramType == null)
            {
                MessageBox.Show("inject.cs Class Program1 Not Found");
                return;
            }

            // Get EntryPoint
            MethodDefinition entryMethod = assemblyToInject.GetEntryPoint();
            if (entryMethod == null)
            {
                MessageBox.Show("Target File EntryPoint Not Found");
                return;
            }
            TypeDefinition entryType = entryMethod.DeclaringType;

            // api List
            List<MethodDefinition> copyMethod = new List<MethodDefinition>();

            #region Copy ILCode method from compiled inect.cs
            foreach (MethodDefinition method in tmpProgramType.Methods)
            {
                // is api?   api is no body
                bool isAPI = (method.Attributes & Mono.Cecil.MethodAttributes.PInvokeImpl) != 0;
                if (isAPI)
                {
                    ModuleReference kernel32Ref = new ModuleReference(method.PInvokeInfo.Module.Name);
                    assemblyToInject.assembly.MainModule.ModuleReferences.Add(kernel32Ref);

                    TypeReference returnType = assemblyToInject.assembly.MainModule.ImportReference(method.ReturnType);
                    
                    // obf
                    //string apiRandomName = CRandom.RandomString(10);
                    //MethodDefinition loadLibraryA = new MethodDefinition(apiRandomName, method.Attributes, returnType);
                    MethodDefinition loadLibraryA = new MethodDefinition(method.Name, method.Attributes, returnType);
                   
                    loadLibraryA.IsPreserveSig = true;
                    loadLibraryA.PInvokeInfo = new PInvokeInfo(method.PInvokeInfo.Attributes, method.PInvokeInfo.EntryPoint, kernel32Ref);

                    // api parameter copy
                    foreach (ParameterDefinition qq in method.Parameters)
                    {
                        TypeReference parameterType = assemblyToInject.assembly.MainModule.ImportReference(qq.ParameterType);
                        loadLibraryA.Parameters.Add(new ParameterDefinition(qq.Name, qq.Attributes, parameterType));
                    }
                    copyMethod.Add(loadLibraryA);   // copy
                    entryType.Methods.Add(loadLibraryA);
                }
                else if (method.Name != ".ctor")
                {
                    if (method.Name == "Test")
                    {
                        continue;
                    }

                    // is Function
                    ILProcessor methodIL = method.Body.GetILProcessor();
                    TypeReference returnType = assemblyToInject.assembly.MainModule.ImportReference(method.ReturnType.Resolve());

                    MethodDefinition testFunction = new MethodDefinition(method.Name, method.Attributes, returnType);
                    ILProcessor testFunctionIL = testFunction.Body.GetILProcessor();

                    entryType.Methods.Add(testFunction);
                    copyMethod.Add(testFunction);   // 복사

                    // Code Copy
                    tmpAssembly.CopyMethodToAssembly(assemblyToInject, entryType.Name, method.Name, "Program1", method.Name);

                    // Copy Parameter
                    foreach (ParameterDefinition qq in method.Parameters)
                    {
                        TypeReference parameterType = assemblyToInject.assembly.MainModule.ImportReference(qq.ParameterType);
                        testFunction.Parameters.Add(new ParameterDefinition(qq.Name, qq.Attributes, parameterType));
                    }

                    // Copy Variable
                    foreach (VariableDefinition a in methodIL.Body.Variables)
                    {
                        TypeReference aaa1 = testFunction.Module.ImportReference(a.VariableType);
                        var tempVar = new VariableDefinition(aaa1);
                        testFunctionIL.Body.Variables.Add(tempVar);
                    }

                    Mono.Collections.Generic.Collection<Instruction> instss = methodIL.Body.Instructions;

                    // Copy try ~ catch
                    foreach (ExceptionHandler b in methodIL.Body.ExceptionHandlers)
                    {
                        // 이전 코드를 디컴파일 한다
                        Instruction tryStart = null;
                        Instruction tryEnd = null;
                        Instruction HandleStart = null;
                        Instruction HandlerEnd = null;

                        foreach (Instruction rr in testFunctionIL.Body.Instructions)
                        {
                            if (rr.Offset == b.TryStart.Offset)
                            {
                                tryStart = rr;
                            }
                            if (rr.Offset == b.TryEnd.Offset)
                            {
                                tryEnd = rr;
                            }

                            if (rr.Offset == b.HandlerStart.Offset)
                            {
                                HandleStart = rr;
                            }

                            if (rr.Offset == b.HandlerEnd.Offset)
                            {
                                HandlerEnd = rr;
                            }

                        }

                        TypeReference aaa1 = null;
                        if (b.CatchType != null)
                            aaa1 = testFunction.Module.ImportReference(b.CatchType);

                        ExceptionHandler handler = new ExceptionHandler(b.HandlerType)
                        {

                            TryStart = tryStart,
                            TryEnd = tryEnd,
                            HandlerStart = HandleStart,
                            HandlerEnd = HandlerEnd,
                            CatchType = aaa1,

                        };
                        testFunctionIL.Body.ExceptionHandlers.Add(handler);
                    }

                }
            }
            #endregion

            #region call redefinition
            foreach (MethodDefinition method in copyMethod)
            {
                if (method.Body != null)
                {
                    ILProcessor methodIL = method.Body.GetILProcessor();
                    foreach (Instruction inst in methodIL.Body.Instructions)
                    {
                        if (inst.OpCode == OpCodes.Call || inst.OpCode == OpCodes.Ldftn)
                        {
                            MethodReference operand = inst.Operand as MethodReference;
                            // is API ?
                            foreach (MethodDefinition findMethod2 in copyMethod)
                            {
                                bool isAPI = (findMethod2.Attributes & Mono.Cecil.MethodAttributes.PInvokeImpl) != 0;
                                if (isAPI)
                                {
                                    if (operand.Name == findMethod2.PInvokeInfo.EntryPoint)
                                    {
                                        inst.Operand = findMethod2;
                                        continue;
                                    }
                                }
                            }

                            // is Function?
                            MethodDefinition findMethod = copyMethod.Find(x => x.Name.Contains(operand.Name));
                            if (findMethod != null)
                            {
                                inst.Operand = findMethod;
                                continue;
                            }

                        }
                    }
                }
            } 
            #endregion

            // insert EntryPoint CALL test
            {
                MethodDefinition findMethodHookFunction = copyMethod.Find(x => x.Name.Contains("TEST"));
                ILProcessor entryIL = entryMethod.Body.GetILProcessor();
                Instruction inst = entryIL.Create(OpCodes.Call, findMethodHookFunction);
                entryIL.InsertBefore(entryMethod.Body.Instructions[0], inst);
            }

            assemblyToInject.SaveAssembly(savePath);
            tmpAssembly.assembly.Dispose();
            MessageBox.Show("SaveAssembly Successful");
        }
    }
}
