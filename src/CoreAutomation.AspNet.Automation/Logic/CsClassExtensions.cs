using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CodeFactory;
using CodeFactory.DotNet.CSharp;
using CodeFactory.Formatting.CSharp;

namespace CoreAutomation.AspNet.Automation.Logic
{
    /// <summary>
    /// Extensions class that provides common automation tasks rolled up under standard extension methods that support the <see cref="CsClass"/> model.
    /// </summary>
    public static class CsClassExtensions
    {
        /// <summary>
        /// Extension method that determines if the class implements a logger field that supports extensions logging from Microsoft.
        /// </summary>
        /// <param name="source">Class model to evaluate.</param>
        /// <param name="loggerName">The name of the logger field.</param>
        /// <returns>Boolean flag if the field was found or not.</returns>
        public static bool HasMicrosoftExtensionsLoggerField(this CsClass source,string loggerName)
        {
            //Bounds check to determine if a class model was provided.
            if (source == null) return false;

            //Bounds check to confirm a field name was provided for the logger.
            if (String.IsNullOrEmpty(loggerName)) return false;
            
            //Bounds check to confirm the target class has fields. 
            if (!source.Fields.Any()) return false;

            //Looking up the field definition by the variable name of the logger.
            var field = source.Fields.FirstOrDefault(f => f.Name == loggerName);

            //If the logger was not found return false.
            if (field == null) return false;

            //Confirming the fields data type is under the logger namespace.
            if (field.DataType.Namespace != AspNetCoreConstants.MicrosoftLoggerNamespace) return false;

            //Confirming the field type is the ILogger interface
            // ReSharper disable once ConvertIfStatementToReturnStatement
            if (field.DataType.Name != AspNetCoreConstants.MicrosoftLoggerInterfaceName) return false;

            return true;
        }

        /// <summary>
        /// Extension method that determines if the logger field is implemented in the class. If it exists will return the provided source. Otherwise will add the logging namespace and the logger field.
        /// </summary>
        /// <param name="source">Source class to check for the logger field.</param>
        /// <param name="loggerName">The name of the logger field to check for.</param>
        /// <param name="parentSourceCode">The source code the class was loaded from.</param>
        /// <returns>The existing source code if the field is found, or the updated source code with the logging field added.</returns>
        public static async Task<CsSource> AddMicrosoftExtensionsLoggerFieldAsync(this CsClass source, string loggerName,
            CsSource parentSourceCode)
        {
            //Bounds checking
            if (source == null) return parentSourceCode;
            if (String.IsNullOrEmpty(loggerName)) return parentSourceCode;

            //Checking to see if the logger field already exists. If it does just return the parent source code.
            if (source.HasMicrosoftExtensionsLoggerField(loggerName)) return parentSourceCode;

            //Adding the logging namespace
            var currentSource = await parentSourceCode.AddUsingStatementAsync(AspNetCoreConstants.MicrosoftLoggerNamespace);

            var currentClass = currentSource.GetModel(source.LookupPath) as CsClass;

            if (currentClass == null) throw new CodeFactoryException("Cannot load class data to add the logger field.");

            CodeFactory.SourceFormatter fieldSource = new CodeFactory.SourceFormatter();

            fieldSource.AppendCodeLine(2, "/// <summary>");
            fieldSource.AppendCodeLine(2, "/// Logger for all logging interactions in the class.");
            fieldSource.AppendCodeLine(2, "/// </summary>");
            fieldSource.AppendCodeLine(2, $"private readonly {AspNetCoreConstants.MicrosoftLoggerInterfaceName} {loggerName};");
            fieldSource.AppendCodeLine(0);

            currentSource = await currentClass.AddToBeginningAsync(fieldSource.ReturnSource());

            return currentSource;

        }


        /// <summary>
        /// Helper method that confirms the class model does not implement a controller base class.
        /// </summary>
        /// <param name="classModel">The class model to confirm does not implement a base class.</param>
        /// <returns></returns>
        public static bool IsController(this CsClass classModel)
        {
            var baseClass = classModel?.BaseClass;
            if (baseClass == null) return false;

            var baseClassName = $"{baseClass.Namespace}.{baseClass.Name}";

            if (baseClassName == AspNetCoreConstants.ControllerBaseName) return true;

            bool isBaseClass = false;
            if (baseClass.BaseClass != null) isBaseClass = IsController(baseClass);
            return isBaseClass;
        }
    }
}
