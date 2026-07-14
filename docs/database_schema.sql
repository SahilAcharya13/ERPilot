-- =========================================================================
-- ERP SYSTEM DATABASE SCHEMA DDL
-- Target DB: Microsoft SQL Server (MSSQL)
-- Enterprise-ready, fully normalized, index-optimized with soft deletes and audit logs
-- =========================================================================

-- Create Database if not exists (uncomment if running as a fresh setup)
-- CREATE DATABASE ErpDatabase;
-- GO
-- USE ErpDatabase;
-- GO

-- =========================================================================
-- TABLE: Users
-- =========================================================================
CREATE TABLE [dbo].[Users] (
    [UserID] INT IDENTITY(1,1) NOT NULL,
    [Name] NVARCHAR(100) NOT NULL,
    [Email] NVARCHAR(256) NOT NULL,
    [PasswordHash] NVARCHAR(512) NOT NULL,
    [Role] NVARCHAR(50) NOT NULL,
    [IsActive] BIT NOT NULL DEFAULT 1,
    [CreatedDate] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ModifiedDate] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
    [IsDeleted] BIT NOT NULL DEFAULT 0,
    [DeletedAt] DATETIME2(7) NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED ([UserID] ASC),
    CONSTRAINT [UQ_Users_Email] UNIQUE NONCLUSTERED ([Email] ASC),
    CONSTRAINT [CK_Users_Role] CHECK ([Role] IN ('Admin', 'Sales', 'Accounts', 'Manager'))
);
GO

-- =========================================================================
-- TABLE: Customers
-- =========================================================================
CREATE TABLE [dbo].[Customers] (
    [CustomerID] INT IDENTITY(1,1) NOT NULL,
    [CustomerCode] NVARCHAR(50) NOT NULL,
    [CompanyName] NVARCHAR(150) NOT NULL,
    [ContactPerson] NVARCHAR(100) NULL,
    [Phone] NVARCHAR(20) NULL,
    [Email] NVARCHAR(256) NULL,
    [CreatedDate] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ModifiedDate] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
    [IsDeleted] BIT NOT NULL DEFAULT 0,
    [DeletedAt] DATETIME2(7) NULL,
    CONSTRAINT [PK_Customers] PRIMARY KEY CLUSTERED ([CustomerID] ASC),
    CONSTRAINT [UQ_Customers_CustomerCode] UNIQUE NONCLUSTERED ([CustomerCode] ASC)
);
GO

-- =========================================================================
-- TABLE: Products
-- =========================================================================
CREATE TABLE [dbo].[Products] (
    [ProductID] INT IDENTITY(1,1) NOT NULL,
    [ProductName] NVARCHAR(150) NOT NULL,
    [Category] NVARCHAR(100) NOT NULL,
    [Unit] NVARCHAR(20) NOT NULL,
    [Rate] DECIMAL(18, 2) NOT NULL,
    [GST] DECIMAL(5, 2) NOT NULL DEFAULT 0.00,
    [Status] NVARCHAR(50) NOT NULL DEFAULT 'Active',
    [CreatedDate] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ModifiedDate] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
    [IsDeleted] BIT NOT NULL DEFAULT 0,
    [DeletedAt] DATETIME2(7) NULL,
    CONSTRAINT [PK_Products] PRIMARY KEY CLUSTERED ([ProductID] ASC),
    CONSTRAINT [CK_Products_Status] CHECK ([Status] IN ('Active', 'Inactive')),
    CONSTRAINT [CK_Products_Rate] CHECK ([Rate] >= 0),
    CONSTRAINT [CK_Products_GST] CHECK ([GST] >= 0 AND [GST] <= 100)
);
GO

-- =========================================================================
-- TABLE: Orders
-- =========================================================================
CREATE TABLE [dbo].[Orders] (
    [OrderID] INT IDENTITY(1,1) NOT NULL,
    [OrderNumber] NVARCHAR(50) NOT NULL,
    [CustomerID] INT NOT NULL,
    [OrderDate] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
    [DeliveryDate] DATETIME2(7) NULL,
    [OrderStatus] NVARCHAR(50) NOT NULL DEFAULT 'Pending',
    [TotalAmount] DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
    [PaidAmount] DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
    [PendingAmount] AS ([TotalAmount] - [PaidAmount]) PERSISTED,
    [Remarks] NVARCHAR(500) NULL,
    [CreatedDate] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ModifiedDate] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
    [IsDeleted] BIT NOT NULL DEFAULT 0,
    [DeletedAt] DATETIME2(7) NULL,
    CONSTRAINT [PK_Orders] PRIMARY KEY CLUSTERED ([OrderID] ASC),
    CONSTRAINT [UQ_Orders_OrderNumber] UNIQUE NONCLUSTERED ([OrderNumber] ASC),
    CONSTRAINT [FK_Orders_Customers] FOREIGN KEY ([CustomerID]) REFERENCES [dbo].[Customers] ([CustomerID]),
    CONSTRAINT [CK_Orders_Status] CHECK ([OrderStatus] IN ('Pending', 'Processing', 'Shipped', 'Completed', 'Cancelled')),
    CONSTRAINT [CK_Orders_TotalAmount] CHECK ([TotalAmount] >= 0),
    CONSTRAINT [CK_Orders_PaidAmount] CHECK ([PaidAmount] >= 0)
);
GO

