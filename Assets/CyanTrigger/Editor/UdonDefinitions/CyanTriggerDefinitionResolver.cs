using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Cyan.CT.Editor
{
    public enum PrimitiveOperation
    {
        None = -1,
        Equality,
        Inequality,
        LogicalAnd,
        LogicalOr,
        LogicalXor,
        ConditionalAnd,
        ConditionalOr,
        ConditionalXor,
        UnaryNegation, // 1
        UnaryMinus, // 1
        Addition,
        Subtraction,
        Multiplication,
        Division,
        Remainder,
        LessThan,
        GreaterThan,
        LessThanOrEqual,
        GreaterThanOrEqual,
        LeftShift,
        RightShift,
    }

    public enum FieldOperation
    {
        None = -1,
        Get,
        Set,
    }

    public class CyanTriggerDefinitionResolver
    {
        public static string GetMethodSignature(MethodBase method)
        {
            if (method == null)
            {
                throw new Exception("Trying to get method signature for Null method");
            }
            
            StringBuilder sig = new StringBuilder();
            sig.Append(GetTypeSignature(method.ReflectedType));
            sig.Append(".__");
            sig.Append(GetMemberNameSanitized(method));
            
            List<Type> parameters = new List<Type>();

            foreach (var parameter in method.GetParameters())
            {
                parameters.Add(parameter.ParameterType);
            }

            if (parameters.Count > 0)
            {
                sig.Append("__");

                List<string> paramStrings = new List<string>();
                foreach (Type param in parameters)
                {
                    paramStrings.Add(GetTypeSignature(param));
                }

                sig.Append(string.Join("_", paramStrings));
            }

            Type returnType = null;

            if (method is MethodInfo methodInfo)
            {
                returnType = methodInfo.ReturnType;
            }
            else if (method is ConstructorInfo constructorInfo)
            {
                // Lolwut
                if (parameters.Count == 0)
                {
                    sig.Append("__");
                }
                returnType = constructorInfo.ReflectedType;
            }

            if (returnType != null || returnType != typeof(void))
            {
                sig.Append("__");
                sig.Append(GetTypeSignature(returnType));
            }

            return sig.ToString();
        }

        public static string GetFieldSignature(FieldInfo fieldInfo, FieldOperation fieldOperation)
        {
            string fieldName = GetMemberNameSanitized(fieldInfo);
            StringBuilder sig = new StringBuilder();
            sig.Append(GetTypeSignature(fieldInfo.ReflectedType));
            sig.Append(".__");

            string fieldType = GetTypeSignature(fieldInfo.FieldType);

            if (fieldOperation == FieldOperation.Set)
            {
                sig.Append("set_");
            }
            else
            {
                sig.Append("get_");
            }

            sig.Append(fieldName);
            sig.Append("__");
            sig.Append(fieldType);

            return sig.ToString();
        }

        public static string GetPrimitiveOperationSignature(Type type, PrimitiveOperation primitiveOperation)
        {
            string typeSig = GetTypeSignature(type);
            StringBuilder sig = new StringBuilder();
            sig.Append(typeSig);
            sig.Append(".__op_");
            sig.Append(primitiveOperation);

            int inputCount =
                primitiveOperation == PrimitiveOperation.UnaryMinus || primitiveOperation == PrimitiveOperation.UnaryNegation
                ? 1
                : 2;

            List<string> inputs = new List<string>();
            for(int i = 0; i < inputCount; ++i)
            {
                if (
                    i == 1 && 
                    (primitiveOperation == PrimitiveOperation.LeftShift || primitiveOperation == PrimitiveOperation.RightShift) &&
                    (type == typeof(uint) || type == typeof(long) || type == typeof(ulong))
                ) {
                    inputs.Add(GetTypeSignature(typeof(int)));
                }
                else
                {
                    inputs.Add(typeSig);
                }
            }

            if (inputs.Count > 0)
            {
                sig.Append("__");
                sig.Append(string.Join("_", inputs));
            }

            Type returnType = null;
            switch (primitiveOperation)
            {
                case PrimitiveOperation.Equality:
                case PrimitiveOperation.Inequality:
                case PrimitiveOperation.LessThan:
                case PrimitiveOperation.LessThanOrEqual:
                case PrimitiveOperation.GreaterThan:
                case PrimitiveOperation.GreaterThanOrEqual:
                    returnType = typeof(bool);
                    break;
                default:
                    returnType = PrimitiveOperationCastUp(type);
                    break;
            }
            if (primitiveOperation == PrimitiveOperation.UnaryMinus && type == typeof(uint))
            {
                returnType = typeof(long);
            }

            sig.Append("__");
            sig.Append(GetTypeSignature(returnType));

            return sig.ToString();
        }

        private static Type PrimitiveOperationCastUp(Type type)
        {
            if (
                type == typeof(byte) ||
                type == typeof(sbyte) ||
                type == typeof(char) ||
                type == typeof(short) ||
                type == typeof(ushort)
            )
            {
                return typeof(int);
            }
            return type;
        }

        public static string GetMemberNameSanitized(MemberInfo memberInfo)
        {
            return memberInfo.Name
                .Replace(".", "")
                ;
        }

        public static string GetTypeSignature(Type t)
        {
            return t.ToString()
                .Replace(".", "")
                .Replace(",", "")
                .Replace("+", "")
                .Replace("[]", "Array")
                .Replace("&", "Ref")
                .Replace("`1[", "")
                .Replace("`2[", "")
                .Replace("]", "")
                .Replace("SystemCollectionsGenericIEnumerableT", "IEnumerableT")
                .Replace("SystemCollectionsGenericListT", "ListT")
                .Replace("VRCUdonUdonBehaviour", "VRCUdonCommonInterfacesIUdonEventReceiver")
                ;
        }
    }
}