using Microsoft.Xrm.Sdk;

namespace DataverseOps
{
    /// <summary>
    /// Result of a single request execution
    /// </summary>
    /// <typeparam name="TResponse">Type of OrganizationResponse</typeparam>
    public class SingleExecutionResult<TResponse> where TResponse : OrganizationResponse
    {
        internal SingleExecutionResult()
        {
        }
        public Guid InternalTransactionId { get; internal set; }
        public string ReferenceValue { get; internal set; } = string.Empty;
        public bool IsSuccess { get; internal set; }
        public TResponse? Response { get; internal set; }
        public string? ErrorMessage { get; internal set; }
        public Exception? Exception { get; internal set; }
        public SingleExecutionTimingData? TimingData { get; internal set; }
    }

    public class SingleExecutionTimingData
    {
        internal SingleExecutionTimingData()
        {
        }

        public DateTime StartTime { get; internal set; }
        public DateTime EndTime { get; internal set; }
        public long SdkTransactionTimeMilliseconds { get; internal set; }
    }

}