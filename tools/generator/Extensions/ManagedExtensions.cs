﻿using Java.Interop.Tools.TypeNameMappings;
using Mono.Cecil;
using System.Collections.Generic;
using System.Linq;

namespace MonoDroid.Generation
{
#if HAVE_CECIL
	internal static class ManagedExtensions
	{
		public static string FullNameCorrected (this TypeReference t) => t.FullName.Replace ('/', '.');

		public static GenericParameterDefinitionList GenericArguments (this MethodDefinition m) => 
			m.HasGenericParameters ? GenericParameterDefinitionList.FromMetadata (m.GenericParameters) : null;

		public static string Deprecated (this MethodDefinition m)
		{
			var v = m.CustomAttributes.FirstOrDefault (a => a.AttributeType.FullName == "System.ObsoleteAttribute");
			return v != null ? (string)v.ConstructorArguments [0].Value ?? "deprecated" : null;
		}

		public static string Visibility (this MethodDefinition m) =>
			m.IsPublic ? "public" : m.IsFamilyOrAssembly ? "protected internal" : m.IsFamily ? "protected" : m.IsAssembly ? "internal" : "private";

		public static IEnumerable<Parameter> GetParameters (this MethodDefinition m, CustomAttribute regatt)
		{
			var jnisig = (string)(regatt.ConstructorArguments.Count > 1 ? regatt.ConstructorArguments [1].Value : regatt.Properties.First (p => p.Name == "JniSignature").Argument.Value);
			var types = jnisig == null ? null : JavaNativeTypeManager.FromSignature (jnisig);
			var e = types?.GetEnumerator ();

			foreach (var p in m.Parameters) {
				if (e != null && !e.MoveNext ())
					e = null;
				// Here we do some tricky thing:
				// Both java.io.InputStream and java.io.OutputStream could be mapped to
				// System.IO.Stream. And when there is Stream in parameters, we have to
				// determine which direction of the Stream it was - in or out.
				// To do that, we inspect JNI Signature to handle that.
				//
				// We could *always* use this JNI information, *IF* there were no
				// int->enum conversion. Sadly this is not true, we still have to expect
				// custom enum types and cannot simply use JNI signature here.
				var rawtype = e?.Current.Type;
				var type = p.ParameterType.FullName == "System.IO.Stream" && e != null ? e.Current.Type : null;
				yield return CecilApiImporter.CreateParameter (p, type, rawtype);
			}
		}

		public static string StripArity (this string type)
		{
			if (string.IsNullOrWhiteSpace (type))
				return type;

			int tick_index;

			// Need to loop for things like List`1<List`1<string>>
			while ((tick_index = type.IndexOf ('`')) >= 0) {
				var less_than_index = type.IndexOf ('<', tick_index);

				if (less_than_index == -1)
					return type;

				type = type.Substring (0, tick_index) + type.Substring (less_than_index);
			}

			return type;
		}

		// Convert a fully qualified type like "System.Void" to the primitive type "void" if applicable
		public static string FilterPrimitive (this string type)
		{
			return type switch {
				"System.Boolean" => "bool",
				"System.Char" => "char",
				"System.Byte" => "byte",
				"System.SByte" => "byte",
				"System.Int16" => "short",
				"System.Int32" => "int",
				"System.Int64" => "long",
				"System.Single" => "float",
				"System.Double" => "double",
				"System.Void" => "void",
				"System.String" => "string",
				_ => type
			};
		}
	}
#endif
}
