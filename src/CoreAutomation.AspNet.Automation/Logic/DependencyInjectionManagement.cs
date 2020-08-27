using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using CodeFactory.DotNet.CSharp;
using CodeFactory.DotNet.CSharp.FormattedSyntax;
using CodeFactory.Formatting.CSharp;
using CodeFactory.VisualStudio;
using CodeFactory.Logging;
namespace CoreAutomation.AspNet.Automation.Logic
{
    /// <summary>
    /// Automation class that will manage creation of dependency injection code.
    /// </summary>
    public static class DependencyInjectionManagement
    {
        //Logger used for code factory logging
        // ReSharper disable once InconsistentNaming
        private static readonly ILogger _logger = LogManager.GetLogger("DependencyInjectionManagement");

        /// <summary>
        /// Helper method that confirms a target project supports the microsoft extensions for dependency injection and Configuration.
        /// </summary>
        /// <param name="sourceProject">Target project to check.</param>
        /// <returns>True if found or false of not.</returns>
        public static async Task<bool> HasMicrosoftExtensionDependencyInjectionLibrariesAsync(VsProject sourceProject)
        {
            if (sourceProject == null) return false;
            if (!sourceProject.IsLoaded) return false;
            var references = await sourceProject.GetProjectReferencesAsync();

            //Checking for dependency injection libraries.
            bool returnResult = references.Any(r => r.Name == "Microsoft.Extensions.DependencyInjection.Abstractions");
            if (!returnResult) return false;

            //Checking for the configuration libraries.
            returnResult = references.Any(r => r.Name == "Microsoft.Extensions.Configuration.Abstractions");
            return returnResult;
        }

        

        /// <summary>
        /// Loads all the classes that exist in the project from each code file found within the project. That qualify for dependency injection.
        /// </summary>
        /// <param name="project">The source project to get the classes from</param>
        /// <returns>The class models for all classes that qualify for transient dependency injection. If no classes are found an empty enumeration will be returned.</returns>
        public static  async Task<IEnumerable<CsClass>> LoadInstanceProjectClassesForRegistrationAsync(VsProject project)
        {
            var result = new List<CsClass>();
            if (project == null) return result;
            if (!project.HasChildren) return result;

            try
            {
                var projectChildren = await project.GetChildrenAsync(true, true);

                var csSourceCodeDocuments = projectChildren
                    .Where(m => m.ModelType == VisualStudioModelType.CSharpSource)
                    .Cast<VsCSharpSource>();
                
                foreach (var csSourceCodeDocument in csSourceCodeDocuments)
                {
                    var sourceCode = csSourceCodeDocument.SourceCode;
                    if (sourceCode == null) continue;
                    if (!sourceCode.Classes.Any()) continue;
                    var classes = sourceCode.Classes.Where(IsTransientClass).Where(c =>
                        result.All(r => $"{c.Namespace}.{c.Name}" != $"{r.Namespace}.{r.Name}"));

                    if(classes.Any()) result.AddRange(classes);
                }

            }
            catch (Exception unhandledError)
            {
                _logger.Error($"The following unhandled error occured while loading the classes to be added to dependency injection.",
                    unhandledError);
            }

            return result;
        }

        /// <summary>
        /// Checks class data to determine if it qualifies for transient dependency injection.
        /// - Checks to make sure the class only has 1 interface defined.
        /// - Checks to see the class only has 1 constructor defined.
        /// - Checks to see if the class is a asp.net controller if it it remove it
        /// - Checks to see the class name is a startup class if so will be removed.
        /// - Confirms the constructor has no well known types if so will be removed.
        /// </summary>
        /// <param name="classData">The class data to check.</param>
        /// <returns>Boolean state if it qualifies.</returns>
        public static bool IsTransientClass(CsClass classData)
        {
            if (classData == null) return false;

            if (classData.IsStatic) return false;
            if (classData.InheritedInterfaces.Any()) if (classData.InheritedInterfaces.Count > 1) return false;
            if (!classData.Constructors.Any()) return false;
            if (CsClassExtensions.IsController(classData)) return false;

            if (classData.Constructors.Count > 1) return false;

            var constructor = classData.Constructors.FirstOrDefault(m => m.HasParameters);

            if (constructor == null) return false;

            if (classData.Name == "Startup") return false;

            return !constructor.Parameters.Any(p => p.ParameterType.IsWellKnownType);
        }

