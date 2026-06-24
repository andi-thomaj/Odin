namespace Odin.Api.Endpoints.Payments.Models
{
    public class AdminAppStoreTransactionContract
    {
        /// <summary>Admin view of a recorded Apple StoreKit transaction, with the owning user and linked order.</summary>
        public class Response
        {
            public int Id { get; set; }
            public string TransactionId { get; set; } = string.Empty;
            public string OriginalTransactionId { get; set; } = string.Empty;
            public string ProductId { get; set; } = string.Empty;
            /// <summary>"qpAdm" | "g25".</summary>
            public string Service { get; set; } = string.Empty;
            /// <summary>"Verified" | "Consumed" | "Refunded".</summary>
            public string Status { get; set; } = string.Empty;
            public int? QpadmOrderId { get; set; }
            public int? G25OrderId { get; set; }
            public DateTime PurchaseDate { get; set; }
            /// <summary>"Production" | "Sandbox" | "Xcode".</summary>
            public string Environment { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public string CreatedBy { get; set; } = string.Empty;

            /// <summary>Owning <c>application_users</c> row id; null when the creator identity has no provisioned user.</summary>
            public int? OwnerId { get; set; }
            public string? OwnerEmail { get; set; }
            public string OwnerFirstName { get; set; } = string.Empty;
            public string OwnerLastName { get; set; } = string.Empty;
        }
    }
}
