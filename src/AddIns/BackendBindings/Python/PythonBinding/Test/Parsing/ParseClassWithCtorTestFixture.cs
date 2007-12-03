﻿// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Matthew Ward" email="mrward@users.sourceforge.net"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Collections.Generic;
using ICSharpCode.PythonBinding;
using ICSharpCode.SharpDevelop.DefaultEditor.Gui.Editor;
using ICSharpCode.SharpDevelop.Dom;
using ICSharpCode.TextEditor.Document;
using NUnit.Framework;
using PythonBinding.Tests;

namespace PythonBinding.Tests.Parsing
{
	/// <summary>
	/// Having a newline at the end of the class's last method was 
	/// causing the "pass" statement to be truncated when the constructor
	/// was folded. This test fixture tests for that bug.
	/// </summary>
	[TestFixture]
	public class ParseClassWithCtorTestFixture
	{
		ICompilationUnit compilationUnit;
		IClass c;
		IMethod method;
		FoldMarker methodMarker;
		FoldMarker classMarker;
		
		[TestFixtureSetUp]
		public void SetUpFixture()
		{
			string python = "class Test:\r\n" +
							"\tdef __init__(self):\r\n" +
							"\t\tpass\r\n";
			
			DefaultProjectContent projectContent = new DefaultProjectContent();
			PythonParser parser = new PythonParser();
			compilationUnit = parser.Parse(projectContent, @"C:\test.py", python);			
			if (compilationUnit.Classes.Count > 0) {
				c = compilationUnit.Classes[0];
				if (c.Methods.Count > 0) {
					method = c.Methods[0];
				}
				
				// Get folds.
				ParserFoldingStrategy foldingStrategy = new ParserFoldingStrategy();
				ParseInformation parseInfo = new ParseInformation();
				parseInfo.ValidCompilationUnit = compilationUnit;
			
				DocumentFactory docFactory = new DocumentFactory();
				IDocument doc = docFactory.CreateDocument();
				doc.TextContent = python;
				List<FoldMarker> markers = foldingStrategy.GenerateFoldMarkers(doc, @"C:\Temp\test.py", parseInfo);
			
				if (markers.Count > 1) {
					classMarker = markers[0];
					methodMarker = markers[1];
				}
			}
		}
		
		/// <summary>
		/// This tests fails because IronPython returns the correct
		/// end column for the method body in this case. In general
		/// it gets it wrong for class and method bodies by being
		/// one character too long.
		/// </summary>
		[Test]
		[Ignore("Method body region returned is off by one due to bug in IronPython.")]
		public void MethodBodyRegion()
		{
			int startLine = 2;
			int startColumn = 21;
			int endLine = 3;
			int endColumn = 7;
			DomRegion region = new DomRegion(startLine, startColumn, endLine, endColumn);
			Assert.AreEqual(region.ToString(), method.BodyRegion.ToString());
		}
		
		[Test]
		[Ignore]
		public void MethodFoldMarkerInnerText()
		{
			Assert.AreEqual("\r\n\t\tpass", methodMarker.InnerText);
		}
		
		[Test]
		public void MethodIsConstructor()
		{
			Assert.IsTrue(method.IsConstructor);
		}
	}
}
