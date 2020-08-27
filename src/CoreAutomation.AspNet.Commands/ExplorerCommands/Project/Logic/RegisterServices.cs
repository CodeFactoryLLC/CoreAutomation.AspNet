using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using CodeFactory;
using CodeFactory.DotNet.CSharp;
using CodeFactory.Formatting.CSharp;
using CodeFactory.Logging;
using CodeFactory.VisualStudio;

namespace CoreAutomation.AspNet.Commands.ExplorerCommands.Project.Logic
{
    public static class RegisterServices
    {
        // ReSharper disable once InconsistentNaming
        private static readonly ILogger _logger = LogManager.GetLogger(nameof(RegisterServices));

        /// <summary>
        /// Stores the default name of the registration class to be used in searches and creation.
        /// </summary>
        public const string RegistrationClassName = "RegisterLibraryServices";

        /// <summary>
        /// The name of the automatic transient registration class name
        /// </summary>
        public const string TransientClassRegistrationMethodName = "AutomaticTransientServicesRegistration";

        /// <summary>
        /// The parameter name assigned to the service collection for automatic service registration.
        /// </summary>
        public const string ServiceCollectionParameterName = "serviceCollection";

        /// <summary>
        /// Searches the root of a project for the dependency injection registration class. If it is not found will generate a new service registration class.
        /// </summary>
        /// <param name="source">The source project to load the registration class from.</param>
        /// <param name="className">The name of the class that is used for service registration. This will be set to the constant <see cref="RegistrationClassName"/> this can be overwritten with a custom class name.</param>
        /// <returns>The source code model for the registration class.</returns>
        public static async Task<CsSource>  GetRegistrationClassAsync(VsProject source, string className = RegistrationClassName)
        {
            _logger.DebugEnter();

            if (source == null)
            {
                _logger.DebugExit();
                throw new ArgumentNullException(nameof(source));
            }
            if (!source.IsLoaded)
            {
                _logger.DebugExit();
                throw new CodeFactoryException("Project model was not loaded cannot load or create the registration class.");
            }
            if (string.IsNullOrEmpty(className))
            {
                _logger.DebugExit();
                throw new ArgumentNullException(nameof(className));
            }

            var projectFiles = await source.GetChildrenAsync(false, true);

            CsSource registrationSource = null;

            try
            {
                //Searching the projects root directory code files for the registration class
                if (projectFiles.Any())
                    registrationSource = projectFiles.Where(f => f.ModelType == VisualStudioModelType.CSharpSource)
                        .Cast<VsCSharpSource>().FirstOrDefault(s => s.SourceCode.Classes.Any(c => c.Name == className))
                        ?.SourceCode;

                //If no registration source code file was found then create a new registration class from scratch.
                if (registrationSource == null)
                {
                    var registrationClassCode = BuildNewRegistrationClass(source, className);
                    if (registrationClassCode == null)
                        throw new CodeFactoryException(
                            "Could not generate the dependency injection registration source code");
                    string registrationClassFileName = $"{className}.cs";

                    var document = await source.AddDocumentAsync(registrationClassFileName, registrationClassCode);

                    registrationSource = await document.GetCSharpSourceModelAsync();

                }

                if (registrationSource == null)
                    throw new CodeFactoryException("Cannot load the source code for the registration class.");
            }
            catch (CodeFactoryException)
            {
                _logger.DebugExit();
                throw;
            }
            catch (Exception unhandledException)
            {
                _logger.Error("The following unhandled error occured, see the log for details.",unhandledException);
                throw new CodeFactoryException("An error occured while loading the registration class source code, cannot continue.");   
            }

            _logger.DebugExit();
            return registrationSource;
        }