-- =========================================================================
-- TABLE: OrderItems
-- =========================================================================
CREATE TABLE [dbo].[OrderItems] (
    [OrderItemID] INT IDENTITY(1,1) NOT NULL,
    [OrderID] INT NOT NULL,
    [ProductID] INT NOT NULL,
    [Description] NVARCHAR(250) NULL,
    [Quantity] DECIMAL(18, 4) NOT NULL,
    [Unit] NVARCHAR(20) NOT NULL,
    [Rate] DECIMAL(18, 2) NOT NULL,
    [Amount] AS ([Quantity] * [Rate]) PERSISTED,
    CONSTRAINT [PK_OrderItems] PRIMARY KEY CLUSTERED ([OrderItemID] ASC),
    CONSTRAINT [FK_OrderItems_Orders] FOREIGN KEY ([OrderID]) REFERENCES [dbo].[Orders] ([OrderID]) ON DELETE CASCADE,
    CONSTRAINT [FK_OrderItems_Products] FOREIGN KEY ([ProductID]) REFERENCES [dbo].[Products] ([ProductID]),
    CONSTRAINT [CK_OrderItems_Quantity] CHECK ([Quantity] > 0),
    CONSTRAINT [CK_OrderItems_Rate] CHECK ([Rate] >= 0)
);
GO

-- =========================================================================
-- TABLE: Payments
-- =========================================================================
CREATE TABLE [dbo].[Payments] (
    [PaymentID] INT IDENTITY(1,1) NOT NULL,
    [CustomerID] INT NOT NULL,
    [OrderID] INT NULL,
    [PaymentDate] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
    [Amount] DECIMAL(18, 2) NOT NULL,
    [PaymentMode] NVARCHAR(50) NOT NULL,
    [ReferenceNumber] NVARCHAR(100) NULL,
    [Remarks] NVARCHAR(500) NULL,
    [CreatedDate] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ModifiedDate] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
    [IsDeleted] BIT NOT NULL DEFAULT 0,
    [DeletedAt] DATETIME2(7) NULL,
    CONSTRAINT [PK_Payments] PRIMARY KEY CLUSTERED ([PaymentID] ASC),
    CONSTRAINT [FK_Payments_Customers] FOREIGN KEY ([CustomerID]) REFERENCES [dbo].[Customers] ([CustomerID]),
    CONSTRAINT [FK_Payments_Orders] FOREIGN KEY ([OrderID]) REFERENCES [dbo].[Orders] ([OrderID]),
    CONSTRAINT [CK_Payments_Amount] CHECK ([Amount] > 0),
    CONSTRAINT [CK_Payments_Mode] CHECK ([PaymentMode] IN ('Cash', 'Cheque', 'Bank Transfer', 'Credit Card', 'UPI', 'Other'))
);
GO

-- =========================================================================
-- TABLE: AiActionLog (Audit / Log table for natural language actions)
-- =========================================================================
CREATE TABLE [dbo].[AiActionLog] (
    [LogID] BIGINT IDENTITY(1,1) NOT NULL,
    [UserID] INT NOT NULL,
    [OriginalPrompt] NVARCHAR(MAX) NOT NULL,
    [ExtractedIntent] NVARCHAR(100) NULL,
    [Parameters] NVARCHAR(MAX) NULL, -- JSON formatted string
    [GeneratedSQL] NVARCHAR(MAX) NULL,
    [ApprovalStatus] NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- 'Pending', 'Approved', 'Rejected', 'SystemBypassed'
    [ExecutionStatus] NVARCHAR(50) NOT NULL DEFAULT 'NotStarted', -- 'NotStarted', 'Success', 'Failed', 'Cancelled'
    [ExecutionTimeMs] INT NULL,
    [ErrorMessage] NVARCHAR(MAX) NULL,
    [Timestamp] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_AiActionLog] PRIMARY KEY CLUSTERED ([LogID] ASC),
    CONSTRAINT [FK_AiActionLog_Users] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
