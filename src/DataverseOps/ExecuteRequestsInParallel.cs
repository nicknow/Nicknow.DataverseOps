using System.Collections.Concurrent;
using Microsoft.Xrm.Sdk;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk.Messages;
using System.Diagnostics;


namespace DataverseOps
{
    /// <summary>
    /// Generic class for executing Dataverse requests in parallel
    /// </summary>
    public class ExecuteRequestsInParallel
    {
        private readonly ServiceClient _serviceClient;
        private readonly bool _captureTimingData;
        private readonly DVOILogger _logger;
        private readonly int _maxParallelOperations;

        /// <summary>
        /// Creates a reusable object that will execute OrganizationRequests to Dataverse in parallel
        /// </summary>
        /// <param name="serviceClient">A Dataverse Service Client object</param>
        /// <param name="maxParallelOperations">(Optional) Maximum number of operations to execute in parallel</param>    
        /// <param name="logger">(Optional) Object to allow logging</param>
        /// <param name="captureTimingData">(Optional) Whether to capture timing data for execution. Default is false (no timing data captured)</param>
        /// <exception cref="ArgumentNullException"></exception>
        public ExecuteRequestsInParallel(ServiceClient serviceClient, int maxParallelOperations = 8, ILogger? logger = null, bool captureTimingData = false)
        {

            _serviceClient = serviceClient ?? throw new ArgumentNullException(nameof(serviceClient));
            _captureTimingData = captureTimingData;
            _maxParallelOperations = maxParallelOperations > 0 ? maxParallelOperations : 8;


            // Initialize custom logger implementation to allow for logging to be optional
            if (logger != null)
            {
                _logger = new DVOILogger(logger);
            }
            else
            {
                _logger = new DVOILogger();
            }


        }

        /// <summary>
        /// Executes OrganizationRequest calls to Dataverse in parallel
        /// </summary>
        /// <typeparam name="TRequest">Type of OrganizationRequest</typeparam>
        /// <typeparam name="TResponse">Type of OrganizationResponse</typeparam>
        /// <param name="requests">An enumerable list of OrganizationRequest objects to be executed</param>
        /// <param name="referenceKeySelector">Optional function to extract a reference value from each request for tracking purposes</param>
        /// <returns>ExecutionResult containing all results, success/error counts, and timing information</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public ExecutionResult<TResponse> ExecuteRequests<TRequest, TResponse>(
            IEnumerable<TRequest> requests,
            Func<TRequest, string>? referenceKeySelector = null)
            where TRequest : OrganizationRequest
            where TResponse : OrganizationResponse
        {
            if (requests == null)
                throw new ArgumentNullException(nameof(requests));

            var requestList = requests.ToList();
            var result = new ExecutionResult<TResponse>
            {
                TotalProcessed = requestList.Count,
                StartTime = DateTime.UtcNow
            };

            if (!requestList.Any())
            {
                result.EndTime = DateTime.UtcNow;
                return result;
            }

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = _maxParallelOperations
            };

            var results = new ConcurrentBag<SingleExecutionResult<TResponse>>();

            _logger.LogInformation($"Starting parallel execution of {requestList.Count} requests with max parallel operations: {parallelOptions.MaxDegreeOfParallelism}");

            Parallel.ForEach(requestList, parallelOptions, request =>
            {
                var singleResult = ExecuteSingleRequest<TRequest, TResponse>(request, referenceKeySelector);
                results.Add(singleResult);
            });            

            result.Results = results.ToList();
            result.EndTime = DateTime.UtcNow;

            _logger.LogInformation($"Parallel execution completed: {result.Results.Count} requests processed in {result.Duration.TotalSeconds} s");

            // Process results after parallel execution is complete
            foreach (var singleResult in results)
            {
                if (singleResult.IsSuccess)
                {
                    result.SuccessCount++;
                }
                else
                {
                    result.ErrorCount++;
                }
            }

