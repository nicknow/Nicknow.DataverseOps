using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Extensions.Logging;

namespace DataverseOps
{
    /// <summary>
    /// Convenience wrapper for common Dataverse operations
    /// </summary>
    public class CUDOperations
    {
        private readonly ExecuteRequestsInParallel _executor;

        public CUDOperations(ServiceClient serviceClient, int maxParallelOperations = 8, ILogger? logger = null, bool captureTimingData = false)
        {
            _executor = new ExecuteRequestsInParallel(serviceClient, maxParallelOperations, logger, captureTimingData);
        }

        #region Parallel Operations

        /// <summary>
        /// Creates multiple entities in parallel
        /// </summary>
        /// <param name="entities">Entities to create</param>
        /// <param name="referenceAttribute">Optional attribute name to use as reference value</param>
        /// <returns>Creation results</returns>
        public ExecutionResult<CreateResponse> CreateEntities(
            IEnumerable<Entity> entities,
            string referenceAttribute = "")
        {
            var requests = entities.Select(entity => new CreateRequest { Target = entity });

            Func<CreateRequest, string>? referenceSelector = null;
            if (!string.IsNullOrEmpty(referenceAttribute))
            {
                referenceSelector = req => req.Target.Contains(referenceAttribute)
                    ? req.Target[referenceAttribute]?.ToString() ?? string.Empty
                    : string.Empty;
            }

            return _executor.ExecuteRequests<CreateRequest, CreateResponse>(requests, referenceSelector);
        }

        /// <summary>
        /// Updates multiple entities in parallel
        /// </summary>
        /// <param name="entities">Entities to update</param>
        /// <param name="referenceAttribute">Optional attribute name to use as reference value</param>
        /// <returns>Update results</returns>
        public ExecutionResult<UpdateResponse> UpdateEntities(
            IEnumerable<Entity> entities,
            string referenceAttribute = "")
        {
            var requests = entities.Select(entity => new UpdateRequest { Target = entity });

            Func<UpdateRequest, string>? referenceSelector = null;
            if (!string.IsNullOrEmpty(referenceAttribute))
            {
                referenceSelector = req => req.Target.Contains(referenceAttribute)
                    ? req.Target[referenceAttribute]?.ToString() ?? string.Empty
                    : string.Empty;
            }

            return _executor.ExecuteRequests<UpdateRequest, UpdateResponse>(requests, referenceSelector);
        }

        /// <summary>
        /// Upserts multiple entities in parallel
        /// </summary>
        /// <param name="entities">Entities to upsert</param>
        /// <param name="referenceAttribute">Optional attribute name to use as reference value</param>
        /// <returns>Upsert results</returns>
        public ExecutionResult<UpsertResponse> UpsertEntities(
            IEnumerable<Entity> entities,
            string referenceAttribute = "")
        {
            var requests = entities.Select(entity => new UpsertRequest { Target = entity });

            Func<UpsertRequest, string>? referenceSelector = null;
            if (!string.IsNullOrEmpty(referenceAttribute))
            {
                referenceSelector = req => req.Target.Contains(referenceAttribute)
                    ? req.Target[referenceAttribute]?.ToString() ?? string.Empty
                    : string.Empty;
            }

            return _executor.ExecuteRequests<UpsertRequest, UpsertResponse>(requests, referenceSelector);
        }

        /// <summary>
        /// Deletes multiple entities in parallel
        /// </summary>
        /// <param name="entityReferences">Entity references to delete</param>
        /// <returns>Delete results</returns>
        public ExecutionResult<DeleteResponse> DeleteEntities(IEnumerable<EntityReference> entityReferences)
        {
            var requests = entityReferences.Select(entityRef => new DeleteRequest
            {
                Target = entityRef
            });

            Func<DeleteRequest, string> referenceSelector = req =>
                $"{req.Target.LogicalName}:{req.Target.Id}";

            return _executor.ExecuteRequests<DeleteRequest, DeleteResponse>(requests, referenceSelector);
        }

        /// <summary>
        /// Deletes multiple entities in parallel by entity name and IDs
        /// </summary>
        /// <param name="entityLogicalName">Logical name of the entity</param>
        /// <param name="entityIds">IDs of entities to delete</param>
        /// <returns>Delete results</returns>
        public ExecutionResult<DeleteResponse> DeleteEntities(string entityLogicalName, IEnumerable<Guid> entityIds)
        {
            var entityReferences = entityIds.Select(id => new EntityReference(entityLogicalName, id));
            return DeleteEntities(entityReferences);
        }

        #endregion

        #region Parallel Batch Operations

