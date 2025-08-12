using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Revit_FA_Tools.Core.Infrastructure.DependencyInjection;
using Revit_FA_Tools.Core.Infrastructure.UnitOfWork;
using Revit_FA_Tools.Core.Services.Interfaces;
using Revit_FA_Tools.Core.Services.Implementation;
using Revit_FA_Tools;
using Revit_FA_Tools.Core.Models.Analysis;
using Revit_FA_Tools.Core.Models.Electrical;
using Revit_FA_Tools.Core.Models.Devices;
using Revit_FA_Tools.Core.Models.Reporting;
using System.Linq;
using Revit_FA_Tools.Models;
using Autodesk.Revit.DB;

namespace Revit_FA_Tools.Core.Infrastructure.ServiceRegistration
{
    /// <summary>
    /// Service registration for dependency injection
    /// </summary>
    public static class ServiceRegistration
    {
        /// <summary>
        /// Registers all Core services
        /// </summary>
        public static IServiceCollection RegisterCoreServices(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            // Register infrastructure services
            services.AddScoped<IUnitOfWork, UnitOfWork.UnitOfWork>();

            // Register configuration services
            services.AddSingleton<FireAlarmConfiguration>(provider => 
            {
                // Load configuration from file or use default
                var config = ConfigurationService.Current ?? new FireAlarmConfiguration();
                return config;
            });

            // Register core business services
            services.AddScoped<IValidationService, UnifiedValidationService>();
            services.AddScoped<IAssignmentService, AssignmentService>();
            services.AddScoped<IParameterMappingService, UnifiedParameterMappingService>();
            services.AddScoped<IAddressingService, UnifiedAddressingService>();
            services.AddScoped<IAddressingPanelService, AddressingPanelService>();

            // Register calculation services
            services.AddScoped<IElectricalCalculationService, ElectricalCalculationService>();
            services.AddScoped<ICircuitBalancingService, CircuitBalancingService>();
            services.AddScoped<IBatteryCalculationService, BatteryCalculationService>();

            // Register reporting services
            services.AddScoped<IReportingService, ReportingService>();

            // Register logging service
            services.AddSingleton<ILoggingService, LoggingService>();

            return services;
        }

        /// <summary>
        /// Registers Revit-specific services
        /// </summary>
        public static IServiceCollection RegisterRevitServices(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            // Register Revit integration services
            services.AddScoped<IModelSyncService, ModelSyncService>();
            services.AddScoped<IRevitDataService, RevitDataService>();
            services.AddScoped<IRevitTransactionService, RevitTransactionService>();

            // Register UI services
            services.AddTransient<IDialogService, DialogService>();
            services.AddTransient<INavigationService, NavigationService>();

            return services;
        }

        /// <summary>
        /// Creates a configured service provider
        /// </summary>
        public static System.IServiceProvider CreateServiceProvider()
        {
            var services = new ServiceCollection();
            
            services.RegisterCoreServices();
            // Note: RegisterRevitServices would be called from the Revit project

            return (System.IServiceProvider)services.BuildServiceProvider();
        }

        /// <summary>
        /// Validates service registration
        /// </summary>
        public static void ValidateServices(System.IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            // Validate that all required services can be resolved
            var requiredServices = new[]
            {
                typeof(IValidationService),
                typeof(IAssignmentService),
                typeof(IParameterMappingService),
                typeof(IAddressingService),
                typeof(IUnitOfWork),
                typeof(ILoggingService)
            };

            foreach (var serviceType in requiredServices)
            {
                var service = serviceProvider.GetService(serviceType);
                if (service == null)
                {
                    throw new InvalidOperationException($"Required service {serviceType.Name} could not be resolved.");
                }
            }
        }
    }

