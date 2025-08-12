using System;
using System.Threading.Tasks;
using Revit_FA_Tools.Core.Services.Interfaces;
using Revit_FA_Tools.Core.Infrastructure.ServiceRegistration;

namespace Revit_FA_Tools.Core.Infrastructure.ErrorHandling
{
    /// <summary>
    /// Centralized exception handling for services
    /// </summary>
    public class ServiceExceptionHandler
    {
        private readonly ILoggingService _loggingService;

        public ServiceExceptionHandler(ILoggingService loggingService)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        /// <summary>
        /// Handles service exceptions with logging and user-friendly error messages
        /// </summary>
        public async Task<ServiceResult<T>> HandleAsync<T>(Func<Task<T>> operation, string operationName)
        {
            try
            {
                var result = await operation();
                return ServiceResult<T>.Success(result);
            }
            catch (ArgumentNullException ex)
            {
                _loggingService.LogError($"Invalid argument in {operationName}", ex);
                return ServiceResult<T>.Failure(
                    ServiceErrorCode.InvalidArgument, 
                    $"Invalid input provided to {operationName}",
                    ex);
            }
            catch (ArgumentException ex)
            {
                _loggingService.LogError($"Invalid argument in {operationName}", ex);
                return ServiceResult<T>.Failure(
                    ServiceErrorCode.InvalidArgument, 
                    ex.Message,
                    ex);
            }
            catch (InvalidOperationException ex)
            {
                _loggingService.LogError($"Invalid operation in {operationName}", ex);
                return ServiceResult<T>.Failure(
                    ServiceErrorCode.InvalidOperation, 
                    ex.Message,
                    ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                _loggingService.LogError($"Access denied in {operationName}", ex);
                return ServiceResult<T>.Failure(
                    ServiceErrorCode.AccessDenied, 
                    "You do not have permission to perform this operation",
                    ex);
            }
            catch (TimeoutException ex)
            {
                _loggingService.LogError($"Timeout in {operationName}", ex);
                return ServiceResult<T>.Failure(
                    ServiceErrorCode.Timeout, 
                    "The operation timed out. Please try again.",
                    ex);
            }
            catch (OutOfMemoryException ex)
            {
                _loggingService.LogError($"Out of memory in {operationName}", ex);
                return ServiceResult<T>.Failure(
                    ServiceErrorCode.SystemResource, 
                    "The system is low on memory. Please close other applications and try again.",
                    ex);
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Unexpected error in {operationName}", ex);
                return ServiceResult<T>.Failure(
                    ServiceErrorCode.UnknownError, 
                    "An unexpected error occurred. Please contact support if this persists.",
                    ex);
            }
        }

        /// <summary>
        /// Handles service operations that don't return a value
        /// </summary>
        public async Task<ServiceResult> HandleAsync(Func<Task> operation, string operationName)
        {
            var result = await HandleAsync(async () =>
            {
                await operation();
                return true;
            }, operationName);

            return new ServiceResult
            {
                IsSuccess = result.IsSuccess,
                ErrorCode = result.ErrorCode,
                ErrorMessage = result.ErrorMessage,
                Exception = result.Exception
            };
        }

        /// <summary>
        /// Handles synchronous service operations
        /// </summary>
        public ServiceResult<T> Handle<T>(Func<T> operation, string operationName)
        {
            try
            {
                var result = operation();
                return ServiceResult<T>.Success(result);
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error in {operationName}", ex);
                return ServiceResult<T>.Failure(
                    GetErrorCode(ex),
                    GetUserFriendlyMessage(ex, operationName),
                    ex);
            }
        }

        private ServiceErrorCode GetErrorCode(Exception ex)
        {
            return ex switch
            {
                ArgumentNullException => ServiceErrorCode.InvalidArgument,
                ArgumentException => ServiceErrorCode.InvalidArgument,
                InvalidOperationException => ServiceErrorCode.InvalidOperation,
                UnauthorizedAccessException => ServiceErrorCode.AccessDenied,
                TimeoutException => ServiceErrorCode.Timeout,
                OutOfMemoryException => ServiceErrorCode.SystemResource,
                _ => ServiceErrorCode.UnknownError
            };
        }

        private string GetUserFriendlyMessage(Exception ex, string operationName)
        {
            return ex switch
            {
                ArgumentNullException => $"Invalid input provided to {operationName}",
                ArgumentException => ex.Message,
                InvalidOperationException => ex.Message,
                UnauthorizedAccessException => "You do not have permission to perform this operation",
                TimeoutException => "The operation timed out. Please try again.",
                OutOfMemoryException => "The system is low on memory. Please close other applications and try again.",
                _ => "An unexpected error occurred. Please contact support if this persists."
            };
        }
    }

