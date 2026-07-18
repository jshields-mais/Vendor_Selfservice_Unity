namespace Vss.Domain;

/// <summary>Where a portal user sits in the ERP-linking lifecycle.</summary>
public enum LinkState
{
    Unlinked = 0,
    PendingLink = 1,
    Linked = 2
}

/// <summary>How a vendor proves ownership of an existing ERP record when linking.</summary>
public enum LinkMethod
{
    VendorNumberPin = 0,
    TaxIdZip = 1
}

public enum LinkRequestStatus
{
    Pending = 0,
    Matched = 1,
    Approved = 2,
    Rejected = 3
}

public enum ChangeRequestStatus
{
    PendingReview = 0,
    InReview = 1,
    Approved = 2,
    Rejected = 3
}

public enum DocumentStatus
{
    Current = 0,
    Expiring = 1,
    Expired = 2,
    AwaitingDocs = 3,
    PendingReview = 4
}