        /// <summary>
        /// Create a new registration class from scratch
        /// </summary>
        /// <returns>Fully formatted source code for registration class.</returns>
        public static string BuildNewRegistrationClass(VsProject sourceProject, string className = RegistrationClassName)
        {
            _logger.DebugEnter();

            if (sourceProject == null)
            {
                _logger.DebugExit();
                return null;
            }

            if (!sourceProject.IsLoaded)
            {
                _logger.DebugExit();
                return null;
            }
            string defaultNamespace = sourceProject.DefaultNamespace;

            if (string.IsNullOrEmpty(defaultNamespace))
            {
                _logger.DebugExit();
                return null;
            }

            CodeFactory.SourceFormatter classFormatter = new CodeFactory.SourceFormatter();

            classFormatter.AppendCodeLine(0, "using System;");
            classFormatter.AppendCodeLine(0, "using System.Collections.Generic;");
            classFormatter.AppendCodeLine(0, "using Microsoft.Extensions.Configuration;");
            classFormatter.AppendCodeLine(0, "using Microsoft.Extensions.DependencyInjection;");
            classFormatter.AppendCodeLine(0, $"namespace {defaultNamespace}");
            classFormatter.AppendCodeLine(0,"{");
            classFormatter.AppendCodeLine(1, "/// <summary>");
            classFormatter.AppendCodeLine(1, $"/// Responsible for dependency inject of services for the library <see cref=\"{sourceProject.Name}\"/>");
            classFormatter.AppendCodeLine(1, "/// </summary>");
            classFormatter.AppendCodeLine(1, $"public static class {className}");
            classFormatter.AppendCodeLine(1,"{");
            classFormatter.AppendCodeLine(2, "/// <summary>");
            classFormatter.AppendCodeLine(2, "/// Flag that determines if registration has already been performed on the library");
            classFormatter.AppendCodeLine(2, "/// </summary>");
            classFormatter.AppendCodeLine(2, "public static bool ServicesRegistered { get; private set; }");
            classFormatter.AppendCodeLine(0);
            classFormatter.AppendCodeLine(2, "/// <summary>");
            classFormatter.AppendCodeLine(2, "/// Register the dependency injection service that are supported by this library and triggers registration for other libraries referenced by this library.");
            classFormatter.AppendCodeLine(2, "/// </summary>");
            classFormatter.AppendCodeLine(2, "/// <param name=\"serviceCollection\">The service collection that dependency injection uses.</param>");
            classFormatter.AppendCodeLine(2, "/// <param name=\"configuration\">The hosting systems configuration.</param>");
            classFormatter.AppendCodeLine(2, "public static void RegisterServices(IServiceCollection serviceCollection, IConfiguration configuration)");
            classFormatter.AppendCodeLine(2,"{");
            classFormatter.AppendCodeLine(3, "//If services have already been registered do no process service registration. This protects from duplicate registration.");
            classFormatter.AppendCodeLine(3, "if (ServicesRegistered) return;");
            classFormatter.AppendCodeLine(0);
            classFormatter.AppendCodeLine(3, "//Call down stream libraries and have them complete their registration.");
            classFormatter.AppendCodeLine(3, "RegisterDependentServices(serviceCollection, configuration);");
            classFormatter.AppendCodeLine(0);
            classFormatter.AppendCodeLine(3, "//Process all manually managed registrations for this library.");
            classFormatter.AppendCodeLine(3, "ManualRegistrationServices(serviceCollection, configuration);");
            classFormatter.AppendCodeLine(0);
            classFormatter.AppendCodeLine(3, "//Process all automatically discovered services for registration.");
            classFormatter.AppendCodeLine(3,"AutomaticTransientServicesRegistration(serviceCollection);");
            classFormatter.AppendCodeLine(0);
            classFormatter.AppendCodeLine(3, "//Update the registration status.");
            classFormatter.AppendCodeLine(3, "ServicesRegistered = true;");
            classFormatter.AppendCodeLine(2,"}");
            classFormatter.AppendCodeLine(0);
            classFormatter.AppendCodeLine(2, "/// <summary>");
            classFormatter.AppendCodeLine(2, "/// Register dependency injection services for child libraries referenced by this library.");
            classFormatter.AppendCodeLine(2, "/// </summary>");
            classFormatter.AppendCodeLine(2,"/// <param name=\"serviceCollection\">The service collection that dependency injection uses.</param>");
            classFormatter.AppendCodeLine(2, "/// <param name=\"configuration\">The hosting systems configuration.</param>");
            classFormatter.AppendCodeLine(2, "private static void RegisterDependentServices(IServiceCollection serviceCollection, IConfiguration configuration)");
            classFormatter.AppendCodeLine(2,"{");
            classFormatter.AppendCodeLine(3, "//Todo: Register services from other libraries directly referenced by this library.");
            classFormatter.AppendCodeLine(2,"}");
            classFormatter.AppendCodeLine(0);
            classFormatter.AppendCodeLine(2, "/// <summary>");
            classFormatter.AppendCodeLine(2, "/// Register services with the service collection manually. This is where manual singleton objects and complex service registration is managed.");
            classFormatter.AppendCodeLine(2, "/// </summary>");
            classFormatter.AppendCodeLine(2, "/// <param name=\"serviceCollection\">The service collection that dependency injection uses.</param>");
            classFormatter.AppendCodeLine(2, "/// <param name=\"configuration\">The hosting systems configuration.</param>");
            classFormatter.AppendCodeLine(2, "private static void ManualRegistrationServices(IServiceCollection serviceCollection, IConfiguration configuration)");
            classFormatter.AppendCodeLine(2,"{");
            classFormatter.AppendCodeLine(3, "//TODO: manually add singleton and manually managed registrations here.");
            classFormatter.AppendCodeLine(2,"}");
            classFormatter.AppendCodeLine(0);
            classFormatter.AppendCodeLine(2, "/// <summary>");
            classFormatter.AppendCodeLine(2, "/// Automated registration of classes using transient registration.");
            classFormatter.AppendCodeLine(2, "/// </summary>");
            classFormatter.AppendCodeLine(2, "/// <param name=\"serviceCollection\">The service collection to register services.</param>");
            classFormatter.AppendCodeLine(2, "private static void AutomaticTransientServicesRegistration(IServiceCollection serviceCollection)");
            classFormatter.AppendCodeLine(2,"{");
            classFormatter.AppendCodeLine(3, "//Will be updated through code automation do not change or add to this method by hand.");
            classFormatter.AppendCodeLine(2,"}");
            classFormatter.AppendCodeLine(1,"}");
            classFormatter.AppendCodeLine(0,"}");

            _logger.DebugExit();
            return classFormatter.ReturnSource();
        }
    }
}
