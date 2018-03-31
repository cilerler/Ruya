﻿using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Ruya.Primitives;

namespace Ruya.ConsoleHost
{
	public class Startup
	{
		public IConfiguration Configuration { get; private set; }
		public IServiceProvider ServiceProvider { get; private set; }

		private void Register()
		{
			BeforeRegistrations();
			RegisterConfiguration();
			RegisterServices();
			AfterRegistrations();
		}

		private static void BeforeRegistrations()
		{
			#region directory
            if (StartupInjector.Instance.CustomDirectoryNameExists)
			{
                Directory.SetCurrentDirectory(StartupInjector.Instance.CustomDirectoryName);
            }
			#endregion
		}

		private void RegisterConfiguration()
		{
            string environmentName = StartupInjector.Instance.EnvironmentName;

			Dictionary<string, string> config = null;
			if (StartupInjector.Instance.InMemoryCollectionExists)
			{
				config = StartupInjector.Instance.InMemoryCollection();
			}

			var configuration = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory());
			if (config != null)
            {
				configuration = configuration.AddInMemoryCollection(config);
            }

            const string configurationJsonFile = "appsettings.json";
            if (!File.Exists(configurationJsonFile))
            {
                throw new NotImplementedException($"Configuration file does not exist!  Current Directory {Directory.GetCurrentDirectory()}");
            }

			configuration.AddJsonFile(configurationJsonFile, true, true)
						 .AddJsonFile($"{Path.GetFileNameWithoutExtension(configurationJsonFile)}.{environmentName}{Path.GetExtension(configurationJsonFile)}", true, true);

            if (EnvironmentHelper.IsDevelopment)
            {
                configuration.AddUserSecrets<Startup>();
            }

            configuration.AddEnvironmentVariables();

		    if (StartupInjector.Instance.ArgsExist)
		    {
		        configuration.AddCommandLine(StartupInjector.Instance.Args);
            }            
			Configuration = configuration.Build();
		}

		private void RegisterServices()
		{
			IServiceCollection serviceCollection = new ServiceCollection();

			serviceCollection.AddSingleton(Configuration);

			serviceCollection.AddOptions();
			serviceCollection.AddLogging(ConfigureLogging);

            //serviceCollection.AddMemoryCache();
            //serviceCollection.AddDistributedMemoryCache();

            //serviceCollection.AddDistributedSqlServerCache(options =>
            //										  {
            //											  options.ConnectionString = Configuration.GetConnectionString(Configuration.GetValue<string>("Cache:SqlServer:ConnectionStringKey"));
            //											  options.SchemaName = Configuration.GetValue<string>("Cache:SqlServer:SchemaName");
            //											  options.TableName = Configuration.GetValue<string>("Cache:SqlServer:TableName");
            //										  });

            //serviceCollection.AddDistributedRedisCache(options =>
            //                                  {
            //	                                  options.Configuration = Configuration.GetConnectionString(Configuration.GetValue<string>("Cache:Redis:Configuration"));
            //	                                  options.InstanceName = Configuration.GetValue<string>("Cache:Redis:InstanceName");
            //	                                  Configuration.GetSection("ConnectionStrings:RedisConnection").Bind(options);
            //	                                  //x options.ResolveDns();
            //                                  });

            if (StartupInjector.Instance.ExternalServicesExist)
			{
				StartupInjector.Instance.ExternalServices(serviceCollection, Configuration);
			}

			ServiceProvider = serviceCollection.BuildServiceProvider();
		}

		private void ConfigureLogging(ILoggingBuilder loggingBuilder)
		{
			loggingBuilder.AddConfiguration(Configuration.GetSection("Logging"))
						  .AddSerilog(dispose: true);
			//x loggingBuilder.AddDebug();
			//x loggingBuilder.AddConsole();

			if (StartupInjector.Instance.ExternalLoggingBuilderExists)
			{
				StartupInjector.Instance.ExternalLoggingBuilder(loggingBuilder);
			}

		}

		private void AfterRegistrations()
		{
		}

		#region Singleton

		private static readonly Lazy<Startup> Lazy = new Lazy<Startup>(() => new Startup());

		public static Startup Instance => Lazy.Value;

		private Startup()
		{
			Register();
		}

		#endregion
	}
}