    /// <summary>
    /// Service interfaces that need to be implemented
    /// </summary>
    public interface IElectricalCalculationService
    {
        Task<ElectricalResults> CalculateAsync(List<Revit_FA_Tools.Models.DeviceSnapshot> devices, CancellationToken cancellationToken = default, IProgress<AnalysisProgress> progress = null);
        ElectricalResults Calculate(List<Revit_FA_Tools.Models.DeviceSnapshot> devices);
    }

    public interface ICircuitBalancingService
    {
        Task<CircuitBalancingResults> BalanceCircuitsAsync(List<Revit_FA_Tools.Models.DeviceSnapshot> devices, CancellationToken cancellationToken = default);
        CircuitBalancingResults BalanceCircuits(List<Revit_FA_Tools.Models.DeviceSnapshot> devices);
    }

    public interface IBatteryCalculationService
    {
        Task<BatteryCalculationResults> CalculateBatteryRequirementsAsync(List<Revit_FA_Tools.Models.DeviceSnapshot> devices, CancellationToken cancellationToken = default);
        BatteryCalculationResults CalculateBatteryRequirements(List<Revit_FA_Tools.Models.DeviceSnapshot> devices);
    }

    public interface IReportingService
    {
        Task<byte[]> GenerateReportAsync(ComprehensiveAnalysisResults results, ReportFormat format, CancellationToken cancellationToken = default);
        byte[] GenerateReport(ComprehensiveAnalysisResults results, ReportFormat format);
        Task<bool> SaveReportAsync(string filePath, byte[] reportData, CancellationToken cancellationToken = default);
    }

