using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ILCodeInjector
{
    public class AssemblyHandler
    {
        public AssemblyDefinition assembly;

        /* READ AND WRITE ASSEMBLY ***************************************************************/
        public bool LoadAssembly(String fileName)
        {
            try
            {
                assembly = AssemblyDefinition.ReadAssembly(fileName);
                return true;
            }
            catch
            {
                MessageBox.Show("Could not read Assembly, probably non .net file or unreadable obfuscation.");
                return false;
            }
        }

        public void SaveAssembly(String outputFileName)
        {
            try
            {
                assembly.Write(outputFileName);
                assembly.Dispose();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
                System.IO.File.Delete(outputFileName);
            }
        }


        /* GETTERS *******************************************************************************/
        public String GetRuntime()
        {
            return assembly.MainModule.Runtime.ToString();
        }

        public AssemblyDefinition GetAssembly()
        {

            return assembly;
        }

        public MethodDefinition GetEntryPoint()
        {
            return assembly.EntryPoint;
        }

        /* DECOMPILERS **************************************************************************/
        public String DecompileMethod(String typeName, String methodName)
        {
            MethodDefinition method = FindMethodInAssembly(typeName, methodName);
            if (method.IsSetter || method.IsGetter || method.Body == null)
                return null;

            String text = "";
            ILProcessor cilWorker = method.Body.GetILProcessor();
            foreach (Instruction ins in cilWorker.Body.Instructions)
            {
                text += ins + Environment.NewLine;
            }

            return text;
        }

        public Instruction[] DecompileMethodAsInstructions(String typeName, String methodName, AssemblyDefinition dstAssembly)
        {
            MethodDefinition srcMethod = FindMethodInAssembly(typeName, methodName);
            if (srcMethod.IsSetter || srcMethod.IsGetter || srcMethod.Body == null)
                return null;

            List<Instruction> dstInstructions = new List<Instruction>();

            foreach (Instruction srcInstruction in srcMethod.Body.Instructions)
            {
                object operand = srcInstruction.Operand;

                if (operand is FieldReference)
                {
                    FieldReference mref = operand as FieldReference;
                    FieldReference newf = dstAssembly.MainModule.ImportReference(mref);
                    dstInstructions.Add(Instruction.Create(srcInstruction.OpCode, newf));
                    continue;
                }
                if (operand is TypeReference)
                {
                    TypeReference mref = operand as TypeReference;
                    TypeReference newf = dstAssembly.MainModule.ImportReference(mref);
                    dstInstructions.Add(Instruction.Create(srcInstruction.OpCode, newf));
                    continue;
                }



                if (operand is MethodReference)
                {
                    MethodReference mref = operand as MethodReference;
                    MethodReference newf = dstAssembly.MainModule.ImportReference(mref);
                    dstInstructions.Add(Instruction.Create(srcInstruction.OpCode, newf));
                    continue;
                }


                dstInstructions.Add(srcInstruction);
            }

            // remove last instruction which is ret
            //dstInstructions.RemoveAt(dstInstructions.Count - 1);

            // reverse the list so that we can inject each instruction using InsertBefore
            dstInstructions.Reverse();

            return dstInstructions.ToArray();
        }
        //MethodDefinition CopyMethod(MethodDefinition templateMethod)
        //{
        //    var returnType = Resolve(templateMethod.ReturnType);
        //    var newMethod = new MethodDefinition(templateMethod.Name, templateMethod.Attributes, returnType)
        //    {
        //        IsPInvokeImpl = templateMethod.IsPInvokeImpl,
        //        IsPreserveSig = templateMethod.IsPreserveSig,
        //    };
        //    if (templateMethod.IsPInvokeImpl)
        //    {
        //        var moduleRef = new ModuleReference(templateMethod.PInvokeInfo.Module.Name);
        //        moduleReader.Module.ModuleReferences.Add(moduleRef);
        //        newMethod.PInvokeInfo = new PInvokeInfo(templateMethod.PInvokeInfo.Attributes, templateMethod.PInvokeInfo.EntryPoint, moduleRef);
        //    }


        //    if (templateMethod.Body != null)
        //    {
        //        newMethod.Body.InitLocals = templateMethod.Body.InitLocals;
        //        foreach (var variableDefinition in templateMethod.Body.Variables)
        //        {
        //            newMethod.Body.Variables.Add(new VariableDefinition(Resolve(variableDefinition.VariableType)));
        //        }
        //        CopyInstructions(templateMethod, newMethod);
        //        CopyExceptionHandlers(templateMethod, newMethod);

        //    }
        //    foreach (var parameterDefinition in templateMethod.Parameters)
        //    {
        //        newMethod.Parameters.Add(new ParameterDefinition(Resolve(parameterDefinition.ParameterType)));
        //    }


        //    targetType.Methods.Add(newMethod);
        //    return newMethod;
        //}


        /* INJECTION ****************************************************************************/
        private void InjectInstructions(MethodDefinition methodDefinition, Instruction[] instructions)
        {
            ILProcessor cilWorker = methodDefinition.Body.GetILProcessor();

            foreach (Instruction instruction in instructions)
            {
                //Debug.WriteLine(instruction.ToString());

                if (methodDefinition.Body.Instructions.Count == 0)
                {
                    cilWorker.Append(instruction);
                }
                else
                {
                    cilWorker.InsertBefore(methodDefinition.Body.Instructions[0], instruction);
                }


            }


        }

        public void CopyMethodToAssembly2(AssemblyHandler assemblyHandlerToInject,
            String typeNameToInject, String methodNameToInject,
            String typeNameToCopy, String methodNameToCopy)
        {



            AssemblyDefinition assemblyToInject = assemblyHandlerToInject.GetAssembly();
            MethodDefinition methodToInject = assemblyHandlerToInject.FindMethodInAssembly(typeNameToInject, methodNameToInject);

            TypeDefinition tt = FindTypeInAssembly(typeNameToInject);

        }


        public void CopyMethodToAssembly(AssemblyHandler assemblyHandlerToInject, String typeNameToInject, String methodNameToInject,
                                         String typeNameToCopy, String methodNameToCopy)
        {
            AssemblyDefinition assemblyToInject = assemblyHandlerToInject.GetAssembly();
            Instruction[] instructions = this.DecompileMethodAsInstructions(typeNameToCopy, methodNameToCopy, assemblyToInject);
            MethodDefinition methodToInject = assemblyHandlerToInject.FindMethodInAssembly(typeNameToInject, methodNameToInject);

            /* inject the instructions in the main assembly */
            InjectInstructions(methodToInject, instructions);
        }


        public MethodDefinition GetAPI(String typeName, String methodName)
        {

            MethodDefinition srcMethod = FindMethodInAssembly(typeName, methodName);
            return srcMethod;

        }

        public MethodDefinition addAPI(String typeName)
        {
            AssemblyDefinition methodDefinition = GetAssembly();
            TypeDefinition tt = FindTypeInAssembly(typeName);

            TypeReference stringType = assembly.MainModule.ImportReference(typeof(String));
            TypeReference nativeIntType = assembly.MainModule.ImportReference(typeof(UInt32));
            TypeReference nativeIntType2 = assembly.MainModule.ImportReference(typeof(IntPtr));

            ModuleReference kernel32Ref = new ModuleReference("kernel32");

            assembly.MainModule.ModuleReferences.Add(kernel32Ref);

            MethodDefinition loadLibraryA = new MethodDefinition("TESTTEST", Mono.Cecil.MethodAttributes.Public |
                        Mono.Cecil.MethodAttributes.HideBySig | Mono.Cecil.MethodAttributes.Static |
                        Mono.Cecil.MethodAttributes.PInvokeImpl, nativeIntType2);

            loadLibraryA.IsPreserveSig = true;
            //loadLibraryA.PInvokeInfo = new PInvokeInfo(PInvokeAttributes.NoMangle | PInvokeAttributes.CharSetAnsi
            //    | PInvokeAttributes.SupportsLastError | PInvokeAttributes.CallConvWinapi, "LoadLibraryA", kernel32Ref);

            loadLibraryA.PInvokeInfo = new PInvokeInfo(PInvokeAttributes.CallConvWinapi, "LoadLibraryA", kernel32Ref);
            loadLibraryA.Parameters.Add(new ParameterDefinition("name", Mono.Cecil.ParameterAttributes.None, stringType));

            tt.Methods.Add(loadLibraryA);

            return loadLibraryA;
        }



        public void addAPI(String typeName, String methodName, MethodDefinition apiName, Instruction[] inst)
        {
            AssemblyDefinition methodDefinition = GetAssembly();
            TypeDefinition tt = FindTypeInAssembly(typeName);

            TypeReference stringType = assembly.MainModule.ImportReference(typeof(String));
            TypeReference nativeIntType = assembly.MainModule.ImportReference(typeof(UInt32));
            TypeReference voidType = assembly.MainModule.ImportReference(typeof(void));
            TypeReference aa = assembly.MainModule.ImportReference(typeof(System.Windows.Forms.DialogResult));

            ModuleReference kernel32Ref = new ModuleReference("kernel32");

            assembly.MainModule.ModuleReferences.Add(kernel32Ref);
            MethodDefinition loadLibraryA = new MethodDefinition("LoadLibraryA", Mono.Cecil.MethodAttributes.Public |
                        Mono.Cecil.MethodAttributes.HideBySig | Mono.Cecil.MethodAttributes.Static |
                        Mono.Cecil.MethodAttributes.PInvokeImpl, nativeIntType);
            loadLibraryA.IsPreserveSig = true;
            loadLibraryA.PInvokeInfo = new PInvokeInfo(PInvokeAttributes.NoMangle | PInvokeAttributes.CharSetAnsi
                | PInvokeAttributes.SupportsLastError | PInvokeAttributes.CallConvWinapi, "LoadLibraryA", kernel32Ref);

            loadLibraryA.Parameters.Add(new ParameterDefinition("name", Mono.Cecil.ParameterAttributes.None, stringType));


            AssemblyDefinition assemblyToInject = this.GetAssembly();

            MethodDefinition testFunction = new MethodDefinition("Fun",
                Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.HideBySig |
                Mono.Cecil.MethodAttributes.Static | MethodAttributes.CompilerControlled, voidType);

            //MethodDefinition md = new MethodDefinition("Decrypt",
            //    MethodAttributes.HideBySig | MethodAttributes.Static | MethodAttributes.CompilerControlled, definition.Import(typeof(string)));


            ILProcessor cilWorker = testFunction.Body.GetILProcessor();

            cilWorker.Append(inst[0]);

            foreach (Instruction instruction in inst)
            {
                cilWorker.InsertBefore(testFunction.Body.Instructions[0], instruction);
            }



            tt.Methods.Add(testFunction);


        }


        /* METHOD HELPERS ***********************************************************************/
        public void ReadMethodsToTree(TreeView tree)
        {
            tree.Nodes.Clear();

            foreach (TypeDefinition type in assembly.MainModule.Types)
            {
                TreeNode node = tree.Nodes.Add(type.Name.ToString());

                foreach (MethodDefinition method in type.Methods)
                {
                    node.Nodes.Add(method.Name.ToString());
                }
            }
        }

        /**
         * Check if the method selected by the user is valid, e.g. it's found in the assmebly
         * and it has a body
         */
        public Boolean CheckIfMethodIsInjectable(String typeName, String methodName)
        {
            if (typeName == null || methodName == null)
                return false;

            foreach (TypeDefinition type in assembly.MainModule.Types)
            {
                if (type.Name != typeName)
                    continue;

                foreach (MethodDefinition method in type.Methods)
                    if (method.Name == methodName && method.Body != null)
                        return true;
            }
            return false;
        }

        public MethodDefinition FindMethodInAssembly(String typeName, String methodName)
        {
            if (typeName == null || methodName == null)
                return null;

            foreach (TypeDefinition type in assembly.MainModule.Types)
            {
                if (type.Name != typeName)
                {
                    continue;
                }

                foreach (MethodDefinition method in type.Methods)
                {
                    if (method.Name == methodName)
                        return method;
                }
            }
            return null;
        }

        public TypeDefinition FindTypeInAssembly(String typeName)
        {
            if (typeName == null)
                return null;

            foreach (TypeDefinition type in assembly.MainModule.Types)
            {
                if (type.Name == typeName)
                {
                    return type;
                }
            }
            return null;
        }

    }
}
