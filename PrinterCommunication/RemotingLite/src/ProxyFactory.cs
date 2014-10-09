/*************************************************************************************************
 * RemotingLite
 * ------
 * A light framework for making remote method invocations using TCP/IP. It is based loosely on
 * Windows Communication Foundation, and is meant to provide programmers with the same API
 * regardless of whether they write software for the Microsoft .NET platform or the Mono .NET
 * platform.
 * Consult the documentation and example applications for information about how to use this API.
 * 
 * Author       : Frank Thomsen
 * http         : http://sector0.dk
 * Concact      : http://sector0.dk/?q=contact
 * Information  : http://sector0.dk/?q=node/27
 * Licence      : Free. If you use this, please let me know.
 * 
 *          Please feel free to contact me with ideas, bugs or improvements.
 *************************************************************************************************/

using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Reflection.Emit;
using System.Reflection;
using System.Threading;

namespace RemotingLite
{
    public sealed class ProxyFactory
    {
        public static TInterface CreateProxy<TInterface>(IPEndPoint endpoint) where TInterface : class
        {
            AppDomain domain = Thread.GetDomain();
            // create a new assembly for the proxy
            AssemblyBuilder assemblyBuilder = domain.DefineDynamicAssembly(new AssemblyName("ProxyAssembly"), AssemblyBuilderAccess.Run);

            // create a new module for the proxy
            ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("ProxyModule", true);

            // Set the class to be public and sealed
            TypeAttributes typeAttributes = TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Sealed;

            // Construct the type builder
            TypeBuilder typeBuilder = moduleBuilder.DefineType(typeof(TInterface).Name + "Proxy", typeAttributes, typeof(Channel));
            List<Type> allInterfaces = new List<Type>(typeof(TInterface).GetInterfaces());
            allInterfaces.Add(typeof(TInterface));

            //add the interface
            typeBuilder.AddInterfaceImplementation(typeof(TInterface));

            //construct the constructor
            Type[] ctorArgTypes = new Type[] { typeof(IPEndPoint) };
            CreateConstructor(typeBuilder, ctorArgTypes);

            //construct the type maps
            Dictionary<Type, OpCode> ldindOpCodeTypeMap = new Dictionary<Type, OpCode>();
            ldindOpCodeTypeMap.Add(typeof(Boolean), OpCodes.Ldind_I1);
            ldindOpCodeTypeMap.Add(typeof(Byte), OpCodes.Ldind_U1);
            ldindOpCodeTypeMap.Add(typeof(SByte), OpCodes.Ldind_I1);
            ldindOpCodeTypeMap.Add(typeof(Int16), OpCodes.Ldind_I2);
            ldindOpCodeTypeMap.Add(typeof(UInt16), OpCodes.Ldind_U2);
            ldindOpCodeTypeMap.Add(typeof(Int32), OpCodes.Ldind_I4);
            ldindOpCodeTypeMap.Add(typeof(UInt32), OpCodes.Ldind_U4);
            ldindOpCodeTypeMap.Add(typeof(Int64), OpCodes.Ldind_I8);
            ldindOpCodeTypeMap.Add(typeof(UInt64), OpCodes.Ldind_I8);
            ldindOpCodeTypeMap.Add(typeof(Char), OpCodes.Ldind_U2);
            ldindOpCodeTypeMap.Add(typeof(Double), OpCodes.Ldind_R8);
            ldindOpCodeTypeMap.Add(typeof(Single), OpCodes.Ldind_R4);
            Dictionary<Type, OpCode> stindOpCodeTypeMap = new Dictionary<Type, OpCode>();
            stindOpCodeTypeMap.Add(typeof(Boolean), OpCodes.Stind_I1);
            stindOpCodeTypeMap.Add(typeof(Byte), OpCodes.Stind_I1);
            stindOpCodeTypeMap.Add(typeof(SByte), OpCodes.Stind_I1);
            stindOpCodeTypeMap.Add(typeof(Int16), OpCodes.Stind_I2);
            stindOpCodeTypeMap.Add(typeof(UInt16), OpCodes.Stind_I2);
            stindOpCodeTypeMap.Add(typeof(Int32), OpCodes.Stind_I4);
            stindOpCodeTypeMap.Add(typeof(UInt32), OpCodes.Stind_I4);
            stindOpCodeTypeMap.Add(typeof(Int64), OpCodes.Stind_I8);
            stindOpCodeTypeMap.Add(typeof(UInt64), OpCodes.Stind_I8);
            stindOpCodeTypeMap.Add(typeof(Char), OpCodes.Stind_I2);
            stindOpCodeTypeMap.Add(typeof(Double), OpCodes.Stind_R8);
            stindOpCodeTypeMap.Add(typeof(Single), OpCodes.Stind_R4);

            //construct the method builders from the method infos defined in the interface
            List<MethodInfo> methods = GetAllMethods(allInterfaces);
            foreach (MethodInfo methodInfo in methods)
            {
                MethodBuilder methodBuilder = ConstructMethod(methodInfo, typeBuilder, ldindOpCodeTypeMap, stindOpCodeTypeMap);
                typeBuilder.DefineMethodOverride(methodBuilder, methodInfo);
            }

            //create the type and construct an instance
            Type t = typeBuilder.CreateType();
            TInterface instance = (TInterface)t.GetConstructor(ctorArgTypes).Invoke(new object[] { endpoint });

            return instance;
        }

        private static List<MethodInfo> GetAllMethods(List<Type> allInterfaces)
        {
            List<MethodInfo> methods = new List<MethodInfo>();
            foreach (Type interfaceType in allInterfaces)
                methods.AddRange(interfaceType.GetMethods());
            return methods;
        }