    public interface ILoggingService
    {
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message, Exception exception = null);
        void LogDebug(string message);
    }

    public interface IModelSyncService
    {
        Task<bool> SyncModelAsync(ComprehensiveAnalysisResults results, CancellationToken cancellationToken = default);
        bool SyncModel(ComprehensiveAnalysisResults results);
    }

    public interface IRevitDataService
    {
        Task<List<FamilyInstance>> GetFireAlarmDevicesAsync(CancellationToken cancellationToken = default);
        List<FamilyInstance> GetFireAlarmDevices();
        Task<bool> UpdateDeviceParametersAsync(List<DeviceParameterUpdate> updates, CancellationToken cancellationToken = default);
    }

    public interface IRevitTransactionService
    {
        Task<T> ExecuteAsync<T>(Func<T> operation, string transactionName, CancellationToken cancellationToken = default);
        T Execute<T>(Func<T> operation, string transactionName);
        Task ExecuteAsync(Action operation, string transactionName, CancellationToken cancellationToken = default);
        void Execute(Action operation, string transactionName);
    }

    public interface IDialogService
    {
        Task<bool> ShowConfirmationAsync(string title, string message, CancellationToken cancellationToken = default);
        bool ShowConfirmation(string title, string message);
        Task ShowMessageAsync(string title, string message, CancellationToken cancellationToken = default);
        void ShowMessage(string title, string message);
        Task<string> ShowInputDialogAsync(string title, string prompt, string defaultValue = "", CancellationToken cancellationToken = default);
    }

    public interface INavigationService
    {
        Task NavigateToAsync(string viewName, object parameter = null, CancellationToken cancellationToken = default);
        void NavigateTo(string viewName, object parameter = null);
        Task<bool> CanNavigateBackAsync();
        bool CanNavigateBack();
        Task NavigateBackAsync(CancellationToken cancellationToken = default);
        void NavigateBack();
    }

    // Stub implementations for compilation
    // Moved to separate file - UnifiedParameterMappingService.cs

    // Moved to separate file - UnifiedAddressingService.cs

    // Moved to separate file - AddressingPanelService.cs

    // Stub implementations for other services
    public class ElectricalCalculationService : IElectricalCalculationService 
    {
        public async Task<ElectricalResults> CalculateAsync(List<Revit_FA_Tools.Models.DeviceSnapshot> devices, CancellationToken cancellationToken = default, IProgress<AnalysisProgress> progress = null)
        {
            await Task.Delay(100, cancellationToken);
            var progressReport = new AnalysisProgress { Message = "Calculating electrical loads..." };
            progress?.Report(progressReport);
            var results = new ElectricalResults();
            if (devices?.Any() == true)
            {
                foreach (var device in devices)
                {
                    results.Elements.Add(new ElementData
                    {
                        Current = 0.1,
                        Wattage = 24,
                        FamilyName = device.FamilyName ?? "Unknown",
                        LevelName = device.Level ?? "Unknown"
                    });
                }
            }
            return results;
        }

        public ElectricalResults Calculate(List<Revit_FA_Tools.Models.DeviceSnapshot> devices)
        {
            var results = new ElectricalResults();
            if (devices?.Any() == true)
            {
                foreach (var device in devices)
                {
                    results.Elements.Add(new ElementData
                    {
                        Current = 0.1,
                        Wattage = 24,
                        FamilyName = device.FamilyName ?? "Unknown",
                        LevelName = device.Level ?? "Unknown"
                    });
                }
            }
            return results;
        }
    }

    public class CircuitBalancingService : ICircuitBalancingService 
    {
        public async Task<CircuitBalancingResults> BalanceCircuitsAsync(List<Revit_FA_Tools.Models.DeviceSnapshot> devices, CancellationToken cancellationToken = default)
        {
            await Task.Delay(50, cancellationToken);
            return new CircuitBalancingResults
            {
                CircuitsCreated = (devices?.Count ?? 0) / 10 + 1,
                IsBalanced = true,
                BalancingScore = 95.0
            };
        }

        public CircuitBalancingResults BalanceCircuits(List<Revit_FA_Tools.Models.DeviceSnapshot> devices)
        {
            return new CircuitBalancingResults
            {
                CircuitsCreated = (devices?.Count ?? 0) / 10 + 1,
                IsBalanced = true,
                BalancingScore = 95.0
            };
        }
    }

    public class BatteryCalculationService : IBatteryCalculationService 
    {
        public async Task<BatteryCalculationResults> CalculateBatteryRequirementsAsync(List<Revit_FA_Tools.Models.DeviceSnapshot> devices, CancellationToken cancellationToken = default)
        {
            await Task.Delay(75, cancellationToken);
            var totalCurrent = (devices?.Count ?? 0) * 0.1;
            return new BatteryCalculationResults
            {
                RequiredCapacityAh = totalCurrent * 24,
                RecommendedBatteryType = "Sealed Lead Acid",
                BackupTimeHours = 24,
                IsAdequate = true
            };
        }

        public BatteryCalculationResults CalculateBatteryRequirements(List<Revit_FA_Tools.Models.DeviceSnapshot> devices)
        {
            var totalCurrent = (devices?.Count ?? 0) * 0.1;
            return new BatteryCalculationResults
            {
                RequiredCapacityAh = totalCurrent * 24,
                RecommendedBatteryType = "Sealed Lead Acid",
                BackupTimeHours = 24,
                IsAdequate = true
            };
        }
    }

    public class ReportingService : IReportingService 
    {
        public async Task<byte[]> GenerateReportAsync(ComprehensiveAnalysisResults results, ReportFormat format, CancellationToken cancellationToken = default)
        {
            await Task.Delay(200, cancellationToken);
            string reportContent = $"Fire Alarm Analysis Report\nGenerated: {DateTime.Now}\nAnalysis Scope: {results?.Scope ?? "Unknown"}";
            return System.Text.Encoding.UTF8.GetBytes(reportContent);
        }

        public byte[] GenerateReport(ComprehensiveAnalysisResults results, ReportFormat format)
        {
            string reportContent = $"Fire Alarm Analysis Report\nGenerated: {DateTime.Now}\nAnalysis Scope: {results?.Scope ?? "Unknown"}";
            return System.Text.Encoding.UTF8.GetBytes(reportContent);
        }

        public async Task<bool> SaveReportAsync(string filePath, byte[] reportData, CancellationToken cancellationToken = default)
        {
            try
            {
                await Task.Run(() => System.IO.File.WriteAllBytes(filePath, reportData), cancellationToken);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
    public class LoggingService : ILoggingService 
    {
        public void LogInfo(string message) 
        {
            System.Diagnostics.Debug.WriteLine($"INFO: {message}");
        }
        
        public void LogWarning(string message) 
        {
            System.Diagnostics.Debug.WriteLine($"WARNING: {message}");
        }
        
        public void LogError(string message, Exception exception = null) 
        {
            System.Diagnostics.Debug.WriteLine($"ERROR: {message}");
            if (exception != null)
                System.Diagnostics.Debug.WriteLine($"Exception: {exception}");
        }
        
        public void LogDebug(string message) 
        {
            System.Diagnostics.Debug.WriteLine($"DEBUG: {message}");
        }
    }

    public class ModelSyncService : IModelSyncService 
    {
        public async Task<bool> SyncModelAsync(ComprehensiveAnalysisResults results, CancellationToken cancellationToken = default)
        {
            await Task.Delay(100, cancellationToken);
            return results != null;
        }

        public bool SyncModel(ComprehensiveAnalysisResults results)
        {
            return results != null;
        }
    }

    public class RevitDataService : IRevitDataService 
    {
        public async Task<List<FamilyInstance>> GetFireAlarmDevicesAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(50, cancellationToken);
            return new List<FamilyInstance>();
        }

        public List<FamilyInstance> GetFireAlarmDevices()
        {
            return new List<FamilyInstance>();
        }

        public async Task<bool> UpdateDeviceParametersAsync(List<DeviceParameterUpdate> updates, CancellationToken cancellationToken = default)
        {
            await Task.Delay(100, cancellationToken);
            return updates?.Count > 0;
        }
    }

    public class RevitTransactionService : IRevitTransactionService 
    {
        public async Task<T> ExecuteAsync<T>(Func<T> operation, string transactionName, CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken);
            return operation != null ? operation() : default(T);
        }

        public T Execute<T>(Func<T> operation, string transactionName)
        {
            return operation != null ? operation() : default(T);
        }

        public async Task ExecuteAsync(Action operation, string transactionName, CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken);
            operation?.Invoke();
        }

        public void Execute(Action operation, string transactionName)
        {
            operation?.Invoke();
        }
    }

    public class DialogService : IDialogService 
    {
        public async Task<bool> ShowConfirmationAsync(string title, string message, CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken);
            return true; // Default to Yes/OK for automation
        }

        public bool ShowConfirmation(string title, string message)
        {
            return true; // Default to Yes/OK for automation
        }

        public async Task ShowMessageAsync(string title, string message, CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken);
            System.Diagnostics.Debug.WriteLine($"Dialog - {title}: {message}");
        }

        public void ShowMessage(string title, string message)
        {
            System.Diagnostics.Debug.WriteLine($"Dialog - {title}: {message}");
        }

        public async Task<string> ShowInputDialogAsync(string title, string prompt, string defaultValue = "", CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken);
            return defaultValue;
        }
    }

    public class NavigationService : INavigationService 
    {
        public async Task NavigateToAsync(string viewName, object parameter = null, CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken);
            System.Diagnostics.Debug.WriteLine($"Navigating to: {viewName}");
        }

        public void NavigateTo(string viewName, object parameter = null)
        {
            System.Diagnostics.Debug.WriteLine($"Navigating to: {viewName}");
        }

        public async Task<bool> CanNavigateBackAsync()
        {
            await Task.Delay(1);
            return true;
        }

        public bool CanNavigateBack()
        {
            return true;
        }

        public async Task NavigateBackAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken);
            System.Diagnostics.Debug.WriteLine("Navigating back");
        }

        public void NavigateBack()
        {
            System.Diagnostics.Debug.WriteLine("Navigating back");
        }
    }
}