        /// <summary>
        /// Builds the services registration method. This will contain the transient registrations for each class in the target project.
        /// This will return a signature of [Public/Private] [static] void [methodName](IServiceCollection [collectionParameterName])
        /// With a body that contains the full transient registrations.
        /// </summary>
        /// <param name="classes">The classes to be added.</param>
        /// <param name="isPublicMethod">Flag that determines if the method is public or private in scope.</param>
        /// <param name="isStatic">Flag to determine if the method should be defined as a static or instance method.</param>
        /// <param name="methodName">The target name of the method to be created.</param>
        /// <param name="serviceCollectionParameterName">The name of the service collection parameter where transient registrations will take place.</param>
        /// <param name="manager">The namespace manager that will be used to shorten type name registration with dependency injection. This will need to be loaded from the target class.</param>
        /// <returns>The formatted method.</returns>
        public static string BuildInjectionMethod(IEnumerable<CsClass> classes,bool isPublicMethod, bool isStatic, string methodName, string serviceCollectionParameterName, NamespaceManager manager = null)
        {

            CodeFactory.SourceFormatter registrationFormatter = new CodeFactory.SourceFormatter();

            string methodSecurity = isPublicMethod ? Security.Public : Security.Private;

            string methodSignature = isStatic
                ? $"{methodSecurity} static void {methodName}(IServiceCollection {serviceCollectionParameterName})"
                : $"{methodSecurity} void {methodName}(IServiceCollection {serviceCollectionParameterName})";

            registrationFormatter.AppendCodeLine(0, "/// <summary>");
            registrationFormatter.AppendCodeLine(0, "/// Automated registration of classes using transient registration.");
            registrationFormatter.AppendCodeLine(0, "/// </summary>");
            registrationFormatter.AppendCodeLine(0, $"/// <param name=\"{serviceCollectionParameterName}\">The service collection to register services.</param>");
            registrationFormatter.AppendCodeLine(0, methodSignature);
            registrationFormatter.AppendCodeLine(0, "{");
            registrationFormatter.AppendCodeLine(1,"//This method was auto generated, do not modify by hand!");
            foreach (var csClass in classes)
            {
                var registration = FormatTransientRegistration(csClass,serviceCollectionParameterName,manager);
                if(registration != null) registrationFormatter.AppendCodeLine(1, registration);
            }
            registrationFormatter.AppendCodeLine(0, "}");

            return registrationFormatter.ReturnSource();
        }

        /// <summary>
        /// Defines the transient registration statement that will register the class.
        /// </summary>
        /// <param name="classData">The class model to get the registration from.</param>
        /// <param name="serviceCollectionParameterName">The name of the service collection parameter that the transient is being made to.</param>
        /// <param name="manager">Optional parameter that contains the namespace manager that contains the known using statements and target namespace for the class that will host this registration data.</param>
        /// <returns>The formatted transient registration call or null if the class does not meet the criteria.</returns>
        private static string FormatTransientRegistration(CsClass classData,string serviceCollectionParameterName, NamespaceManager manager = null)
        {
            //Cannot find the class data will return null
            if (classData == null) return null;

            string registrationType = null;
            string classType = null;

            ICsMethod constructorData = classData.Constructors.FirstOrDefault();

            //Confirming we have a constructor 
            if (constructorData == null) return null;

            //Getting the fully qualified type name for the formatters library for the class.
            classType = classData.CSharpFormatBaseTypeName(manager);  
            
            //if we are not able to format the class name correctly return null.
            if (classType == null) return null;

            //Assuming the first interface inherited will be used for dependency injection if any are provided.
            if (classData.InheritedInterfaces.Any())
            {
                CsInterface interfaceData = classData.InheritedInterfaces.FirstOrDefault();

                if (interfaceData != null) registrationType = interfaceData.CSharpFormatInheritanceTypeName(manager);
            }

            //Creating statement to add the the container.
            string diStatement = registrationType != null
                ? $"{serviceCollectionParameterName}.AddTransient<{registrationType},{classType}>();" :
                  $"{serviceCollectionParameterName}.AddTransient<{classType}>();";

            return diStatement;
        }
    }
}