        /// <summary>
        /// Create multiple entities in parallel with batching support
        /// </summary>
        /// <param name="entities">Entities to create</param>
        /// <param name="requestsPerBatch">Number of create requests per batch</param>
        /// <param name="referenceAttribute">Optional attribute name to use as reference for tracking</param>
        /// <param name="continueOnError">Whether to continue processing remaining batches if a batch fails</param>
        /// <returns>ExecutionResult containing all create results</returns>
        public ExecutionResult<CreateResponse> CreateEntities(
            IEnumerable<Entity> entities,
            int requestsPerBatch,
            string referenceAttribute = "",
            bool continueOnError = true)
        {
            var requests = entities.Select(entity => new CreateRequest { Target = entity }).ToList();
            Func<CreateRequest, string>? referenceSelector = null;
            if (!string.IsNullOrEmpty(referenceAttribute))
            {
                referenceSelector = req => req.Target.Contains(referenceAttribute)
                    ? req.Target[referenceAttribute]?.ToString() ?? string.Empty
                    : string.Empty;
            }
            var batchResult = _executor.ExecuteRequests<CreateRequest, CreateResponse>(requests, requestsPerBatch, referenceSelector, continueOnError);

            return TransformBatchResultToIndividualResults<CreateRequest, CreateResponse>(batchResult, requests, referenceSelector);

        }

        /// <summary>
        /// Updates multiple entities in parallel with batching support
        /// </summary>
        /// <param name="entities">Entities to update</param>
        /// <param name="requestsPerBatch">Number of create requests per batch</param>
        /// <param name="referenceAttribute">Optional attribute name to use as reference for tracking</param>
        /// <param name="continueOnError">Whether to continue processing remaining batches if a batch fails</param>
        /// <returns>ExecutionResult containing all update results</returns>
        public ExecutionResult<UpdateResponse> UpdateEntities(
            IEnumerable<Entity> entities,
            int requestsPerBatch,
            string referenceAttribute = "",
            bool continueOnError = true)
        {
            var requests = entities.Select(entity => new UpdateRequest { Target = entity });

            Func<UpdateRequest, string>? referenceSelector = null;
            if (!string.IsNullOrEmpty(referenceAttribute))
            {
                referenceSelector = req => req.Target.Contains(referenceAttribute)
                    ? req.Target[referenceAttribute]?.ToString() ?? string.Empty
                    : string.Empty;
            }

            var batchResult = _executor.ExecuteRequests<UpdateRequest, UpdateResponse>(requests, requestsPerBatch, referenceSelector, continueOnError);

            return TransformBatchResultToIndividualResults<UpdateRequest, UpdateResponse>(batchResult, requests, referenceSelector);
        }

        /// <summary>
        /// Upserts multiple entities in parallel with batching support
        /// </summary>
        /// <param name="entities">Entities to upsert</param>
        /// <param name="requestsPerBatch">Number of create requests per batch</param>
        /// <param name="referenceAttribute">Optional attribute name to use as reference for tracking</param>
        /// <param name="continueOnError">Whether to continue processing remaining batches if a batch fails</param>
        /// <returns>ExecutionResult containing all upsert results</returns>
        public ExecutionResult<UpsertResponse> UpsertEntities(
            IEnumerable<Entity> entities,
            int requestsPerBatch,
            string referenceAttribute = "",
            bool continueOnError = true)
        {
            var requests = entities.Select(entity => new UpsertRequest { Target = entity });

            Func<UpsertRequest, string>? referenceSelector = null;
            if (!string.IsNullOrEmpty(referenceAttribute))
            {
                referenceSelector = req => req.Target.Contains(referenceAttribute)
                    ? req.Target[referenceAttribute]?.ToString() ?? string.Empty
                    : string.Empty;
            }

            var batchResult = _executor.ExecuteRequests<UpsertRequest, UpsertResponse>(requests, requestsPerBatch, referenceSelector, continueOnError);

            return TransformBatchResultToIndividualResults<UpsertRequest, UpsertResponse>(batchResult, requests, referenceSelector);
        }

        /// <summary>
        /// Deletes multiple entities in parallel with batching support
        /// </summary>
        /// <param name="entityReferences">Entity references to delete</param>
        /// <returns>Delete results</returns>
        public ExecutionResult<DeleteResponse> DeleteEntities(IEnumerable<EntityReference> entityReferences, int requestsPerBatch, bool continueOnError = true)
        {
            var requests = entityReferences.Select(entityRef => new DeleteRequest
            {
                Target = entityRef
            });

            Func<DeleteRequest, string> referenceSelector = req =>
                $"{req.Target.LogicalName}:{req.Target.Id}";

            var batchResult = _executor.ExecuteRequests<DeleteRequest, DeleteResponse>(requests, requestsPerBatch, referenceSelector, continueOnError);

            return TransformBatchResultToIndividualResults<DeleteRequest, DeleteResponse>(batchResult, requests, referenceSelector);
        }