            return result;
        }

        /// <summary>
        /// Executes OrganizationRequest calls to Dataverse in batches, with batches executed in parallel
        /// </summary>
        /// <typeparam name="TRequest">Type of OrganizationRequest</typeparam>
        /// <typeparam name="TResponse">Type of OrganizationResponse</typeparam>
        /// <param name="requests">An enumerable list of OrganizationRequest objects to be executed</param>
        /// <param name="requestsPerBatch">Number of requests to include in each batch</param>
        /// <param name="referenceKeySelector">Optional function to extract a reference value from each request for tracking purposes</param>
        /// <param name="continueOnError">Whether to continue processing remaining batches if a batch fails</param>
        /// <returns>ExecutionResult containing all results, success/error counts, and timing information</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public ExecutionResult<ExecuteMultipleResponse> ExecuteRequests<TRequest, TResponse>(
            IEnumerable<TRequest> requests,
            int requestsPerBatch,
            Func<TRequest, string>? referenceKeySelector = null,
            bool continueOnError = true)
            where TRequest : OrganizationRequest
            where TResponse : OrganizationResponse
        {
            if (requests == null)
                throw new ArgumentNullException(nameof(requests));

            if (requestsPerBatch <= 0)
                throw new ArgumentException("Requests per batch must be greater than 0", nameof(requestsPerBatch));

            var requestList = requests.ToList();
            var result = new ExecutionResult<ExecuteMultipleResponse>
            {
                TotalProcessed = requestList.Count,
                StartTime = DateTime.UtcNow
            };

            if (!requestList.Any())
            {
                result.EndTime = DateTime.UtcNow;
                return result;
            }

            _logger.LogInformation($"Preparing batches for execution: {requestList.Count} requests in batches of {requestsPerBatch}");

            // Create batches
            var batches = CreateBatches(requestList, requestsPerBatch, referenceKeySelector, continueOnError);

            _logger.LogInformation($"Created {batches.Count} batches for parallel execution");

            // Execute batches in parallel using the existing ExecuteRequests method
            var batchResult = ExecuteRequests<ExecuteMultipleRequest, ExecuteMultipleResponse>(
                batches,
                batch => $"Batch_{batches.IndexOf(batch) + 1}"
            );

            // Transform the batch results back to the expected format
            result.Results = batchResult.Results;
            result.SuccessCount = batchResult.SuccessCount;
            result.ErrorCount = batchResult.ErrorCount;
            result.EndTime = batchResult.EndTime;

            if (_logger.IsLoggingEnabled)
            {
                _logger.LogInformation($"Batch execution completed: {result.SuccessCount} successful batches, {result.ErrorCount} failed batches");

                // Log individual request results from successful batches
                var totalSuccessfulRequests = 0;
                var totalFailedRequests = 0;

                foreach (var batchResultItem in result.Results.Where(r => r.IsSuccess && r.Response != null))
                {
                    var executeMultipleResponse = batchResultItem.Response!;
                    foreach (var response in executeMultipleResponse.Responses)
                    {
                        if (response.Fault == null)
                            totalSuccessfulRequests++;
                        else
                            totalFailedRequests++;
                    }
                }

                _logger.LogInformation($"Individual request results: {totalSuccessfulRequests} successful, {totalFailedRequests} failed");
            }

            return result;
        }

        private List<ExecuteMultipleRequest> CreateBatches<TRequest>(
            List<TRequest> requests,
            int requestsPerBatch,
            Func<TRequest, string>? referenceKeySelector,
            bool continueOnError)
            where TRequest : OrganizationRequest
        {
            var batches = new List<ExecuteMultipleRequest>();
            var requestsWithMetadata = requests.Select((request, index) => new
            {
                Request = request,
                Index = index,
                ReferenceKey = referenceKeySelector?.Invoke(request) ?? $"Request_{index}"
            }).ToList();

            for (int i = 0; i < requestsWithMetadata.Count; i += requestsPerBatch)
            {
                var batchRequests = requestsWithMetadata
                    .Skip(i)
                    .Take(requestsPerBatch)
                    .ToList();

                var executeMultipleRequest = new ExecuteMultipleRequest
                {
                    Settings = new ExecuteMultipleSettings
                    {
                        ContinueOnError = continueOnError,
                        ReturnResponses = true
                    },
                    Requests = new OrganizationRequestCollection()
                };

                foreach (var requestInfo in batchRequests)
                {
                    executeMultipleRequest.Requests.Add(requestInfo.Request);
                }

                batches.Add(executeMultipleRequest);

                _logger.LogTrace($"Created batch {batches.Count} with {batchRequests.Count} requests");
            }

            return batches;
        }

        private SingleExecutionResult<TResponse> ExecuteSingleRequest<TRequest, TResponse>(
            TRequest request,
            Func<TRequest, string>? referenceKeySelector = null)
            where TRequest : OrganizationRequest
            where TResponse : OrganizationResponse
        {
            var transactionId = Guid.NewGuid();

            try
            {

                var sw = _captureTimingData ? new Stopwatch() : null;

                request.RequestId = transactionId; // Set the internal transaction ID for tracking

                _logger.LogTrace($"START | Executing request: {request.GetType().Name}. Internal Transaction Id: {transactionId}");

                var startTime = DateTime.Now;
                TResponse? response = null;
               
                // Use cloned instance to ensure thread safety
                using (var instanceServices = _serviceClient.Clone())
                {
                    sw?.Start();
                    response = instanceServices.Execute(request) as TResponse;
                    sw?.Stop();
                }

                var stopTime = DateTime.Now;

                _logger.LogTrace($"FINISH | Execute Success: {request.GetType().Name}. Internal Transaction Id: {transactionId}");


                var executionTimeLogging = (_captureTimingData
                    ? $". Execution time: {sw?.Elapsed.TotalMilliseconds} ms"
                    : string.Empty);
                _logger.LogInformation($"SUCCESS | Dataverse Request executed successfully: {request.GetType().Name}. Internal Transaction Id: {transactionId}. Response: {response?.ToString() ?? "null"}{executionTimeLogging}");

                return new SingleExecutionResult<TResponse>
                {
                    IsSuccess = true,
                    Response = response,
                    ReferenceValue = referenceKeySelector?.Invoke(request) ?? string.Empty,
                    TimingData = _captureTimingData ? new SingleExecutionTimingData
                    {
                        StartTime = startTime,
                        EndTime = stopTime,
                        SdkTransactionTimeMilliseconds = sw?.ElapsedMilliseconds ?? 0
                    } : null,
                    InternalTransactionId = transactionId
                };
            }
            catch (Exception ex)
            {
                var errorMessage = $"ERROR | Failed to execute {request.GetType().Name}: {ex.Message}. Internal Transaction Id: {transactionId}";

                _logger.LogError(ex, errorMessage);

                return new SingleExecutionResult<TResponse>
                {
                    IsSuccess = false,
                    ErrorMessage = errorMessage,
                    ReferenceValue = referenceKeySelector?.Invoke(request) ?? string.Empty,
                    Exception = ex,
                    TimingData = null,
                    InternalTransactionId = transactionId
                };
            }

        }

    }
}