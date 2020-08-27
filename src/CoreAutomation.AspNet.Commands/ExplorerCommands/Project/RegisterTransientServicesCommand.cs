using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CodeFactory;
using CodeFactory.Formatting.CSharp;
using CodeFactory.Logging;
using CodeFactory.VisualStudio;
using CodeFactory.VisualStudio.SolutionExplorer;
using CoreAutomation.AspNet.Automation.Logic;
using CoreAutomation.AspNet.Commands.ExplorerCommands.Project.Logic;

namespace CoreAutomation.AspNet.Commands.ExplorerCommands.Project
{
    /// <summary>
    /// Code factory command for automation of a project when selected from solution explorer.
    /// </summary>
    public class RegisterTransientServicesCommand : ProjectCommandBase
    {
        private static readonly string commandTitle = "Register Transient Services";
        private static readonly string commandDescription = "Dynamically generates all transient services to be registered in the services container managing this library.";

#pragma warning disable CS1998

        /// <inheritdoc />
        public RegisterTransientServicesCommand(ILogger logger, IVsActions vsActions) : base(logger, vsActions, commandTitle, commandDescription)
        {

            //Intentionally blank
        }
#pragma warning disable CS1998
        #region Overrides of VsCommandBase<VsProject>

        /// <summary>
        /// Validation logic that will determine if this command should be enabled for execution.
        /// </summary>
        /// <param name="result">The target model data that will be used to determine if this command should be enabled.</param>
        /// <returns>Boolean flag that will tell code factory to enable this command or disable it.</returns>
        public override async Task<bool> EnableCommandAsync(VsProject result)
        {
            //Result that determines if the the command is enabled and visible in the context menu for execution.
            bool isEnabled = false;

            try
            {
                //Confirming the project supports dependency injection
                isEnabled = await DependencyInjectionManagement.HasMicrosoftExtensionDependencyInjectionLibrariesAsync(result);

            }
            catch (Exception unhandledError)
            {
                _logger.Error($"The following unhandled error occured while checking if the solution explorer project command {commandTitle} is enabled. ",
                    unhandledError);
                isEnabled = false;
            }

            return isEnabled;
        }

        /// <summary>
        /// Code factory framework calls this method when the command has been executed. 
        /// </summary>
        /// <param name="result">The code factory model that has generated and provided to the command to process.</param>
        [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
        public override async Task ExecuteCommandAsync(VsProject result)
        {
            try
            {
                var registrationSourceCode = await RegisterServices.GetRegistrationClassAsync(result);

                if(registrationSourceCode == null) throw new CodeFactoryException("Could load or create the dependency injection code.");

                if(!registrationSourceCode.IsLoaded) throw new CodeFactoryException("Could load or create the dependency injection code.");

                var registrationClasses = await DependencyInjectionManagement.LoadInstanceProjectClassesForRegistrationAsync(result);

                var manager = registrationSourceCode.LoadNamespaceManager(result.DefaultNamespace);

                var injectionMethod = DependencyInjectionManagement.BuildInjectionMethod(registrationClasses, false,
                    true, RegisterServices.TransientClassRegistrationMethodName,
                    RegisterServices.ServiceCollectionParameterName, manager);

                if(injectionMethod == null) throw new CodeFactoryException("Could not generated the automated dependency injection method");

                var registrationClass =
                    registrationSourceCode.Classes.FirstOrDefault(c =>
                        c.Name == RegisterServices.RegistrationClassName);

                if(registrationClass == null) throw new CodeFactoryException("Could not load the dependency injection class");

                var autoRegistrationMethod = registrationClass.Methods.FirstOrDefault(m =>
                    m.Name == RegisterServices.TransientClassRegistrationMethodName);

                if (autoRegistrationMethod != null) await autoRegistrationMethod.ReplaceAsync(injectionMethod);
                else await registrationClass.AddToEndAsync(injectionMethod);
            }
            catch (Exception unhandledError)
            {
                _logger.Error($"The following unhandled error occured while executing the solution explorer project command {commandTitle}. ",
                    unhandledError);

            }
        }

        #endregion
    }
}
