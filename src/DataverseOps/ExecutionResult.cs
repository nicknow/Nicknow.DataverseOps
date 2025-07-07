using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;

namespace DataverseOps
{
    /// <summary>
    /// Result of parallel execution containing all individual results and summary information
    /// </summary>
    /// <typeparam name="TResponse">Type of OrganizationResponse</typeparam>
    public class ExecutionResult<TResponse> where TResponse : OrganizationResponse
    {
        public List<SingleExecutionResult<TResponse>> Results { get; internal set; } = new List<SingleExecutionResult<TResponse>>();
        public int TotalProcessed { get; internal set; }
        public int SuccessCount { get; internal set; }
        public int ErrorCount { get; internal set; }
        public DateTime StartTime { get; internal set; }
        public DateTime EndTime { get; internal set; }

        public List<SingleExecutionResult<ExecuteMultipleResponse>>? BatchResults { get; internal set; }

        public TimeSpan Duration => EndTime - StartTime;

        public bool HasErrors => ErrorCount > 0;

        public IEnumerable<SingleExecutionResult<TResponse>> SuccessResults =>
            Results.Where(r => r.IsSuccess);

        public IEnumerable<SingleExecutionResult<TResponse>> ErrorResults =>
            Results.Where(r => !r.IsSuccess);
    }
}