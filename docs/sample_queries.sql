-- =========================================================================
-- ERP AI ENGINE - SAMPLE SQL TEMPLATES & CONVERSATION WORKFLOWS
-- Mapped to the 7 Core Conversation Examples in the Master Specification.
-- All queries use ANSI SQL parameters to prevent SQL injection.
-- =========================================================================

-- =========================================================================
-- EXAMPLE 1: Pending Balance Check
-- User: "How much payment is pending for XYZ Traders?"
-- =========================================================================

-- 1. AI Intent: GetPendingPayment
-- 2. Parameters: { "CustomerName": "XYZ Traders" }
-- 3. Explanation: "Checking outstanding balances for XYZ Traders."

-- 4. Generated Parameterized SQL:
DECLARE @CompanyName NVARCHAR(150) = N'XYZ Traders';

SELECT 
    c.CustomerID,
    c.CustomerCode,
    c.CompanyName,
    ISNULL(SUM(o.PendingAmount), 0.00) AS TotalPendingAmount
FROM dbo.Customers c
LEFT JOIN dbo.Orders o ON c.CustomerID = o.CustomerID AND o.IsDeleted = 0
WHERE (c.CompanyName LIKE '%' + @CompanyName + '%' OR c.CustomerCode = @CompanyName)
  AND c.IsDeleted = 0
GROUP BY c.CustomerID, c.CustomerCode, c.CompanyName;

-- 5. Mock DB Result:
-- CustomerID | CustomerCode | CompanyName | TotalPendingAmount
-- -----------+--------------+-------------+-------------------
-- 12         | XYZ001       | XYZ Traders | 100000.00

-- 6. AI Conversation Output:
-- "XYZ Traders (XYZ001) has a pending payment balance of ₹100,000.00."


-- =========================================================================
-- EXAMPLE 2: Last Payment Received
-- User: "When was the last payment received from ABC Ltd?"
-- =========================================================================

-- 1. AI Intent: GetLastPayment
-- 2. Parameters: { "CustomerName": "ABC Ltd" }
-- 3. Explanation: "Finding the most recent payment received from ABC Ltd."

-- 4. Generated Parameterized SQL:
DECLARE @CustomerName NVARCHAR(150) = N'ABC Ltd';

SELECT TOP 1 
    p.PaymentID,
    c.CompanyName,
    p.PaymentDate,
    p.Amount,
    p.PaymentMode,
    p.ReferenceNumber,
    p.Remarks
FROM dbo.Payments p
INNER JOIN dbo.Customers c ON p.CustomerID = c.CustomerID
WHERE (c.CompanyName LIKE '%' + @CustomerName + '%' OR c.CustomerCode = @CustomerName)
  AND p.IsDeleted = 0
  AND c.IsDeleted = 0
ORDER BY p.PaymentDate DESC;

-- 5. Mock DB Result:
-- PaymentID | CompanyName | PaymentDate             | Amount   | PaymentMode   | ReferenceNumber | Remarks
-- ----------+-------------+-------------------------+----------+---------------+-----------------+-------------------
-- 502       | ABC Ltd     | 2026-07-12T14:30:00.000 | 75000.00 | Bank Transfer | TXN90281048     | Clearance of Invoice 1018

-- 6. AI Conversation Output:
-- "The last payment from ABC Ltd was ₹75,000.00 received on July 12, 2026 via Bank Transfer (Ref: TXN90281048)."


-- =========================================================================
-- EXAMPLE 3: Get Pending Orders
-- User: "Show pending orders."
-- =========================================================================

-- 1. AI Intent: GetPendingOrders
-- 2. Parameters: {} (No customer filter specified, get all)
-- 3. Explanation: "Fetching all active orders currently in Pending status."

-- 4. Generated Parameterized SQL:
SELECT 
    o.OrderID,
    o.OrderNumber,
    c.CompanyName,
    o.OrderDate,
    o.DeliveryDate,
    o.OrderStatus,
    o.TotalAmount,
    o.PendingAmount
FROM dbo.Orders o
INNER JOIN dbo.Customers c ON o.CustomerID = c.CustomerID
WHERE o.OrderStatus = 'Pending'
  AND o.IsDeleted = 0
  AND c.IsDeleted = 0
ORDER BY o.OrderDate ASC;

