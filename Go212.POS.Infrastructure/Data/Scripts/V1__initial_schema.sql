-- ============================================================
-- GO212 POS — Database Schema
-- Version: V1__initial_schema.sql
-- Engine: MySQL 8.4 LTS | Charset: utf8mb4 | Engine: InnoDB
-- Rule: amounts use DECIMAL, never FLOAT/DOUBLE
-- Rule: sensitive deletes = deactivation (IsActive = false)
-- Rule: all dates stored as UTC (DATETIME)
-- ============================================================

CREATE DATABASE IF NOT EXISTS go212_pos
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;

USE go212_pos;

-- ── 1. VENDOR ────────────────────────────────────────────────
-- One row per installation. Each vendor has their own database.
CREATE TABLE IF NOT EXISTS Vendor (
    Id                  BIGINT          NOT NULL AUTO_INCREMENT,
    Name                VARCHAR(150)    NOT NULL,
    Phone               VARCHAR(30),
    Email               VARCHAR(120),
    Address             VARCHAR(255),
    TaxId               VARCHAR(50),
    Currency            VARCHAR(10)     NOT NULL DEFAULT 'MAD',
    LogoPath            VARCHAR(500)    NOT NULL DEFAULT '',
    ReceiptHeader       VARCHAR(500)    NOT NULL DEFAULT '',
    ReceiptFooter       VARCHAR(500)    NOT NULL DEFAULT '',
    SaleNumberPrefix    VARCHAR(10)     NOT NULL DEFAULT 'VTE',
    SaleNumberNext      INT             NOT NULL DEFAULT 1,
    CreatedAt           DATETIME        NOT NULL DEFAULT (UTC_TIMESTAMP()),
    UpdatedAt           DATETIME        NOT NULL DEFAULT (UTC_TIMESTAMP()),
    PRIMARY KEY (Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ── 2. USERS ─────────────────────────────────────────────────
-- PIN stored as BCrypt hash. Never plain text.
-- Role: 1=Admin, 2=Manager, 3=Cashier
CREATE TABLE IF NOT EXISTS User (
    Id                  BIGINT          NOT NULL AUTO_INCREMENT,
    Name                VARCHAR(100)    NOT NULL,
    Username            VARCHAR(50)     NOT NULL,
    PinHash             VARCHAR(255)    NOT NULL,
    Role                TINYINT         NOT NULL DEFAULT 3,
    IsActive            BOOLEAN         NOT NULL DEFAULT TRUE,
    LastLoginAt         DATETIME,
    FailedLoginAttempts INT             NOT NULL DEFAULT 0,
    LockedUntil         DATETIME,
    CreatedAt           DATETIME        NOT NULL DEFAULT (UTC_TIMESTAMP()),
    UpdatedAt           DATETIME        NOT NULL DEFAULT (UTC_TIMESTAMP()),
    PRIMARY KEY (Id),
    UNIQUE INDEX UX_User_Username (Username)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ── 3. CATEGORIES ────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS Category (
    Id              BIGINT          NOT NULL AUTO_INCREMENT,
    Name            VARCHAR(100)    NOT NULL,
    Description     VARCHAR(255),
    Color           VARCHAR(7)      NOT NULL DEFAULT '#00BF63',
    IconName        VARCHAR(50),
    DisplayOrder    INT             NOT NULL DEFAULT 0,
    IsActive        BOOLEAN         NOT NULL DEFAULT TRUE,
    CreatedAt       DATETIME        NOT NULL DEFAULT (UTC_TIMESTAMP()),
    UpdatedAt       DATETIME        NOT NULL DEFAULT (UTC_TIMESTAMP()),
    PRIMARY KEY (Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ── 4. PRODUCTS ──────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS Product (
    Id                  BIGINT          NOT NULL AUTO_INCREMENT,
    Name                VARCHAR(150)    NOT NULL,
    Description         VARCHAR(500),
    CategoryId          BIGINT          NOT NULL,
    PriceHT             DECIMAL(12,2)   NOT NULL,
    TaxRate             DECIMAL(5,2)    NOT NULL DEFAULT 20.00,
    Barcode             VARCHAR(100),
    Unit                VARCHAR(20)     NOT NULL DEFAULT 'pcs',
    ImagePath           VARCHAR(500),
    StockQuantity       INT             NOT NULL DEFAULT 0,
    StockAlertThreshold INT             NOT NULL DEFAULT 5,
    IsActive            BOOLEAN         NOT NULL DEFAULT TRUE,
    HasVariants         BOOLEAN         NOT NULL DEFAULT FALSE,
    CreatedAt           DATETIME        NOT NULL DEFAULT (UTC_TIMESTAMP()),
    UpdatedAt           DATETIME        NOT NULL DEFAULT (UTC_TIMESTAMP()),
    PRIMARY KEY (Id),
    UNIQUE INDEX UX_Product_Barcode (Barcode),
    CONSTRAINT FK_Product_Category FOREIGN KEY (CategoryId)
        REFERENCES Category (Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ── 5. CUSTOMERS ─────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS Customer (
    Id              BIGINT          NOT NULL AUTO_INCREMENT,
    Name            VARCHAR(150)    NOT NULL,
    Phone           VARCHAR(30),
    Email           VARCHAR(120),
    TotalPurchases  DECIMAL(14,2)   NOT NULL DEFAULT 0.00,
    VisitCount      INT             NOT NULL DEFAULT 0,
    IsActive        BOOLEAN         NOT NULL DEFAULT TRUE,
    CreatedAt       DATETIME        NOT NULL DEFAULT (UTC_TIMESTAMP()),
    UpdatedAt       DATETIME        NOT NULL DEFAULT (UTC_TIMESTAMP()),
    PRIMARY KEY (Id),
    INDEX IX_Customer_Phone (Phone)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ── 6. CASH SESSIONS ─────────────────────────────────────────
CREATE TABLE IF NOT EXISTS CashSession (
    Id                  BIGINT          NOT NULL AUTO_INCREMENT,
    UserId              BIGINT          NOT NULL,
    OpeningFloat        DECIMAL(12,2)   NOT NULL,
    OpenedAt            DATETIME        NOT NULL DEFAULT (UTC_TIMESTAMP()),
    ClosingExpected     DECIMAL(12,2),
    ClosingCounted      DECIMAL(12,2),
    ClosingDiscrepancy  DECIMAL(12,2),
    ClosedAt            DATETIME,
    Status              TINYINT         NOT NULL DEFAULT 1,  -- 1=Open, 2=Closed
    Notes               VARCHAR(500),
    CreatedAt           DATETIME        NOT NULL DEFAULT (UTC_TIMESTAMP()),
    UpdatedAt           DATETIME        NOT NULL DEFAULT (UTC_TIMESTAMP()),
    PRIMARY KEY (Id),
    CONSTRAINT FK_CashSession_User FOREIGN KEY (UserId)
        REFERENCES User (Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ── 7. SALES ─────────────────────────────────────────────────
-- A completed sale is NEVER deleted. Cancel/Refund only.
CREATE TABLE IF NOT EXISTS Sale (
    Id                  BIGINT          NOT NULL AUTO_INCREMENT,
    SaleNumber          VARCHAR(30)     NOT NULL,             -- VTE-20260801-001
    UserId              BIGINT          NOT NULL,
    CustomerId          BIGINT,
    CashSessionId       BIGINT          NOT NULL,
    Status              TINYINT         NOT NULL DEFAULT 1,   -- 1=Open 2=Completed 3=Cancelled 4=Refunded
    SubtotalHT          DECIMAL(14,2)   NOT NULL DEFAULT 0.00,
    TaxAmount           DECIMAL(12,2)   NOT NULL DEFAULT 0.00,
    DiscountAmount      DECIMAL(12,2)   NOT NULL DEFAULT 0.00,
    TotalTTC            DECIMAL(14,2)   NOT NULL DEFAULT 0.00,
    CancellationReason  VARCHAR(500),
    CancelledByUserId   BIGINT,
    CancelledAt         DATETIME,
    CreatedAt           DATETIME        NOT NULL DEFAULT (UTC_TIMESTAMP()),
    UpdatedAt           DATETIME        NOT NULL DEFAULT (UTC_TIMESTAMP()),
    PRIMARY KEY (Id),
    UNIQUE INDEX UX_Sale_Number (SaleNumber),
    CONSTRAINT FK_Sale_User         FOREIGN KEY (UserId)        REFERENCES User (Id),
    CONSTRAINT FK_Sale_Customer     FOREIGN KEY (CustomerId)    REFERENCES Customer (Id),
    CONSTRAINT FK_Sale_CashSession  FOREIGN KEY (CashSessionId) REFERENCES CashSession (Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ── 8. SALE ITEMS ────────────────────────────────────────────
-- Prices are snapshotted at time of sale (product price may change later).
CREATE TABLE IF NOT EXISTS SaleItem (
    Id              BIGINT          NOT NULL AUTO_INCREMENT,
    SaleId          BIGINT          NOT NULL,
    ProductId       BIGINT          NOT NULL,
    ProductName     VARCHAR(150)    NOT NULL,    -- Snapshot
    ProductBarcode  VARCHAR(100)    NOT NULL DEFAULT '',
    Quantity        INT             NOT NULL,
    UnitPriceHT     DECIMAL(12,2)   NOT NULL,    -- Snapshot
    TaxRate         DECIMAL(5,2)    NOT NULL,     -- Snapshot
    DiscountPercent DECIMAL(5,2)    NOT NULL DEFAULT 0.00,
    LineTotalHT     DECIMAL(14,2)   NOT NULL,
    LineTaxAmount   DECIMAL(12,2)   NOT NULL,
    LineTotalTTC    DECIMAL(14,2)   NOT NULL,
    Note            VARCHAR(255),
    CreatedAt       DATETIME        NOT NULL DEFAULT (UTC_TIMESTAMP()),
    UpdatedAt       DATETIME        NOT NULL DEFAULT (UTC_TIMESTAMP()),
    PRIMARY KEY (Id),
    CONSTRAINT FK_SaleItem_Sale     FOREIGN KEY (SaleId)    REFERENCES Sale (Id),
    CONSTRAINT FK_SaleItem_Product  FOREIGN KEY (ProductId) REFERENCES Product (Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ── 9. PAYMENTS ──────────────────────────────────────────────
-- Card: only type + status + terminal reference. NEVER card number.
CREATE TABLE IF NOT EXISTS Payment (
    Id              BIGINT          NOT NULL AUTO_INCREMENT,
    SaleId          BIGINT          NOT NULL,
    Method          TINYINT         NOT NULL,    -- 1=Cash 2=Card 3=Mixed
    Amount          DECIMAL(12,2)   NOT NULL,
    IsSuccess       BOOLEAN         NOT NULL DEFAULT TRUE,
    CardTerminalRef VARCHAR(100),               -- Card only, NO card number
    CardType        VARCHAR(20),                -- "Visa", "Mastercard"
    Notes           VARCHAR(255),
    CreatedAt       DATETIME        NOT NULL DEFAULT (UTC_TIMESTAMP()),
    UpdatedAt       DATETIME        NOT NULL DEFAULT (UTC_TIMESTAMP()),
    PRIMARY KEY (Id),
    CONSTRAINT FK_Payment_Sale FOREIGN KEY (SaleId) REFERENCES Sale (Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ── 10. STOCK MOVEMENTS ──────────────────────────────────────
CREATE TABLE IF NOT EXISTS StockMovement (
    Id              BIGINT          NOT NULL AUTO_INCREMENT,
    ProductId       BIGINT          NOT NULL,
    Type            TINYINT         NOT NULL,    -- 1=Entry 2=Exit 3=Sale 4=Return 5=Adjustment
    QuantityBefore  INT             NOT NULL,
    QuantityChange  INT             NOT NULL,    -- Positive=in, Negative=out
    QuantityAfter   INT             NOT NULL,
    SaleId          BIGINT,
    UserId          BIGINT          NOT NULL,
    Reason          VARCHAR(255),
    CreatedAt       DATETIME        NOT NULL DEFAULT (UTC_TIMESTAMP()),
    UpdatedAt       DATETIME        NOT NULL DEFAULT (UTC_TIMESTAMP()),
    PRIMARY KEY (Id),
    CONSTRAINT FK_StockMovement_Product FOREIGN KEY (ProductId) REFERENCES Product (Id),
    CONSTRAINT FK_StockMovement_User    FOREIGN KEY (UserId)    REFERENCES User (Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ── 11. EXPENSES ─────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS Expense (
    Id              BIGINT          NOT NULL AUTO_INCREMENT,
    CashSessionId   BIGINT          NOT NULL,
    UserId          BIGINT          NOT NULL,
    Description     VARCHAR(255)    NOT NULL,
    Amount          DECIMAL(12,2)   NOT NULL,
    Category        VARCHAR(50),
    CreatedAt       DATETIME        NOT NULL DEFAULT (UTC_TIMESTAMP()),
    UpdatedAt       DATETIME        NOT NULL DEFAULT (UTC_TIMESTAMP()),
    PRIMARY KEY (Id),
    CONSTRAINT FK_Expense_CashSession   FOREIGN KEY (CashSessionId) REFERENCES CashSession (Id),
    CONSTRAINT FK_Expense_User          FOREIGN KEY (UserId)        REFERENCES User (Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ── 12. RETURNS ──────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `Return` (
    Id              BIGINT          NOT NULL AUTO_INCREMENT,
    OriginalSaleId  BIGINT          NOT NULL,
    UserId          BIGINT          NOT NULL,
    Reason          VARCHAR(500)    NOT NULL,
    RefundAmount    DECIMAL(12,2)   NOT NULL,
    RefundMethod    TINYINT         NOT NULL,
    CreatedAt       DATETIME        NOT NULL DEFAULT (UTC_TIMESTAMP()),
    UpdatedAt       DATETIME        NOT NULL DEFAULT (UTC_TIMESTAMP()),
    PRIMARY KEY (Id),
    CONSTRAINT FK_Return_Sale FOREIGN KEY (OriginalSaleId) REFERENCES Sale (Id),
    CONSTRAINT FK_Return_User FOREIGN KEY (UserId) REFERENCES User (Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS ReturnItem (
    Id          BIGINT  NOT NULL AUTO_INCREMENT,
    ReturnId    BIGINT  NOT NULL,
    SaleItemId  BIGINT  NOT NULL,
    Quantity    INT     NOT NULL,
    RestockItem BOOLEAN NOT NULL DEFAULT TRUE,
    CreatedAt   DATETIME NOT NULL DEFAULT (UTC_TIMESTAMP()),
    UpdatedAt   DATETIME NOT NULL DEFAULT (UTC_TIMESTAMP()),
    PRIMARY KEY (Id),
    CONSTRAINT FK_ReturnItem_Return   FOREIGN KEY (ReturnId)   REFERENCES `Return` (Id),
    CONSTRAINT FK_ReturnItem_SaleItem FOREIGN KEY (SaleItemId) REFERENCES SaleItem (Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ── 13. SETTINGS ─────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS Setting (
    Id          BIGINT          NOT NULL AUTO_INCREMENT,
    `Key`       VARCHAR(100)    NOT NULL,
    Value       TEXT            NOT NULL,
    Description VARCHAR(255),
    IsSecret    BOOLEAN         NOT NULL DEFAULT FALSE,
    CreatedAt   DATETIME        NOT NULL DEFAULT (UTC_TIMESTAMP()),
    UpdatedAt   DATETIME        NOT NULL DEFAULT (UTC_TIMESTAMP()),
    PRIMARY KEY (Id),
    UNIQUE INDEX UX_Setting_Key (`Key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ── 14. AUDIT EVENTS ─────────────────────────────────────────
-- Immutable. No DELETE. No sensitive data (PIN, card numbers).
CREATE TABLE IF NOT EXISTS AuditEvent (
    Id              BIGINT          NOT NULL AUTO_INCREMENT,
    UserId          BIGINT,
    UserName        VARCHAR(100)    NOT NULL DEFAULT '',
    Action          SMALLINT        NOT NULL,
    TargetEntity    VARCHAR(50),
    TargetId        BIGINT,
    Details         TEXT,           -- Human-readable, NO secrets
    IpOrMachine     VARCHAR(100)    NOT NULL DEFAULT '',
    CreatedAt       DATETIME        NOT NULL DEFAULT (UTC_TIMESTAMP()),
    UpdatedAt       DATETIME        NOT NULL DEFAULT (UTC_TIMESTAMP()),
    PRIMARY KEY (Id),
    INDEX IX_AuditEvent_UserId      (UserId),
    INDEX IX_AuditEvent_Action      (Action),
    INDEX IX_AuditEvent_CreatedAt   (CreatedAt)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
