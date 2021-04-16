// ApplicationAction.cs: A bar action that starts an application.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

using Morphic.Windows.Native.WindowsCom;

namespace Morphic.Client.Bar.Data.Actions
{
    using Microsoft.Extensions.Logging;
    using Microsoft.Win32;
    using Morphic.Core;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Interop;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;

    /// <summary>
    /// Action to start an application.
    /// </summary>
    [JsonTypeName("application")]
    public class ApplicationAction : BarAction
    {
        private string? exeNameValue;

        /// <summary>
        /// The actual path to the executable.
        /// </summary>
        public string? AppPath { get; set; }

        public override ImageSource? DefaultImageSource
        {
            get
            {
                if (this.AppPath != null)
                {
                    return Imaging.CreateBitmapSourceFromHIcon(
                        System.Drawing.Icon.ExtractAssociatedIcon(this.AppPath).Handle,
                        Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Start a default application. This value will be mapped locally
        /// </summary>
        [JsonProperty("default")]
        public string? DefaultAppName { get; set; }

        /// <summary>
        /// Invoke the value in `exe` as-is, via the shell (explorer). Don't resolve the path.
        /// </summary>
        [JsonProperty("shell")]
        public bool Shell { get; set; }

        /// <summary>
        /// This is a windows store app. The value of `exe` is the Application User Model ID of the app.
        /// For example, `Microsoft.WindowsCalculator_8wekyb3d8bbwe!App`
        /// </summary>
        [JsonProperty("appx")]
        public bool AppX { get; set; }

        /// <summary>
        /// true to always start a new instance. false to activate an existing instance.
        /// </summary>
        [JsonProperty("newInstance")]
        public bool NewInstance { get; set; }

        /// <summary>
        /// The initial state of the window.
        /// </summary>
        [JsonProperty("windowStyle")]
        public ProcessWindowStyle WindowStyle { get; set; } = ProcessWindowStyle.Normal;

        private static string? ConvertExeIdToExecutablePath(string exeId)
        {
            switch (exeId)
            {
                case "calculator":
                    {
                        var appPath = ApplicationAction.SearchPathEnv("calc.exe");
                        return appPath;
                    }
                case "microsoftEdge":
                    {
                        var appPath = ApplicationAction.SearchAppPaths("msedge.exe");
                        return appPath;
                    }
                case "microsoftSkype":
                    {
                        var appPath = ApplicationAction.SearchAppPaths("Skype.exe");
                        return appPath;
                    }
                default:
                    return null;
            }
        }

        /// <summary>
        /// Executable name, or the full path to it. If also providing arguments, surround the executable path with quotes.
        /// </summary>
        [JsonProperty("exe")]
        public string? ExeName
        {
            get => this.exeNameValue ?? null;
            set
            {
                this.exeNameValue = value;
                this.AppPath = null; // until we find the executable id'd by exeNameValue, AppPath should be null; it will be set to the actual executable path (or AppX identity)

                if (value != null && value.StartsWith("appx:", StringComparison.InvariantCultureIgnoreCase))
                {
                    this.AppX = true;
                    this.AppPath = value.Substring(5);
                }

                if (value != null && value.Length > 0)
                {
                    var appPath = ApplicationAction.ConvertExeIdToExecutablePath(value);
                    if (appPath != null)
                    {
                        this.AppPath = appPath;
                        App.Current.Logger.LogDebug($"Resolved exe file '{this.exeNameValue}' to '{this.AppPath ?? "(null)"}'");
                    }
                }

                this.IsAvailable = this.AppPath != null;
            }
        }

        /// <summary>
        /// Array of arguments.
        /// </summary>
        [JsonProperty("args")]
        public List<string> Arguments { get; set; } = new List<string>();

        /// <summary>
        /// The arguments, if they're passed after the exe name.
        /// </summary>
        public string? ArgumentsString { get; set; }

        /// <summary>
        /// Environment variables to set
        /// </summary>
        [JsonProperty("env")]
        public Dictionary<string, string> EnvironmentVariables { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Searches the directories in the PATH environment variable.
        /// </summary>
        /// <param name="file"></param>
        /// <returns>null if not found.</returns>
        private static string? SearchPathEnv(string file)
        {
            // OBSERVATION: the noted alternative may search some standard system directories outside of the PATH if the path env var isn't explicitly provided as an array of strings
            // Alternative: https://docs.microsoft.com/en-us/windows/win32/api/shlwapi/nf-shlwapi-pathfindonpathw
            return Environment.GetEnvironmentVariable("PATH")?
                    .Split(Path.PathSeparator)
                    .Select(p => Path.Combine(p, file))
                    .FirstOrDefault(File.Exists);
        }

        /// <summary>
        /// Searches SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths (in both HKCU and HKLM) for an executable.
        /// </summary>
        /// <param name="file"></param>
        /// <returns>null if not found.</returns>
        private static string? SearchAppPaths(string file)
        {
            string? fullPath = null;

            // Look in *\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths (giving priority to the current user, in case an entry exists in both locations)
            foreach (RegistryKey rootKey in new[] {Registry.CurrentUser, Registry.LocalMachine})
            {
                RegistryKey? key =
                    rootKey.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{file}");
                if (key != null)
                {
                    // capture the default key (which should be the full path with executable)
                    fullPath = key.GetValue(null) as string;
                    if (fullPath != null)
                    {
                        break;
                    }
                }
            }

            return fullPath;
        }

        private static IMorphicResult<string> StripExecutableFromCommand(string command)
        {
            // we need to split off the executable name from any arguments; it is either enclosed in quotes or it's everything before a space
            //
            // option 1: enclosed in quotes
            // NOTE: there may be other ways of addressing this (such as looking for a file extension on the executable file name)
            var indexOfFirstDoubleQuote = command.IndexOf('\"');
            if (indexOfFirstDoubleQuote == 0)
            {
                var indexOfSecondDoubleQuote = command.IndexOf('\"', indexOfFirstDoubleQuote + 1);
                if (indexOfSecondDoubleQuote > 1)
                {
                    command = command.Substring(indexOfFirstDoubleQuote + 1, indexOfSecondDoubleQuote - indexOfFirstDoubleQuote - 1);
                    return IMorphicResult<string>.SuccessResult(command);
                }
                else
                {
                    return IMorphicResult<string>.ErrorResult();
                }
            }
            //
            // option 2: everything before a space (or everything, if there are no spaces)
            var indexOfFirstSpace = command.IndexOf(' ');
            if (indexOfFirstSpace > 0)
            {
                command = command.Substring(0, indexOfFirstSpace);
                return IMorphicResult<string>.SuccessResult(command);
            }
            else
            {
                return IMorphicResult<string>.SuccessResult(command);
            }
        }

        private static IMorphicResult<string> GetOpenCommandForProgIdClass(string progId) 
        {
            // look up the browser progId's actual executable path (e.g. path to Edge, instead of "MSEdgeHtm")
            var browserOpenCommandRegistryKey = Registry.ClassesRoot.OpenSubKey(progId + @"\shell\open\command");
            if (browserOpenCommandRegistryKey != null)
            {
                // get the string to launch the browser (e.g. the default registry key value); this result may include arguments
                var browserOpenCommand = browserOpenCommandRegistryKey.GetValue(null) as string;
                if (browserOpenCommand != null)
                {
                    var stripExecutableFromCommandResult = ApplicationAction.StripExecutableFromCommand(browserOpenCommand);
                    if (stripExecutableFromCommandResult.IsError == true)
                    {
                        return IMorphicResult<string>.ErrorResult();
                    }
                    browserOpenCommand = stripExecutableFromCommandResult.Value!;

                    return IMorphicResult<string>.SuccessResult(browserOpenCommand);
                }
            }

            // if we could not get the open command, return failure
            return IMorphicResult<string>.ErrorResult();
        }

        private static IMorphicResult<string> GetPathToExecutableForUrlAssociation(string urlAssociation)
        {
            var userSelectedBrowserRegistryKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\Shell\Associations\UrlAssociations\" + urlAssociation + @"\UserChoice", false);
            if (userSelectedBrowserRegistryKey != null)
            {
                var progId = userSelectedBrowserRegistryKey.GetValue("ProgId") as string;
                if (progId != null)
                {
                    var getOpenCommandForProgIdClassResult = ApplicationAction.GetOpenCommandForProgIdClass(progId);
                    if (getOpenCommandForProgIdClassResult.IsError == false)
                    {
                        var browserOpenCommand = getOpenCommandForProgIdClassResult.Value!;
                        return IMorphicResult<string>.SuccessResult(browserOpenCommand);
                    }
                }
            }

            // if we could not get the open command, return failure
            return IMorphicResult<string>.ErrorResult();
        }

        private static IMorphicResult<string> GetPathToExecutableForFileExtension(string fileExtension)
        {
            var userSelectedBrowserRegistryKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\" + fileExtension + @"\UserChoice", false);
            if (userSelectedBrowserRegistryKey != null)
            {
                var progId = userSelectedBrowserRegistryKey.GetValue("ProgId") as string;
                if (progId != null)
                {
                    var getOpenCommandForProgIdClassResult = ApplicationAction.GetOpenCommandForProgIdClass(progId);
                    if (getOpenCommandForProgIdClassResult.IsError == false)
                    {
                        var browserOpenCommand = getOpenCommandForProgIdClassResult.Value!;
                        return IMorphicResult<string>.SuccessResult(browserOpenCommand);
                    }
                }
            }

            // if we could not get the open command, return failure
            return IMorphicResult<string>.ErrorResult();
        }

        private static IMorphicResult<string> GetDefaultBrowserPath()
        {
            var userSelectedBrowserRegistryKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice", false);
            if (userSelectedBrowserRegistryKey != null)
            {
                var browserProgId = userSelectedBrowserRegistryKey.GetValue("ProgId") as string;
                if (browserProgId != null)
                {
                    var getOpenCommandForProgIdClassResult = ApplicationAction.GetOpenCommandForProgIdClass(browserProgId);
                    if (getOpenCommandForProgIdClassResult.IsError == false)
                    {
                        var browserOpenCommand = getOpenCommandForProgIdClassResult.Value!;
                        return IMorphicResult<string>.SuccessResult(browserOpenCommand);
                    }
                }
            }

            // if we could not get the browser using the UserChoice registry key, try looking up the file association for an ".htm" file instead




            // if no path could be found, return failure
            return IMorphicResult<string>.ErrorResult();
        }

        protected override Task<IMorphicResult> InvokeAsyncImpl(string? source = null, bool? toggleState = null)
        {
            if (this.DefaultAppName != null)
            {
                // use the default application for this type
                switch (this.DefaultAppName!)
                {
                    case "browser":
                        {
                            string? associatedExecutablePath = null;

                            // try to get the executable for https:// urls
                            var getAssociatedExecutableForHttpUrlsResult = ApplicationAction.GetPathToExecutableForUrlAssociation("https");
                            if (getAssociatedExecutableForHttpUrlsResult.IsError == false)
                            {
                                associatedExecutablePath = getAssociatedExecutableForHttpUrlsResult.Value!;
                            }

                            // if we haven't found the default browser yet, look for the default application to open ".htm" files
                            if (associatedExecutablePath == null)
                            {
                                var getAssociatedExecutableForHtmFilesResult = ApplicationAction.GetPathToExecutableForFileExtension(".htm");
                                if (getAssociatedExecutableForHtmFilesResult.IsError == false)
                                {
                                    associatedExecutablePath = getAssociatedExecutableForHtmFilesResult.Value!;
                                }
                            }

                            // if we still haven't found the default browser, gracefully degrade by trying to use the launch process executable shortcut "https:" instead
                            if (associatedExecutablePath == null)
                            {
                                associatedExecutablePath = "https:";
                            }

                            var launchBrowserProcessResult = ApplicationAction.LaunchProcess(associatedExecutablePath, new List<string>(), new Dictionary<string, string>(), this.WindowStyle);
                            return Task.FromResult(launchBrowserProcessResult);
                        }
                    case "email":
                        {
                            var launchTarget = "mailto:";

                            var launchMailProcessResult = ApplicationAction.LaunchProcess(launchTarget, new List<string>(), new Dictionary<string, string>(), this.WindowStyle, true /* useShellExecute should be true for all protocols (e.g. 'mailto:') */);
                            return Task.FromResult(launchMailProcessResult);
                        }
                    default:
                        {
                            // unknown
                            Debug.Assert(false, "Unknown 'default' application type: " + this.DefaultAppName!);
                            return Task.FromResult(IMorphicResult.ErrorResult);
                        }
                }
            }

            // if we reach here, we need to launch the executable related to this "exe" ID
            if (string.IsNullOrEmpty(this.ExeName) || string.IsNullOrEmpty(this.AppPath))
            {
                // if we don't have an exeName ID tag, we have failed
                return Task.FromResult(IMorphicResult.ErrorResult);
            }

            if (this.AppX)
            {
                var pid = Appx.Start(this.AppPath);
                return Task.FromResult(pid > 0 ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult);
            }

            // for all other processes, launch the executable
            // pathToExecutable
            var pathToExecutable = this.AppPath;
            //
            // useShellExecute
            var useShellExecute = true; // default
            if (this.Shell)
            {
                useShellExecute = true;
            }
            // arguments
            List<string> arguments = new List<string>();
            if (this.Arguments.Count > 0)
            {
                foreach (string argument in this.Arguments)
                {
                    var resolvedString = this.ResolveString(argument, source);
                    if (resolvedString != null)
                    {
                        arguments.Add(resolvedString);
                    }
                }
            }
            else
            {
                var resolvedString = this.ResolveString(this.ArgumentsString, source);
                if (resolvedString != null)
                {
                    arguments.Add(resolvedString);
                }
            }
            //
            // environmentVariables
            Dictionary<string, string> environmentVariables = new Dictionary<string, string>();
            foreach (var (key, value) in this.EnvironmentVariables)
            {
                var resolvedString = this.ResolveString(value, source);
                if (resolvedString != null)
                {
                    environmentVariables.Add(key, resolvedString);
                } 
                else
                {
                    Debug.Assert(false, "Could not resolve environment variable: key = " + key + ", value = '" + value + "'");
                }
            }
            //
            // windowStyle
            var windowStyle = this.WindowStyle;

            var launchProcessResult = ApplicationAction.LaunchProcess(pathToExecutable, arguments, environmentVariables, windowStyle, useShellExecute);
            return Task.FromResult(launchProcessResult);
        }

        private static IMorphicResult LaunchProcess(string pathToExecutable, List<string> arguments, Dictionary<string, string> environmentVariables, ProcessWindowStyle windowStyle, bool useShellExecute = true)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = pathToExecutable,
                ErrorDialog = true,
                // This is required to start taskmgr (the UAC prompt)
                UseShellExecute = useShellExecute,
                WindowStyle = windowStyle
            };

            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            foreach (var (key, value) in environmentVariables)
            {
                startInfo.EnvironmentVariables.Add(key, value);
            }

            Process? process = Process.Start(startInfo);
            if (process != null)
            {
                return IMorphicResult.SuccessResult;
            }
            else
            {
                return IMorphicResult.ErrorResult;
            }
        }

        /// <summary>
        /// Activates a running instance of the application.
        /// </summary>
        /// <returns>false if it could not be done.</returns>
        /// <exception cref="NotImplementedException"></exception>
        private IMorphicResult ActivateInstance()
        {
            bool success = false;
            string? friendlyName = Path.GetFileNameWithoutExtension(this.AppPath);
            if (!string.IsNullOrEmpty(friendlyName))
            {
                success = Process.GetProcessesByName(friendlyName)
                    .Where(p => p.MainWindowHandle != IntPtr.Zero)
                    .OrderByDescending(p => p.StartTime)
                    .Any(process => WinApi.ActivateWindow(process.MainWindowHandle));
            }

            return success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
        }
    }
}
