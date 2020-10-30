﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Morphic.ManualTester
{
    using Settings.SolutionsRegistry;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public IServiceProvider ServiceProvider { get; private set; } = null!;
        public IConfiguration Configuration { get; private set; } = null!;
        private ILogger<MainWindow> logger = null!;
        public string fileContent = "";
        public string filePath = "";
        public bool AutoApply = true;

        public MainWindow()
        {
            InitializeComponent();
            OnStartup();
        }

        /// <summary>
        /// Configure the dependency injection system with services
        /// </summary>
        /// <param name="services"></param>
        private void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(ConfigureLogging);
            services.AddSingleton<IServiceCollection>(services);
            services.AddSingleton<IServiceProvider>(provider => provider);
            services.AddSolutionsRegistryServices();
        }

        void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Exception ex = e.Exception;
            logger.LogError("handled uncaught exception: {msg}", ex.Message);
            logger.LogError(ex.StackTrace);

            Dictionary<String, String> extraData = new Dictionary<string, string>();

            MessageBox.Show("An unhandled exception just occurred: " + e.Exception.Message, "Exception Sample", MessageBoxButton.OK, MessageBoxImage.Warning);
            // This prevents the exception from crashing the application
            e.Handled = true;
        }

        /// <summary>
        /// Configure the logging for the application
        /// </summary>
        /// <param name="logging"></param>
        private void ConfigureLogging(ILoggingBuilder logging)
        {
            logging.SetMinimumLevel(LogLevel.Debug);
        }

        protected void OnStartup()
        {
            var collection = new ServiceCollection();
            ConfigureServices(collection);
            ServiceProvider = collection.BuildServiceProvider();
            logger = ServiceProvider.GetRequiredService<ILogger<MainWindow>>();
            LoadNewRegistry(new object(), new RoutedEventArgs());
        }

        private async void LoadNewRegistry(object sender, RoutedEventArgs e)
        {
            var filedialog = new OpenFileDialog();
            filedialog.InitialDirectory = "Documents";
            filedialog.Filter = "json files (*.json, *.json5)|*.json;*.json5|All files (*.*)|*.*";
            if(filedialog.ShowDialog() == true)
            {
                this.LoadedFileName.Text = "...";
                this.SettingsList.Items.Clear();
                var loadtext = new TextBlock();
                loadtext.Text = "LOADING...";
                this.SettingsList.Items.Add(loadtext);

                try
                {
                    Solutions solutions = Solutions.FromFile(this.ServiceProvider, filedialog.FileName);
                    this.LoadedFileName.Text = "Loaded file " + filedialog.FileName;
                    this.SettingsList.Items.Clear();
                    foreach(var solution in solutions.All.Values)
                    {
                        SolutionHeader header = new SolutionHeader(this, solution);
                        SettingsList.Items.Add(header);
                    }
                }
                catch
                {
                    this.LoadedFileName.Text = "ERROR";
                    this.SettingsList.Items.Clear();
                    var feature = new TextBlock();
                    feature.Text = "AN ERROR HAS OCCURRED. TRY A DIFFERENT FILE";
                    this.SettingsList.Items.Add(feature);
                }
            }
        }

        private void ToggleAutoApply(object sender, RoutedEventArgs e)
        {
            if((AutoApplyToggle.IsChecked) != null && ApplySettings != null)
            {
                if((bool)AutoApplyToggle.IsChecked)
                {
                    this.AutoApply = true;
                    ApplySettings.Visibility = Visibility.Hidden;
                }
                else
                {
                    this.AutoApply = false;
                    ApplySettings.Visibility = Visibility.Visible;
                }
            }
        }

        private void ApplyAllSettings(object sender, RoutedEventArgs e)
        {
            foreach(var element in this.SettingsList.Items)
            {
                try
                {
                    SolutionHeader? header = (SolutionHeader?)element;
                    if (header != null)
                    {
                        header.ApplyAllSettings();
                    }
                }
                catch { }
            }
        }
    }
}
