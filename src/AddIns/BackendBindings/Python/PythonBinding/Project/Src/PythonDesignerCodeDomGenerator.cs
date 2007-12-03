﻿// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Matthew Ward" email="mrward@users.sourceforge.net"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.IO;
using IronPython.CodeDom;

namespace ICSharpCode.PythonBinding
{
	/// <summary>
	/// Generates a code dom from a form or user control
	/// definition that can be used in the forms designer. The
	/// code dom generated by the python provider cannot
	/// be displayed in the forms designer with some changes
	/// being made.
	/// </summary>
	public sealed class PythonDesignerCodeDomGenerator
	{
		CodeCompileUnit compileUnit;
		CodeTypeDeclaration formClass;
		CodeMemberMethod initializeComponentMethod;
		
		PythonDesignerCodeDomGenerator(CodeCompileUnit compileUnit)
		{
			this.compileUnit = compileUnit;
		}
		
		/// <summary>
		/// Converts the python code into a code dom compatible
		/// with the forms designer.
		/// </summary>
		public static CodeCompileUnit Parse(string pythonCode)
		{
			return Parse(new StringReader(pythonCode));
		}
		
		/// <summary>
		/// Converts the python code into a code dom compatible
		/// with the forms designer.
		/// </summary>
		public static CodeCompileUnit Parse(TextReader reader)
		{
			PythonProvider provider = new PythonProvider();
			CodeCompileUnit compileUnit = provider.Parse(reader);
			
			PythonDesignerCodeDomGenerator generator = new PythonDesignerCodeDomGenerator(compileUnit);
			return generator.FixCompileUnit();
		}
		
		/// <summary>
		/// Fixes the compile unit generated by the Python provider.
		/// This method does the following:
		/// 
		/// 1) Fully qualifieds the base type of the form or user controls.
		///
		/// The PythonProvider does not fully qualify the base class
		/// of the form or user control being loaded so this method
		/// corrects the code compile unit. Otherwise the code compile
		/// unit cannot be loaded by the BasicDesignerLoader. If this
		/// is not done you get an InvalidOperationException:
		/// 
		/// "The designer could not be shown for this file because none of the classes within it can be designed.  The designer inspected the following classes in the file: 
		/// MainForm --- The base class 'Form' could not be loaded."
		/// 
		/// When the base class name is replaced with the fully qualified
		/// name the designer loader can load the form.
		/// 
		/// 2) Adds missing class fields that the forms designer needs.
		/// 
		/// The PythonProvider does not add class fields. Using the code
		/// dom generated by the PythonProvider cannot be loaded by the forms
		/// designer. Loading it will result in the CodeDomSerializerBase
		/// failing in the DeserializeExpression method with an error like:
		/// 
		/// "The variable 'textBox1' is either undeclared or was never assigned."
		/// 
		/// 3) Convert field references to property references.
		/// 
		/// Assigning a value to a property causes the 
		/// CodeDomSerializerBase.DeserializeAssignStatement to fail with:
		/// 
		/// "The type 'System.Windows.Forms.TextBox' has no field named 'Name'."
		/// </remarks>
		CodeCompileUnit FixCompileUnit()
		{
			formClass = FindForm(compileUnit);
			FullyQualifyBaseType(formClass);
			initializeComponentMethod = FindInitializeComponentMethod(formClass);			
			AddFields();
			
			IronPython.CodeDom.PythonProvider provider = new IronPython.CodeDom.PythonProvider();
			CodeGeneratorOptions options = new CodeGeneratorOptions();
			options.BlankLinesBetweenMembers = false;
			options.IndentString = "\t";
			StringWriter writer = new StringWriter();
			provider.GenerateCodeFromCompileUnit(compileUnit, writer, options);
			
			Console.WriteLine("Code: " + writer.ToString());
			
			return compileUnit;
		}
		
		/// <summary>
		/// Returns the initialize component method from the form class.
		/// </summary>
		static CodeMemberMethod FindInitializeComponentMethod(CodeTypeDeclaration formClass)
		{
			foreach (CodeTypeMember member in formClass.Members) {
				if (member.Name == "InitializeComponent") {
					return member as CodeMemberMethod;
				}
			}
			return null;
		}
		