-- 5. Mock DB Result:
-- OrderID | OrderNumber  | CompanyName | OrderDate               | DeliveryDate            | OrderStatus | TotalAmount | PendingAmount
-- --------+--------------+-------------+-------------------------+-------------------------+-------------+-------------+--------------
-- 1025    | ORD-2026-004 | XYZ Traders | 2026-07-14T08:30:00.000 | 2026-07-20T00:00:00.000 | Pending     | 150000.00   | 100000.00

-- 6. AI Conversation Output:
-- "There is 1 pending order: Order ORD-2026-004 for XYZ Traders, placed today, totaling ₹150,000.00 (₹100,000.00 pending)."


-- =========================================================================
-- EXAMPLE 4: Conversational Add New Order
-- User: "Add a new order."
-- =========================================================================

-- 1. AI Intent: CreateOrder
-- 2. Initial parameters missing. AI responds asking follow-up questions:
--    "Sure, I can create a new order. Could you please specify:
--     1. Customer Name/Code?
--     2. Product Name/Code?
--     3. Quantity?
--     4. Rate (price per unit)?
--     5. Delivery Date?
--     6. Remarks (optional)?"
--
-- User response: "For XYZ Traders, 50 units of Steel Bar at ₹1,200 each, delivery by July 25th."
--
-- 3. Extracted Parameters:
--    {
--       "CustomerName": "XYZ Traders",
--       "ProductName": "Steel Bar",
--       "Quantity": 50.0,
--       "Rate": 1200.0,
--       "DeliveryDate": "2026-07-25",
--       "Remarks": "Added via AI Voice Assistant"
--    }

-- 4. Generated SQL (Executes inside Transaction, requires explicit approval):
-- Prior lookups done by backend to find CustomerID & ProductID:
-- SELECT CustomerID FROM dbo.Customers WHERE CompanyName = 'XYZ Traders'; --> 12
-- SELECT ProductID, Unit FROM dbo.Products WHERE ProductName = 'Steel Bar'; --> 8, 'pcs'

BEGIN TRANSACTION;
BEGIN TRY
    -- Insert Order Header
    DECLARE @NewOrderID INT;
    DECLARE @CustomerID INT = 12;
    DECLARE @OrderNumber NVARCHAR(50) = 'ORD-2026-006'; -- Generated sequence
    DECLARE @TotalAmt DECIMAL(18,2) = 60000.00; -- 50 * 1200
    DECLARE @DelivDate DATETIME2(7) = '2026-07-25T00:00:00';
    DECLARE @Rem NVARCHAR(500) = N'Added via AI Voice Assistant';

    INSERT INTO dbo.Orders (
        OrderNumber, CustomerID, OrderDate, DeliveryDate, OrderStatus, TotalAmount, PaidAmount, Remarks
    ) VALUES (
        @OrderNumber, @CustomerID, SYSUTCDATETIME(), @DelivDate, 'Pending', @TotalAmt, 0.00, @Rem
    );

    SET @NewOrderID = SCOPE_IDENTITY();

    -- Insert Order Items
    DECLARE @ProductID INT = 8;
    DECLARE @Qty DECIMAL(18,4) = 50.0000;
    DECLARE @Rate DECIMAL(18,2) = 1200.00;
    DECLARE @Unit NVARCHAR(20) = N'pcs';

    INSERT INTO dbo.OrderItems (
        OrderID, ProductID, Description, Quantity, Unit, Rate
    ) VALUES (
        @NewOrderID, @ProductID, N'Steel Bar', @Qty, @Unit, @Rate
    );

    COMMIT TRANSACTION;
    SELECT @NewOrderID AS CreatedOrderID, @OrderNumber AS OrderNumber;
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    THROW;
END CATCH;

-- 5. AI Confirmation Prompt (Shown before executing):
-- "WARNING: This will add a new order for XYZ Traders totaling ₹60,000.00 for 50 Steel Bars. Please confirm to execute."


-- =========================================================================
-- EXAMPLE 5: Record Payment
-- User: "Record payment of ₹50,000 received today from XYZ."
-- =========================================================================

