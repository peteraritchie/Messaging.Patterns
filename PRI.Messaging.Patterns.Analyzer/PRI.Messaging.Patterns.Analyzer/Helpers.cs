using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using PRI.Messaging.Patterns.Extensions.Bus;
using PRI.Messaging.Primitives;

namespace PRI.Messaging.Patterns.Analyzer
{
	internal static class Helpers
	{
		public static MethodInfo GetMethodInfo(IMethodSymbol methodSymbolInfo, Type type, string methodName)
		{
			var mis = type.GetRuntimeMethods().Where(e => e.Name == methodName);
			var matchingMethod =
				mis.SingleOrDefault(methodSymbolInfo.IsSymbolOf);
			return matchingMethod;
		}

		public static MethodInfo GetRequestAsyncInvocationMethodInfo(IMethodSymbol methodSymbolInfo)
		{
			return GetMethodInfo(methodSymbolInfo, typeof(BusExtensions), nameof(BusExtensions.RequestAsync));
		}

		public static MethodInfo GetSendInvocationMethodInfo(IMethodSymbol methodSymbolInfo)
		{
			return GetMethodInfo(methodSymbolInfo, typeof(BusExtensions), nameof(BusExtensions.Send));
		}


		public static MethodInfo GetHandleInvocationMethodInfo(IMethodSymbol methodSymbolInfo)
		{
			return GetMethodInfo(methodSymbolInfo, typeof(IConsumer<>), nameof(IConsumer<IMessage>.Handle));
		}

		public static MethodInfo GetPublishInvocationMethodInfo(IMethodSymbol methodSymbolInfo)
		{
			return GetMethodInfo(methodSymbolInfo, typeof(IConsumer<>), nameof(BusExtensions.Publish));
		}

		public static string SquareToAngleBrackets(string text)
		{
			var result = text.Replace('[', '<');
			result = result.Replace("`1", "");
			result = result.Replace("`2", "");
			result = result.Replace("`3", "");
			result = result.Replace(", ", ",");
			return result.Replace(']', '>');
		}

		public static string GetSignature(IMethodSymbol methodSymbol)
		{
			if (methodSymbol.ReducedFrom != null)
			{
				methodSymbol = methodSymbol.ReducedFrom;
			}
			return $"{methodSymbol.ReturnType} {methodSymbol}".Replace(", ", ",");
		}

		public static string GetCSharpAliasName(Type type)
		{
			return Aliases.ContainsKey(type) ? Aliases[type] : type.ToString();
		}

		private static readonly Dictionary<Type, string> Aliases = new Dictionary<Type, string>
		{
			// ReSharper disable StringLiteralTypo
			{typeof(byte), "byte"},
			{typeof(sbyte), "sbyte"},
			{typeof(short), "short"},
			{typeof(ushort), "ushort"},
			{typeof(int), "int"},
			{typeof(uint), "uint"},
			{typeof(long), "long"},
			{typeof(ulong), "ulong"},
			{typeof(float), "float"},
			{typeof(double), "double"},
			{typeof(decimal), "decimal"},
			{typeof(object), "object"},
			{typeof(bool), "bool"},
			{typeof(char), "char"},
			{typeof(string), "string"},
			{typeof(void), "void"},
			{typeof(byte?), "byte?"},
			{typeof(sbyte?), "sbyte?"},
			{typeof(short?), "short?"},
			{typeof(ushort?), "ushort?"},
			{typeof(int?), "int?"},
			{typeof(uint?), "uint?"},
			{typeof(long?), "long?"},
			{typeof(ulong?), "ulong?"},
			{typeof(float?), "float?"},
			{typeof(double?), "double?"},
			{typeof(decimal?), "decimal?"},
			{typeof(bool?), "bool?"},
			{typeof(char?), "char?"}
			// ReSharper restore StringLiteralTypo
		};
	}
}