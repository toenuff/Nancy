namespace Nancy
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Nancy.Bootstrapper;
    using Nancy.Configuration;
    using Nancy.Diagnostics;
    using Nancy.TinyIoc;

    /// <summary>
    /// TinyIoC bootstrapper - registers default route resolver and registers itself as
    /// INancyModuleCatalog for resolving modules but behavior can be overridden if required.
    /// </summary>
    public class DefaultNancyBootstrapper : NancyBootstrapperWithRequestContainerBase<TinyIoCContainer>
    {
        /// <summary>
        /// Default assemblies that are ignored for autoregister
        /// </summary>
        public static IEnumerable<Func<Assembly, bool>> DefaultAutoRegisterIgnoredAssemblies = new Func<Assembly, bool>[]
            {
                asm => asm.FullName.StartsWith("Microsoft.", StringComparison.Ordinal),
                asm => asm.FullName.StartsWith("System.", StringComparison.Ordinal),
                asm => asm.FullName.StartsWith("System,", StringComparison.Ordinal),
                asm => asm.FullName.StartsWith("CR_ExtUnitTest", StringComparison.Ordinal),
                asm => asm.FullName.StartsWith("mscorlib,", StringComparison.Ordinal),
                asm => asm.FullName.StartsWith("CR_VSTest", StringComparison.Ordinal),
                asm => asm.FullName.StartsWith("DevExpress.CodeRush", StringComparison.Ordinal),
                asm => asm.FullName.StartsWith("IronPython", StringComparison.Ordinal),
                asm => asm.FullName.StartsWith("IronRuby", StringComparison.Ordinal),
                asm => asm.FullName.StartsWith("xunit", StringComparison.Ordinal),
                asm => asm.FullName.StartsWith("Nancy.Testing", StringComparison.Ordinal),
                asm => asm.FullName.StartsWith("MonoDevelop.NUnit", StringComparison.Ordinal),
                asm => asm.FullName.StartsWith("SMDiagnostics", StringComparison.Ordinal),
                asm => asm.FullName.StartsWith("CppCodeProvider", StringComparison.Ordinal),
                asm => asm.FullName.StartsWith("WebDev.WebHost40", StringComparison.Ordinal),
            };

        /// <summary>
        /// Gets the assemblies to ignore when autoregistering the application container
        /// Return true from the delegate to ignore that particular assembly, returning false
        /// does not mean the assembly *will* be included, a true from another delegate will
        /// take precedence.
        /// </summary>
        protected virtual IEnumerable<Func<Assembly, bool>> AutoRegisterIgnoredAssemblies
        {
            get { return DefaultAutoRegisterIgnoredAssemblies; }
        }

        /// <summary>
        /// Configures the container using AutoRegister followed by registration
        /// of default INancyModuleCatalog and IRouteResolver.
        /// </summary>
        /// <param name="container">Container instance</param>
        protected override void ConfigureApplicationContainer(TinyIoCContainer container)
        {
            AutoRegister(container, this.AutoRegisterIgnoredAssemblies);
        }

        /// <summary>
        /// Resolve INancyEngine
        /// </summary>
        /// <returns>INancyEngine implementation</returns>
        protected override sealed INancyEngine GetEngineInternal()
        {
            return this.ApplicationContainer.Resolve<INancyEngine>();
        }

        /// <summary>
        /// Create a default, unconfigured, container
        /// </summary>
        /// <returns>Container instance</returns>
        protected override TinyIoCContainer GetApplicationContainer()
        {
            return new TinyIoCContainer();
        }

        /// <summary>
        /// Registers an <see cref="INancyEnvironment"/> instance in the container.
        /// </summary>
        /// <param name="container">The container to register into.</param>
        /// <param name="environment">The <see cref="INancyEnvironment"/> instance to register.</param>
        protected override void RegisterNancyEnvironment(TinyIoCContainer container, INancyEnvironment environment)
        {
            container.Register(environment);
        }

        /// <summary>
        /// Register the bootstrapper's implemented types into the container.
        /// This is necessary so a user can pass in a populated container but not have
        /// to take the responsibility of registering things like INancyModuleCatalog manually.
        /// </summary>
        /// <param name="applicationContainer">Application container to register into</param>
        protected override sealed void RegisterBootstrapperTypes(TinyIoCContainer applicationContainer)
        {
            applicationContainer.Register<INancyModuleCatalog>(this);
        }

        /// <summary>
        /// Register the default implementations of internally used types into the container as singletons
        /// </summary>
        /// <param name="container">Container to register into</param>
        /// <param name="typeRegistrations">Type registrations to register</param>
        protected override sealed void RegisterTypes(TinyIoCContainer container, IEnumerable<TypeRegistration> typeRegistrations)
        {
            foreach (var typeRegistration in typeRegistrations)
            {
                switch (typeRegistration.Lifetime)
                {
                    case Lifetime.Transient:
                        container.Register(typeRegistration.RegistrationType, typeRegistration.ImplementationType).AsMultiInstance();
                        break;
                    case Lifetime.Singleton:
                        container.Register(typeRegistration.RegistrationType, typeRegistration.ImplementationType).AsSingleton();
                        break;
                    case Lifetime.PerRequest:
                        throw new InvalidOperationException("Unable to directly register a per request lifetime.");
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        /// <summary>
        /// Register the various collections into the container as singletons to later be resolved
        /// by IEnumerable{Type} constructor dependencies.
        /// </summary>
        /// <param name="container">Container to register into</param>
        /// <param name="collectionTypeRegistrations">Collection type registrations to register</param>
        protected override sealed void RegisterCollectionTypes(TinyIoCContainer container, IEnumerable<CollectionTypeRegistration> collectionTypeRegistrations)
        {
            foreach (var collectionTypeRegistration in collectionTypeRegistrations)
            {
                switch (collectionTypeRegistration.Lifetime)
                {
                    case Lifetime.Transient:
                        container.RegisterMultiple(collectionTypeRegistration.RegistrationType, collectionTypeRegistration.ImplementationTypes).AsMultiInstance();
                        break;
                    case Lifetime.Singleton:
                        container.RegisterMultiple(collectionTypeRegistration.RegistrationType, collectionTypeRegistration.ImplementationTypes).AsSingleton();
                        break;
                    case Lifetime.PerRequest:
                        throw new InvalidOperationException("Unable to directly register a per request lifetime.");
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        /// <summary>
        /// Register the given module types into the container
        /// </summary>
        /// <param name="container">Container to register into</param>
        /// <param name="moduleRegistrationTypes">NancyModule types</param>
        protected override sealed void RegisterRequestContainerModules(TinyIoCContainer container, IEnumerable<ModuleRegistration> moduleRegistrationTypes)
        {
            foreach (var moduleRegistrationType in moduleRegistrationTypes)
            {
                container.Register(
                    typeof(INancyModule),
                    moduleRegistrationType.ModuleType,
                    moduleRegistrationType.ModuleType.FullName).
                    AsSingleton();
            }
        }

        /// <summary>
        /// Register the given instances into the container
        /// </summary>
        /// <param name="container">Container to register into</param>
        /// <param name="instanceRegistrations">Instance registration types</param>
        protected override void RegisterInstances(TinyIoCContainer container, IEnumerable<InstanceRegistration> instanceRegistrations)
        {
            foreach (var instanceRegistration in instanceRegistrations)
            {
                container.Register(
                    instanceRegistration.RegistrationType,
                    instanceRegistration.Implementation);
            }
        }

        /// <summary>
        /// Creates a per request child/nested container
        /// </summary>
        /// <param name="context">Current context</param>
        /// <returns>Request container instance</returns>
        protected override TinyIoCContainer CreateRequestContainer(NancyContext context)
        {
            return this.ApplicationContainer.GetChildContainer();
        }

        /// <summary>
        /// Gets the <see cref="INancyEnvironmentConfigurator"/> used by th.
        /// </summary>
        /// <returns>An <see cref="INancyEnvironmentConfigurator"/> instance.</returns>
        protected override INancyEnvironmentConfigurator GetEnvironmentConfigurator()
        {
            return this.ApplicationContainer.Resolve<INancyEnvironmentConfigurator>();
        }

        /// <summary>
        /// Gets the diagnostics for initialization
        /// </summary>
        /// <returns>IDiagnostics implementation</returns>
        protected override IDiagnostics GetDiagnostics()
        {
            return this.ApplicationContainer.Resolve<IDiagnostics>();
        }

        /// <summary>
        /// Gets all registered startup tasks
        /// </summary>
        /// <returns>An <see cref="IEnumerable{T}"/> instance containing <see cref="IApplicationStartup"/> instances. </returns>
        protected override IEnumerable<IApplicationStartup> GetApplicationStartupTasks()
        {
            return this.ApplicationContainer.ResolveAll<IApplicationStartup>(false);
        }

        /// <summary>
        /// Gets all registered request startup tasks
        /// </summary>
        /// <returns>An <see cref="IEnumerable{T}"/> instance containing <see cref="IRequestStartup"/> instances.</returns>
        protected override IEnumerable<IRequestStartup> RegisterAndGetRequestStartupTasks(TinyIoCContainer container, Type[] requestStartupTypes)
        {
            container.RegisterMultiple(typeof(IRequestStartup), requestStartupTypes);

            return container.ResolveAll<IRequestStartup>(false);
        }

        /// <summary>
        /// Gets all registered application registration tasks
        /// </summary>
        /// <returns>An <see cref="IEnumerable{T}"/> instance containing <see cref="IRegistrations"/> instances.</returns>
        protected override IEnumerable<IRegistrations> GetRegistrationTasks()
        {
            return this.ApplicationContainer.ResolveAll<IRegistrations>(false);
        }

        /// <summary>
        /// Retrieve all module instances from the container
        /// </summary>
        /// <param name="container">Container to use</param>
        /// <returns>Collection of NancyModule instances</returns>
        protected override sealed IEnumerable<INancyModule> GetAllModules(TinyIoCContainer container)
        {
            var nancyModules = container.ResolveAll<INancyModule>(false);
            return nancyModules;
        }

        /// <summary>
        /// Retrieve a specific module instance from the container
        /// </summary>
        /// <param name="container">Container to use</param>
        /// <param name="moduleType">Type of the module</param>
        /// <returns>NancyModule instance</returns>
        protected override sealed INancyModule GetModule(TinyIoCContainer container, Type moduleType)
        {
            container.Register(typeof(INancyModule), moduleType);

            return container.Resolve<INancyModule>();
        }

        /// <summary>
        /// Executes auto registration with the given container.
        /// </summary>
        /// <param name="container">Container instance</param>
        private static void AutoRegister(TinyIoCContainer container, IEnumerable<Func<Assembly, bool>> ignoredAssemblies)
        {
            var assembly = typeof(NancyEngine).Assembly;

            container.AutoRegister(AppDomain.CurrentDomain.GetAssemblies().Where(a => !ignoredAssemblies.Any(ia => ia(a))), DuplicateImplementationActions.RegisterMultiple, t => t.Assembly != assembly);
        }
    }
}