		/// <summary>
		/// Finds the Form class in the code compile unit.
		/// </summary>
		static CodeTypeDeclaration FindForm(CodeCompileUnit unit)
		{
			foreach (CodeNamespace ns in unit.Namespaces) {
				foreach (CodeTypeDeclaration type in ns.Types) {
					foreach (CodeTypeMember member in type.Members) {
						if (member.Name == "InitializeComponent") {
							return type;
						}
					}
				}
			}	
			return null;
		}		
		
		/// <summary>
		/// Fully qualifies the base type. The Python code dom 
		/// generator does not fully qualify the base type of a class
		/// so this method does the work.
		/// </summary>
		static void FullyQualifyBaseType(CodeTypeDeclaration type)
		{
			CodeTypeReference reference = type.BaseTypes[0];
			if (reference.BaseType == "Form") {
				reference.BaseType = "System.Windows.Forms.Form";
			} else if (reference.BaseType == "UserControl") {
				reference.BaseType = "System.Windows.Forms.UserControl";
			}
		}
		
		/// <summary>
		/// Adds the fields to the form class based on the fields that
		/// are initialized in the InitializeComponents method.
		/// </summary>
		void AddFields()
		{
			foreach (CodeStatement statement in initializeComponentMethod.Statements) {
				CodeAssignStatement assignStatement = statement as CodeAssignStatement;
				if (assignStatement != null) {
					CodeMemberField field = CreateField(assignStatement);
					if (field != null) {
						formClass.Members.Add(field);
					} else {
						FixFieldReference(assignStatement);
					}
				}
			}
		}
		
		/// <summary>
		/// Field references are converted to property references.
		/// </summary>
		void FixFieldReference(CodeAssignStatement assignStatement)
		{
			CodeFieldReferenceExpression fieldRef = assignStatement.Left as CodeFieldReferenceExpression;
			if (fieldRef != null) {
				CodePropertyReferenceExpression propertyRef = new CodePropertyReferenceExpression();
				propertyRef.PropertyName = fieldRef.FieldName;
				propertyRef.TargetObject = fieldRef.TargetObject;
				assignStatement.Left = propertyRef;
			}
		}
		
		/// <summary>
		/// Creates a CodeMemberField if the assign statement
		/// refers to a field initialisation statement, for example:
		/// 
		/// textBox1 = System.Windows.Forms.Form() 
		/// </summary>
		CodeMemberField CreateField(CodeAssignStatement assignStatement)
		{
			string fieldName = GetFieldName(assignStatement);
			CodeTypeReference type = GetType(assignStatement);

			if (fieldName != null && type != null) {
				CodeMemberField field = new CodeMemberField();
				field.Type = type;
				field.Name = fieldName;
				return field;
			}
			return null;
		}
		
		/// <summary>
		/// Gets the type reference created by the right hand side of the
		/// assign statement.
		/// </summary>
		static CodeTypeReference GetType(CodeAssignStatement assignStatement)
		{
			CodeObjectCreateExpression objectCreateExpression = assignStatement.Right as CodeObjectCreateExpression;
			if (objectCreateExpression != null) {
				return objectCreateExpression.CreateType;
			}
			return null;
		}

		/// <summary>
		/// Gets the field name from the left hand side of the
		/// assignment expression.
		/// </summary>
		/// <returns>
		/// null if the assign statement does not refer to a field 
		/// initialisation statement (e.g.
		/// textBox1 = System.Windows.Forms.Form). 
		/// </returns>
		static string GetFieldName(CodeAssignStatement assignStatement)
		{
			CodeFieldReferenceExpression fieldRefExpression = assignStatement.Left as CodeFieldReferenceExpression;
			if (IsFieldInitialization(fieldRefExpression)) {
				return fieldRefExpression.FieldName;
			}
			return null;
		}
		
		/// <summary>
		/// Checks the field reference expression has a target object
		/// of the type CodeFieldReferenceExpression. If so then
		/// the field reference refers a property assignment:
		///  
		/// textBox1.Name = 'test'
		/// 
		/// and not a field initializer:
		///  
		/// textBox1 = System.Windows.Forms.Form()
		/// </summary>
		static bool IsFieldInitialization(CodeFieldReferenceExpression expression)
		{
			if (expression != null) {
				return expression.TargetObject.GetType() != typeof(CodeFieldReferenceExpression);
			}
			return false;
		}
	}
}