GO

-- =========================================================================
-- INDEXES FOR PERFORMANCE OPTIMIZATION
-- =========================================================================

-- Users search
CREATE NONCLUSTERED INDEX [IX_Users_IsDeleted] ON [dbo].[Users] ([IsDeleted] ASC);

-- Customers search
CREATE NONCLUSTERED INDEX [IX_Customers_CompanyName] ON [dbo].[Customers] ([CompanyName] ASC) WHERE [IsDeleted] = 0;
CREATE NONCLUSTERED INDEX [IX_Customers_Phone] ON [dbo].[Customers] ([Phone] ASC) WHERE [IsDeleted] = 0;

-- Products search
CREATE NONCLUSTERED INDEX [IX_Products_ProductName] ON [dbo].[Products] ([ProductName] ASC) WHERE [IsDeleted] = 0;
CREATE NONCLUSTERED INDEX [IX_Products_Category] ON [dbo].[Products] ([Category] ASC) WHERE [IsDeleted] = 0;

-- Orders indexes for filtering and joins
CREATE NONCLUSTERED INDEX [IX_Orders_CustomerID] ON [dbo].[Orders] ([CustomerID] ASC) WHERE [IsDeleted] = 0;
CREATE NONCLUSTERED INDEX [IX_Orders_OrderDate] ON [dbo].[Orders] ([OrderDate] DESC) WHERE [IsDeleted] = 0;
CREATE NONCLUSTERED INDEX [IX_Orders_OrderStatus] ON [dbo].[Orders] ([OrderStatus] ASC) WHERE [IsDeleted] = 0;

-- OrderItems index for join speed
CREATE NONCLUSTERED INDEX [IX_OrderItems_OrderID] ON [dbo].[OrderItems] ([OrderID] ASC);
CREATE NONCLUSTERED INDEX [IX_OrderItems_ProductID] ON [dbo].[OrderItems] ([ProductID] ASC);

-- Payments indexes
CREATE NONCLUSTERED INDEX [IX_Payments_CustomerID] ON [dbo].[Payments] ([CustomerID] ASC) WHERE [IsDeleted] = 0;
CREATE NONCLUSTERED INDEX [IX_Payments_OrderID] ON [dbo].[Payments] ([OrderID] ASC) WHERE [IsDeleted] = 0;
CREATE NONCLUSTERED INDEX [IX_Payments_PaymentDate] ON [dbo].[Payments] ([PaymentDate] DESC) WHERE [IsDeleted] = 0;

-- AiActionLog indexing for auditing
CREATE NONCLUSTERED INDEX [IX_AiActionLog_UserID] ON [dbo].[AiActionLog] ([UserID] ASC);
CREATE NONCLUSTERED INDEX [IX_AiActionLog_Timestamp] ON [dbo].[AiActionLog] ([Timestamp] DESC);
GO

-- =========================================================================
-- TRIGGERS TO AUTO-UPDATE MODIFIEDDATE COLUMNS ON UPDATE
-- =========================================================================

CREATE TRIGGER [dbo].[TR_Users_UpdateTimestamp]
ON [dbo].[Users]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[Users]
    SET [ModifiedDate] = SYSUTCDATETIME()
    FROM [dbo].[Users] U
    INNER JOIN inserted i ON U.[UserID] = i.[UserID];
END;
GO

CREATE TRIGGER [dbo].[TR_Customers_UpdateTimestamp]
ON [dbo].[Customers]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[Customers]
    SET [ModifiedDate] = SYSUTCDATETIME()
    FROM [dbo].[Customers] C
    INNER JOIN inserted i ON C.[CustomerID] = i.[CustomerID];
END;
GO

CREATE TRIGGER [dbo].[TR_Products_UpdateTimestamp]
ON [dbo].[Products]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[Products]
    SET [ModifiedDate] = SYSUTCDATETIME()
    FROM [dbo].[Products] P
    INNER JOIN inserted i ON P.[ProductID] = i.[ProductID];
END;
GO

CREATE TRIGGER [dbo].[TR_Orders_UpdateTimestamp]
ON [dbo].[Orders]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[Orders]
    SET [ModifiedDate] = SYSUTCDATETIME()
    FROM [dbo].[Orders] O
    INNER JOIN inserted i ON O.[OrderID] = i.[OrderID];
END;
GO

CREATE TRIGGER [dbo].[TR_Payments_UpdateTimestamp]
ON [dbo].[Payments]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[Payments]
    SET [ModifiedDate] = SYSUTCDATETIME()
    FROM [dbo].[Payments] P
    INNER JOIN inserted i ON P.[PaymentID] = i.[PaymentID];
END;
GO