        private static void CreateConstructor(TypeBuilder typeBuilder, Type[] ctorArgTypes)
        {
            ConstructorBuilder ctor = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.HasThis, ctorArgTypes);
            ConstructorInfo baseCtor = typeof(Channel).GetConstructor(ctorArgTypes);

            ILGenerator ctorIL = ctor.GetILGenerator();
            ctorIL.Emit(OpCodes.Ldarg_0); //load "this"
            ctorIL.Emit(OpCodes.Ldarg_1); //load "endpoint"
            ctorIL.Emit(OpCodes.Call, baseCtor); //call "base(...)"
            ctorIL.Emit(OpCodes.Ret);
        }

        private static MethodBuilder ConstructMethod(MethodInfo methodInfo, TypeBuilder typeBuilder, Dictionary<Type, OpCode> ldindOpCodeTypeMap, Dictionary<Type, OpCode> stindOpCodeTypeMap)
        {
            ParameterInfo[] paramInfos = methodInfo.GetParameters();
            int nofParams = paramInfos.Length;
            Type[] parameterTypes = new Type[nofParams];
            for (int i = 0; i < nofParams; i++)
                parameterTypes[i] = paramInfos[i].ParameterType;
            Type returnType = methodInfo.ReturnType;
            MethodBuilder methodBuilder = typeBuilder.DefineMethod(methodInfo.Name, MethodAttributes.Public | MethodAttributes.Virtual, returnType, parameterTypes);

            ILGenerator mIL = methodBuilder.GetILGenerator();
            GenerateILCodeForMethod(mIL, parameterTypes, methodBuilder.ReturnType, ldindOpCodeTypeMap, stindOpCodeTypeMap);
            return methodBuilder;
        }

        private static void GenerateILCodeForMethod(ILGenerator mIL, Type[] inputArgTypes, Type returnType, Dictionary<Type, OpCode> ldindOpCodeTypeMap, Dictionary<Type, OpCode> stindOpCodeTypeMap)
        {
            int nofArgs = inputArgTypes.Length;
            //get the MethodInfo for InvokeMethod
            MethodInfo invokeMethodMI = typeof(Channel).GetMethod("InvokeMethod", BindingFlags.Instance | BindingFlags.NonPublic);
            //declare local variables
            LocalBuilder resultLB = mIL.DeclareLocal(typeof(object[])); // object[] result

            mIL.Emit(OpCodes.Ldarg_0); //load "this"
            mIL.Emit(OpCodes.Ldc_I4, nofArgs); //push the number of arguments
            mIL.Emit(OpCodes.Newarr, typeof(object)); //create an array of objects

            //store every input argument in the args array
            for (int i = 0; i < nofArgs; i++)
            {
                Type inputType = inputArgTypes[i].IsByRef ? inputArgTypes[i].GetElementType() : inputArgTypes[i];

                mIL.Emit(OpCodes.Dup);
                mIL.Emit(OpCodes.Ldc_I4, i); //push the index onto the stack
                mIL.Emit(OpCodes.Ldarg, i + 1); //load the i'th argument. This might be an address			
                if (inputArgTypes[i].IsByRef)
                {
                    if (inputType.IsValueType)
                    {
                        mIL.Emit(ldindOpCodeTypeMap[inputType]);
                        mIL.Emit(OpCodes.Box, inputType);
                    }
                    else
                        mIL.Emit(OpCodes.Ldind_Ref);
                }
                else
                {
                    if (inputArgTypes[i].IsValueType)
                        mIL.Emit(OpCodes.Box, inputArgTypes[i]);
                }
                mIL.Emit(OpCodes.Stelem_Ref); //store the reference in the args array
            }
            mIL.Emit(OpCodes.Call, invokeMethodMI);
            mIL.Emit(OpCodes.Stloc, resultLB.LocalIndex); //store the result
            //store the results in the arguments
            for (int i = 0; i < nofArgs; i++)
            {
                if (inputArgTypes[i].IsByRef)
                {
                    Type inputType = inputArgTypes[i].GetElementType();
                    mIL.Emit(OpCodes.Ldarg, i + 1); //load the address of the argument
                    mIL.Emit(OpCodes.Ldloc, resultLB.LocalIndex); //load the result array
                    mIL.Emit(OpCodes.Ldc_I4, i + 1); //load the index into the result array
                    mIL.Emit(OpCodes.Ldelem_Ref); //load the value in the index of the array
                    if (inputType.IsValueType)
                    {
                        mIL.Emit(OpCodes.Unbox, inputArgTypes[i].GetElementType());
                        mIL.Emit(ldindOpCodeTypeMap[inputArgTypes[i].GetElementType()]);
                        mIL.Emit(stindOpCodeTypeMap[inputArgTypes[i].GetElementType()]);
                    }
                    else
                    {
                        mIL.Emit(OpCodes.Castclass, inputArgTypes[i].GetElementType());
                        mIL.Emit(OpCodes.Stind_Ref); //store the unboxed value at the argument address
                    }
                }
            }
            if (returnType != typeof(void))
            {
                mIL.Emit(OpCodes.Ldloc, resultLB.LocalIndex); //load the result array
                mIL.Emit(OpCodes.Ldc_I4, 0); //load the index of the return value. Alway 0
                mIL.Emit(OpCodes.Ldelem_Ref); //load the value in the index of the array

                if (returnType.IsValueType)
                {
                    mIL.Emit(OpCodes.Unbox, returnType); //unbox it
                    mIL.Emit(ldindOpCodeTypeMap[returnType]);
                }
                else
                    mIL.Emit(OpCodes.Castclass, returnType);
            }
            mIL.Emit(OpCodes.Ret);
        }
    }
}