    /// <summary>
    /// Service operation result with error handling
    /// </summary>
    public class ServiceResult
    {
        public bool IsSuccess { get; set; }
        public ServiceErrorCode ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
        public Exception Exception { get; set; }

        public static ServiceResult Success()
        {
            return new ServiceResult { IsSuccess = true };
        }

        public static ServiceResult Failure(ServiceErrorCode errorCode, string message, Exception exception = null)
        {
            return new ServiceResult
            {
                IsSuccess = false,
                ErrorCode = errorCode,
                ErrorMessage = message,
                Exception = exception
            };
        }
    }

    /// <summary>
    /// Service operation result with return value
    /// </summary>
    public class ServiceResult<T> : ServiceResult
    {
        public T Data { get; set; }

        public static ServiceResult<T> Success(T data)
        {
            return new ServiceResult<T>
            {
                IsSuccess = true,
                Data = data
            };
        }

        public static new ServiceResult<T> Failure(ServiceErrorCode errorCode, string message, Exception exception = null)
        {
            return new ServiceResult<T>
            {
                IsSuccess = false,
                ErrorCode = errorCode,
                ErrorMessage = message,
                Exception = exception
            };
        }
    }

    /// <summary>
    /// Service error codes for categorizing errors
    /// </summary>
    public enum ServiceErrorCode
    {
        None = 0,
        InvalidArgument = 1000,
        InvalidOperation = 2000,
        AccessDenied = 3000,
        NotFound = 4000,
        Timeout = 5000,
        SystemResource = 6000,
        ValidationFailed = 7000,
        BusinessLogicError = 8000,
        ExternalServiceError = 9000,
        UnknownError = 9999
    }

    /// <summary>
    /// Extension methods for easier error handling
    /// </summary>
    public static class ServiceResultExtensions
    {
        /// <summary>
        /// Executes an action if the service result is successful
        /// </summary>
        public static ServiceResult<T> OnSuccess<T>(this ServiceResult<T> result, Action<T> action)
        {
            if (result.IsSuccess && result.Data != null)
            {
                action(result.Data);
            }
            return result;
        }

        /// <summary>
        /// Executes an action if the service result failed
        /// </summary>
        public static ServiceResult<T> OnFailure<T>(this ServiceResult<T> result, Action<ServiceErrorCode, string> action)
        {
            if (!result.IsSuccess)
            {
                action(result.ErrorCode, result.ErrorMessage);
            }
            return result;
        }

        /// <summary>
        /// Transforms the result data if successful
        /// </summary>
        public static ServiceResult<TOut> Map<TIn, TOut>(this ServiceResult<TIn> result, Func<TIn, TOut> mapper)
        {
            if (result.IsSuccess && result.Data != null)
            {
                try
                {
                    return ServiceResult<TOut>.Success(mapper(result.Data));
                }
                catch (Exception ex)
                {
                    return ServiceResult<TOut>.Failure(ServiceErrorCode.UnknownError, ex.Message, ex);
                }
            }

            return ServiceResult<TOut>.Failure(result.ErrorCode, result.ErrorMessage, result.Exception);
        }
    }
}