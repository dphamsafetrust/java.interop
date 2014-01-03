using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JavaVMTest
	{
		[Test]
		public void CreateJavaVM ()
		{
			Assert.IsNotNull (JVM.Current.SafeHandle);
			Assert.IsNotNull (JniEnvironment.Current);
		}

		[Test]
		public void JDK_OnlySupportsOneVM ()
		{
			#pragma warning disable 0219
			var first = JVM.Current;
			#pragma warning restore 0219
			try {
				var second = new JavaVMBuilder ().CreateJavaVM ();
				// If we reach here, we're in a JVM that supports > 1 VM
				second.Dispose ();
				Assert.Ignore ();
			} catch (NotSupportedException) {
			} catch (Exception e){
				Assert.Fail ("Expected NotSupportedException; got: {0}", e);
			}
		}
	}
}

