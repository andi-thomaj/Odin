namespace Odin.Api.Endpoints.Admin.Models
{
    public class ClearBackendCacheContract
    {
        public class Response
        {
            /// <summary>
            /// Number of entries evicted from the in-process backend cache, or <c>-1</c> when the
            /// cache implementation does not support counting/clearing.
            /// </summary>
            public long EntriesCleared { get; set; }
        }
    }
}
