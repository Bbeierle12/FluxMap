using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace NetWatch.Desktop
{
    public class Program
    {
        [System.Runtime.InteropServices.DllImport("Microsoft.ui.xaml.dll")]
        [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.SafeDirectories)]
        private static extern void XamlCheckProcessRequirements();

        [STAThread]
        public static void Main(string[] args)
        {
            var bootstrap = TryLoadBootstrap();
            InvokeBootstrap(bootstrap, "Initialize", 0x00010005u);
            XamlCheckProcessRequirements();
            try
            {
                Application.Start(_ => new App());
            }
            finally
            {
                InvokeBootstrap(bootstrap, "Shutdown");
            }
        }

        private static Type? TryLoadBootstrap()
        {
            var localPath = Path.Combine(AppContext.BaseDirectory, "Microsoft.WindowsAppRuntime.Bootstrap.Net.dll");
            if (!File.Exists(localPath))
            {
                WriteBootstrapLog($"Bootstrap DLL not found at {localPath}");
                return null;
            }

            var assembly = Assembly.LoadFrom(localPath);
            var bootstrap = assembly.GetType("Microsoft.Windows.ApplicationModel.DynamicDependency.Bootstrap")
                           ?? assembly.GetType("Microsoft.WindowsAppRuntime.Bootstrap")
                           ?? assembly.GetType("Microsoft.WindowsAppRuntime.Bootstrap.Net.Bootstrap")
                           ?? assembly.GetType("Microsoft.WindowsAppRuntime.Bootstrap.Net");
            WriteBootstrapLog(bootstrap is null
                ? "Bootstrap type not found."
                : $"Bootstrap type: {bootstrap.FullName}");
            if (bootstrap is null)
            {
                try
                {
                    var names = assembly.GetTypes()
                        .Select(t => t.FullName)
                        .Where(n => n is not null && n.Contains("Bootstrap", StringComparison.OrdinalIgnoreCase))
                        .Take(10)
                        .ToArray();
                    if (names.Length == 0)
                    {
                        WriteBootstrapLog("No bootstrap-related types found in assembly.");
                    }
                    else
                    {
                        WriteBootstrapLog("Bootstrap-related types: " + string.Join(", ", names));
                    }
                }
                catch (Exception ex)
                {
                    WriteBootstrapLog($"Failed to enumerate bootstrap types: {ex.GetType().Name}");
                }
            }
            return bootstrap;
        }

        private static void InvokeBootstrap(Type? bootstrap, string method, params object[] args)
        {
            if (bootstrap is null)
            {
                WriteBootstrapLog($"Bootstrap method '{method}' skipped (type missing).");
                return;
            }

            var argTypes = Array.ConvertAll(args, a => a.GetType());
            MethodInfo? target = null;
            if (method == "Initialize" && args.Length == 1 && args[0] is uint)
            {
                var tag = Environment.GetEnvironmentVariable("NETWATCH_BOOTSTRAP_TAG");
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    target = bootstrap.GetMethod(method, new[] { typeof(uint), typeof(string) });
                    if (target is not null)
                    {
                        var value = string.Equals(tag, "__null__", StringComparison.OrdinalIgnoreCase)
                            ? null
                            : tag;
                        args = new object?[] { args[0], value };
                    }
                }
            }
            target ??= bootstrap.GetMethod(method, argTypes);
            if (target is null)
            {
                WriteBootstrapLog($"Bootstrap method '{method}' not found.");
                return;
            }
            WriteBootstrapLog($"Invoking {bootstrap.FullName}.{method}.");
            target?.Invoke(null, args);
        }

        private static void WriteBootstrapLog(string message)
        {
            try
            {
                var root = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "NetWatch");
                Directory.CreateDirectory(root);
                File.AppendAllText(Path.Combine(root, "bootstrap.log"),
                    $"{DateTime.UtcNow:O} {message}{Environment.NewLine}");
            }
            catch
            {
                // Ignore logging errors to avoid blocking startup.
            }
        }
    }
}