-- 1. AI Intent: RecordPayment
-- 2. Extracted Parameters:
--    {
--       "CustomerName": "XYZ",
--       "Amount": 50000.00,
--       "PaymentDate": "2026-07-14",
--       "PaymentMode": "Bank Transfer", -- (Or defaults to UPI/Cash if unspecified, prompts user for clarification)
--       "Remarks": "Recorded via AI Prompt"
--    }

-- 3. Generated SQL (Requires approval):
-- Prior lookup finds CustomerID: 12
-- If a pending order exists, the payment can optionally update that order's PaidAmount.

BEGIN TRANSACTION;
BEGIN TRY
    DECLARE @CustID INT = 12;
    DECLARE @PaymentAmt DECIMAL(18,2) = 50000.00;
    DECLARE @PayMode NVARCHAR(50) = N'Bank Transfer';
    DECLARE @Reference NVARCHAR(100) = N'AI-REC-20260714';
    DECLARE @PaymentRemarks NVARCHAR(500) = N'Recorded via AI Prompt';

    -- Step A: Insert Payment Receipt Record
    INSERT INTO dbo.Payments (
        CustomerID, OrderID, PaymentDate, Amount, PaymentMode, ReferenceNumber, Remarks
    ) VALUES (
        @CustID, NULL, SYSUTCDATETIME(), @PaymentAmt, @PayMode, @Reference, @PaymentRemarks
    );

    -- Step B: Optional Auto-Allocation to Oldest Outstanding Order
    -- Find oldest pending order for Customer 12
    DECLARE @OldestOrderID INT;
    SELECT TOP 1 @OldestOrderID = OrderID 
    FROM dbo.Orders 
    WHERE CustomerID = @CustID AND PendingAmount > 0 AND IsDeleted = 0 
    ORDER BY OrderDate ASC;

    IF @OldestOrderID IS NOT NULL
    BEGIN
        -- Link payment to order
        UPDATE dbo.Payments 
        SET OrderID = @OldestOrderID 
        WHERE PaymentID = SCOPE_IDENTITY();

        -- Update order paid status
        UPDATE dbo.Orders
        SET PaidAmount = PaidAmount + @PaymentAmt
        WHERE OrderID = @OldestOrderID;
    END

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    THROW;
END CATCH;

-- 4. AI Explanation:
-- "This will record a ₹50,000.00 payment received from XYZ Traders and apply it to their oldest pending order. Do you confirm?"


-- =========================================================================
-- EXAMPLE 6: Update Order Status
-- User: "Update Order 1025 status to Completed."
-- =========================================================================

-- 1. AI Intent: UpdateOrder
-- 2. Parameters: { "OrderID": 1025, "Status": "Completed" }
-- 3. Explanation: "Updating order #1025 status to Completed."

-- 4. Generated Parameterized SQL (Requires approval):
DECLARE @TgtOrderID INT = 1025;
DECLARE @NewStatus NVARCHAR(50) = N'Completed';

UPDATE dbo.Orders
SET OrderStatus = @NewStatus
WHERE OrderID = @TgtOrderID
  AND IsDeleted = 0;

-- 5. Mock DB Result:
-- Rows Affected: 1

-- 6. AI Conversation Output:
-- "Order 1025 has been successfully updated to Completed status."


-- =========================================================================
-- EXAMPLE 7: Delete Order (Soft Delete with Warnings)
-- User: "Delete Order 1025."
-- =========================================================================

-- 1. AI Intent: DeleteOrder
-- 2. Parameters: { "OrderID": 1025 }

-- 3. Safety Verification & System Warning:
--    "CAUTION: Deleting Order 1025 will soft-delete this record. It will no longer show up in active reports or customer pending balances, though the ledger audit logs will remain intact. This action requires approval."

-- 4. Generated Parameterized SQL (Requires administrative role & confirmation):
DECLARE @DeleteOrderID INT = 1025;

UPDATE dbo.Orders
SET IsDeleted = 1
WHERE OrderID = @DeleteOrderID;

-- Note: The trigger or database reference keeps Payments intact but soft-deletes the order header.
-- In case payments exist on this order, they are updated to unlinked (OrderID = NULL) so that customer ledger matches.
UPDATE dbo.Payments
SET OrderID = NULL
WHERE OrderID = @DeleteOrderID;

-- 5. AI Conversation Output (Post approval):
-- "Order 1025 has been successfully deleted from active sales databases."
