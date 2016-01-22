using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace Java.Interop {

	struct JavaVMInitArgs {
		public  JniVersion                      version;    /*				 use JNI_VERSION_1_2 or later */

		public  int                             nOptions;
		public  IntPtr /* JavaVMOption[] */     options;
		public  byte                            ignoreUnrecognized;
	}

	struct JavaVMOption {
		public  IntPtr /* const char* */    optionString;
		public  IntPtr /* void * */         extraInfo;
	}

	public class JreRuntimeOptions : JniRuntime.CreationOptions {

		internal    List<string>    Options = new List<string> ();

		public  JniVersion  JniVersion                  {get; set;}
		public  bool        IgnoreUnrecognizedOptions   {get; set;}

		public  Collection<string>  ClassPath           {get; private set;}

		public JreRuntimeOptions ()
		{
			JniVersion  = JniVersion.v1_2;
			ClassPath   = new Collection<string> () {
				Path.Combine (
					Path.GetDirectoryName (typeof (JreRuntimeOptions).Assembly.Location),
					"java-interop.jar"),
			};

			if (ValueManager == null && Type.GetType ("Mono.Runtime", throwOnError: false) != null)
				ValueManager    = new MonoRuntimeValueManager ();
		}

		public JreRuntimeOptions AddOption (string option)
		{
			Options.Add (option);
			return this;
		}

		public JreRuntimeOptions AddSystemProperty (string name, string value)
		{
			if (name == null)
				throw new ArgumentNullException ("name");
			if (value == null)
				throw new ArgumentNullException ("value");
			if (name == "java.class.path")
				throw new ArgumentException ("Do not use AddSystemProperty() for the 'java.class.path' property. Add to the ClassPath collection instead.", "name");
			Options.Add (string.Format ("-D{0}={1}", name, value));
			return this;
		}

		public JreRuntime CreateJreVM ()
		{
			return new JreRuntime (this);
		}
	}

	public class JreRuntime : JniRuntime
	{
		const string LibraryName = "jvm.dll";

		[DllImport (LibraryName)]
		static extern int JNI_CreateJavaVM (out IntPtr javavm, out IntPtr jnienv, ref JavaVMInitArgs args);

		static unsafe JreRuntimeOptions CreateJreVM (JreRuntimeOptions builder)
		{
			if (builder == null)
				throw new ArgumentNullException ("builder");

			if (builder.InvocationPointer != IntPtr.Zero)
				return builder;

			var args = new JavaVMInitArgs () {
				version             = builder.JniVersion,
				nOptions            = builder.Options.Count + 1,
				ignoreUnrecognized  = builder.IgnoreUnrecognizedOptions ? (byte) 1 : (byte) 0,
			};
			var options = new JavaVMOption [builder.Options.Count + 1];
			try {
				for (int i = 0; i < builder.Options.Count; ++i)
					options [i].optionString = Marshal.StringToHGlobalAnsi (builder.Options [i]);
				var classPath   = Marshal.StringToHGlobalAnsi (string.Format ("-Djava.class.path={0}", string.Join (Path.PathSeparator.ToString (), builder.ClassPath)));
				options [builder.Options.Count].optionString = classPath;
				fixed (JavaVMOption* popts = options) {
					args.options = (IntPtr) popts;
					IntPtr      javavm;
					IntPtr      jnienv;
					int r = JNI_CreateJavaVM (out javavm, out jnienv, ref args);
					if (r != 0) {
						var message = string.Format (
								"The JDK supports creating at most one JVM per process, ever; " +
								"do you have a JVM running already, or have you already created (and destroyed?) one? " +
								"(JNI_CreateJavaVM returned {0}).",
								r);
						throw new NotSupportedException (message);
					}
					builder.InvocationPointer            = javavm;
					builder.EnvironmentPointer   = jnienv;
					builder.DestroyRuntimeOnDispose  = true;
					return builder;
				}
			} finally {
				for (int i = 0; i < options.Length; ++i)
					Marshal.FreeHGlobal (options [i].optionString);
			}
		}

		internal protected JreRuntime (JreRuntimeOptions builder)
			: base (CreateJreVM (builder))
		{
		}

		public override string GetCurrentThreadDescription ()
		{
			return string.Format ("'{0}'({1})", Thread.CurrentThread.Name, Thread.CurrentThread.ManagedThreadId);
		}
	}
}
