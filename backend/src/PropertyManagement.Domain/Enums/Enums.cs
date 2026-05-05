namespace PropertyManagement.Domain.Enums;

public enum PmsProvider
{
    RentManager = 1,
    Yardi = 2,
    AppFolio = 3,
    Buildium = 4,
    PropertyFlow = 5
}

public enum CaseStageCode
{
    Intake = 1,
    Draft = 2,
    FormReview = 3,
    ReadyToFile = 4,
    Filed = 5,
    CourtDateScheduled = 6,
    Judgment = 7,
    Settlement = 8,
    Dismissed = 9,
    WarrantRequested = 10,
    Closed = 11
}

public enum CaseStatusCode
{
    Open = 1,
    OnHold = 2,
    Closed = 3,
    Cancelled = 4
}

public enum CaseType
{
    LandlordTenantEviction = 1,
    LandlordTenantHoldover = 2,
    Other = 99
}

public enum LtFormType
{
    VerifiedComplaint = 1,
    Summons = 2,
    CertificationByLandlord = 3,
    CertificationByAttorney = 4,
    CertificationOfLeaseAndRegistration = 5,
    LandlordCaseInformationStatement = 6,
    RequestForResidentialWarrantOfRemoval = 7
}

/// <summary>NJ LT cases move through three procedural phases; each form belongs to one of them.</summary>
public enum LtFormPhase
{
    Filing = 1,
    TrialCertification = 2,
    Warrant = 3
}

public enum DocumentType
{
    Lease = 1,
    Ledger = 2,
    NoticeToQuit = 3,
    NoticeToCease = 4,
    RegistrationStatement = 5,
    CertifiedMail = 6,
    Court = 7,
    Generated = 8,
    Other = 99
}

public enum AuditAction
{
    // ─── Auth ───────────────────────────────────────────────────────
    Login = 1,
    Logout = 2,
    LoginFailed = 3,

    // ─── PMS sync (umbrella + lifecycle) ────────────────────────────
    PmsSync = 10,
    PmsSyncStarted = 11,
    PmsSyncCompleted = 12,
    PmsSyncFailed = 13,

    // ─── Case lifecycle ─────────────────────────────────────────────
    CreateCase = 20,
    UpdateCase = 21,
    ChangeStatus = 22,
    CloseCase = 23,

    // ─── Documents ──────────────────────────────────────────────────
    GeneratePdf = 30,
    DownloadPdf = 31,
    UploadDocument = 40,
    DeleteDocument = 41,

    // ─── Users ──────────────────────────────────────────────────────
    CreateUser = 50,
    UpdateUser = 51,
    UserRoleChanged = 52,

    // ─── PMS integration CRUD ──────────────────────────────────────
    PmsIntegrationCreated = 60,
    PmsIntegrationUpdated = 61,
    PmsIntegrationDeleted = 62,

    // ─── Case sub-resources ─────────────────────────────────────────
    PaymentRecorded = 70,
    CommentAdded = 71,

    // ─── Client portal ──────────────────────────────────────────────
    ClientPortalAccess = 80,

    // ─── Client (property-management company) CRUD ────────────────
    ClientCreated = 90,
    ClientUpdated = 91,
    ClientDeleted = 92
}

public enum SyncStatus
{
    Started = 1,
    Succeeded = 2,
    Failed = 3,
    PartiallySucceeded = 4
}
