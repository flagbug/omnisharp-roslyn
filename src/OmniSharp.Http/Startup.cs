using System;
using System.Composition.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniSharp.Eventing;
using OmniSharp.Http.Middleware;
using OmniSharp.Options;
using OmniSharp.Roslyn;
using OmniSharp.Services;
using OmniSharp.Stdio.Services;

namespace OmniSharp.Http
{
    public class Startup
    {
        private readonly IOmniSharpEnvironment _environment;
        private readonly IEventEmitter _eventEmitter;
        private readonly ISharedTextWriter _writer;
        private readonly OmniSharpHttpEnvironment _httpEnvironment;
        private readonly IConfigurationRoot _configuration;
        // private OmniSharpWorkspace _workspace;
        private CompositionHost _compositionHost;

        public Startup()
        {
            _environment = Program.Instance._environment;
            _eventEmitter = NullEventEmitter.Instance;
            _writer = Program.Instance._sharedTextWriter;
            _httpEnvironment = new OmniSharpHttpEnvironment { Port = Program.Instance._serverPort };
            _configuration = new OmniSharpConfigurationBuilder(_environment).Build();
        }

        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            var serviceProvider = OmniSharpMefBuilder.CreateDefaultServiceProvider(_configuration);
            var mefBuilder = new OmniSharpMefBuilder(serviceProvider, _environment, _writer, _eventEmitter);
            var compositionHost = mefBuilder.Build();
            _compositionHost = compositionHost;
            return serviceProvider;
        }

        public void Configure(
            IApplicationBuilder app,
            IServiceProvider serviceProvider,
            ILoggerFactory loggerFactory,
            IOptionsMonitor<OmniSharpOptions> options)
        {
            var logger = loggerFactory.CreateLogger<Startup>();
            loggerFactory.AddConsole((category, level) =>
            {
                if (OmniSharp.LogFilter(category, level, _environment)) return true;

                if (string.Equals(category, typeof(ExceptionHandlerMiddleware).FullName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                return false;
            });

            app.UseRequestLogging();
            app.UseExceptionHandler("/error");
            app.UseMiddleware<EndpointMiddleware>();
            app.UseMiddleware<StatusMiddleware>();
            app.UseMiddleware<StopServerMiddleware>();

            new OmniSharpWorkspaceInitializer(serviceProvider, _compositionHost, _configuration, logger).Initialize();

            logger.LogInformation($"Omnisharp server running on port '{_httpEnvironment.Port}' at location '{_environment.TargetDirectory}' on host {_environment.HostProcessId}.");
        }
    }
}