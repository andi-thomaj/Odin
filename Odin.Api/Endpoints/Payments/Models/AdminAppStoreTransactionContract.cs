namespace Odin.Api.Endpoints.Payments.Models
{
    public class AdminAppStoreTransactionContract
    {
        /// <summary>
        /// Admin view of a single recorded Apple in-app purchase — across EVERY purchase type the app sells:
        /// the paid qpAdm/G25 analysis orders (<c>app_store_transactions</c>) plus the per-order add-ons,
        /// the Y-DNA results unlock (<c>qpadm_ydna_unlocks</c>) and the "Through the Ages" AI portraits
        /// (<c>ancestral_portrait_sets</c>). Carries the owning user, the linked order, and the nominal money paid.
        /// </summary>
        public class Response
        {
            /// <summary>Numeric id within its source table (0 for the Guid-keyed AI-portrait sets); use <see cref="RowKey"/> as the stable unique key.</summary>
            public int Id { get; set; }

            /// <summary>Globally-unique stable key across all purchase kinds (<c>"{kind}:{transactionId}"</c>) — the grid row id.</summary>
            public string RowKey { get; set; } = string.Empty;

            /// <summary>Purchase category: "Order" (qpAdm/G25 analysis), "YDnaUnlock", or "AiPortraits".</summary>
            public string Kind { get; set; } = string.Empty;

            /// <summary>Human-friendly product name, e.g. "qpAdm Analysis", "G25 Analysis", "Y-DNA Unlock", "Through the Ages".</summary>
            public string ProductLabel { get; set; } = string.Empty;

            /// <summary>Nominal amount paid for this purchase, in <see cref="Currency"/> (see <c>AppleIapOptions</c> — informational/display).</summary>
            public decimal Amount { get; set; }

            /// <summary>ISO currency code for <see cref="Amount"/> (a single nominal reporting currency).</summary>
            public string Currency { get; set; } = string.Empty;

            public string TransactionId { get; set; } = string.Empty;
            public string OriginalTransactionId { get; set; } = string.Empty;
            public string ProductId { get; set; } = string.Empty;
            /// <summary>"qpAdm" | "g25" (the add-ons are qpAdm-scoped).</summary>
            public string Service { get; set; } = string.Empty;
            /// <summary>"Verified" | "Consumed" | "Refunded". Add-ons are "Consumed" while the entitlement row exists (a refund deletes/revokes it).</summary>
            public string Status { get; set; } = string.Empty;
            public int? QpadmOrderId { get; set; }
            public int? G25OrderId { get; set; }
            public DateTime PurchaseDate { get; set; }
            /// <summary>"Production" | "Sandbox" | "Xcode"; empty for add-ons (not persisted on their entitlement rows).</summary>
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