        /// <summary>
        /// Deletes multiple entities in parallel by entity name and IDs with batching support
        /// </summary>
        /// <param name="entityLogicalName">Logical name of the entity</param>
        /// <param name="entityIds">IDs of entities to delete</param>
        /// <returns>Delete results</returns>
        public ExecutionResult<DeleteResponse> DeleteEntities(string entityLogicalName, IEnumerable<Guid> entityIds, int requestsPerBatch, bool continueOnError = true)
        {
            var entityReferences = entityIds.Select(id => new EntityReference(entityLogicalName, id));
            return DeleteEntities(entityReferences, requestsPerBatch, continueOnError);
        }

        #endregion Parallel Batch Operations

        #region Internal Routines

        // <summary>
        /// Transforms ExecutionResult&lt;ExecuteMultipleResponse&gt; to ExecutionResult&lt;TResponse&gt;
        /// </summary>
        private ExecutionResult<TResponse> TransformBatchResultToIndividualResults<TRequest, TResponse>(
            ExecutionResult<ExecuteMultipleResponse> batchResult,
            IEnumerable<TRequest> originalRequests,
            Func<TRequest, string>? referenceSelector)
            where TRequest : OrganizationRequest
            where TResponse : OrganizationResponse
        {
            var individualResult = new ExecutionResult<TResponse>
            {
                TotalProcessed = batchResult.TotalProcessed,
                StartTime = batchResult.StartTime,
                EndTime = batchResult.EndTime,
                Results = new List<SingleExecutionResult<TResponse>>(),
                BatchResults = batchResult.Results
            };

            var requestIndex = 0;

            var originalRequestsList = originalRequests.ToList();

            // Process each batch result
            foreach (var batchSingleResult in batchResult.Results)
            {
                if (batchSingleResult.IsSuccess && batchSingleResult.Response != null)
                {
                    // Process successful batch - extract individual responses
                    var executeMultipleResponse = batchSingleResult.Response;

                    for (int i = 0; i < executeMultipleResponse.Responses.Count; i++)
                    {
                        var response = executeMultipleResponse.Responses[i];
                        var originalRequest = requestIndex < originalRequestsList.Count ? originalRequestsList[requestIndex] : null;
                        var referenceValue = originalRequest != null && referenceSelector != null
                            ? referenceSelector(originalRequest)
                            : string.Empty;

                        if (response.Fault == null)
                        {
                            // Successful individual response
                            individualResult.Results.Add(new SingleExecutionResult<TResponse>
                            {
                                IsSuccess = true,
                                Response = response.Response as TResponse,
                                ReferenceValue = referenceValue,
                                InternalTransactionId = batchSingleResult.InternalTransactionId
                            });
                            individualResult.SuccessCount++;
                        }
                        else
                        {
                            // Failed individual response
                            individualResult.Results.Add(new SingleExecutionResult<TResponse>
                            {
                                IsSuccess = false,
                                ErrorMessage = response.Fault.Message,
                                ReferenceValue = referenceValue,
                                Exception = new Exception(response.Fault.Message),
                                InternalTransactionId = batchSingleResult.InternalTransactionId
                            });
                            individualResult.ErrorCount++;
                        }
                        requestIndex++;
                    }
                }
                else
                {
                    // Entire batch failed - mark all requests in this batch as failed
                    // We need to determine how many requests were in this batch
                    var remainingRequests = originalRequestsList.Count - requestIndex;
                    var estimatedBatchSize = Math.Min(remainingRequests,
                        requestIndex > 0 ? requestIndex : remainingRequests);

                    // If we can't determine batch size, we need to track it differently
                    // For now, we'll mark remaining requests as failed
                    for (int i = requestIndex; i < originalRequestsList.Count; i++)
                    {
                        var originalRequest = originalRequestsList[i];
                        var referenceValue = referenceSelector != null
                            ? referenceSelector(originalRequest)
                            : string.Empty;

                        individualResult.Results.Add(new SingleExecutionResult<TResponse>
                        {
                            IsSuccess = false,
                            ErrorMessage = batchSingleResult.ErrorMessage ?? "Batch execution failed",
                            ReferenceValue = referenceValue,
                            Exception = batchSingleResult.Exception
                        });
                        individualResult.ErrorCount++;
                    }
                    break; // Exit the loop since we've processed all remaining requests
                }
            }

            return individualResult;
        }

        #endregion Internal Routines
    }
}